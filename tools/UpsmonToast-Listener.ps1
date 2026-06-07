# UPSMON toast listener — runs via Scheduled Task (PowerShell is allowed under Device Guard).
# Creates the same hidden window as stock EventMsg.exe and shows Windows toasts.
param([switch]$Spy)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
if (-not $Root -or $Root -eq $PSScriptRoot) { $Root = $env:ProgramData + '\UpsmonToastNotify' }

$LogFile = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'UpsmonToastNotify\event-msg.log'
$DataRoot = 'C:\Users\Public\UPSMON-Pro'
if (-not (Test-Path -LiteralPath $DataRoot)) { $DataRoot = $Root }
$ToastScript = Join-Path $PSScriptRoot 'Show-Toast.ps1'

function Write-Log([string]$Message) {
    $line = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  ' + $Message
    try {
        $dir = Split-Path -Parent $LogFile
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
    } catch { }
}

$listenerCode = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class UpsmonToastListener
{
    const int WM_COPYDATA = 0x004A;
    const int WM_USER = 0x0400;
    static string _toastScript;
    static string _logFile;
    static string _dataRoot;
    static bool _spy;
    static Dictionary<byte, string> _events = new Dictionary<byte, string>();
    static uint[] _regMsgs;
    static Native.WndProc _proc;
    static IntPtr _hwnd;
    static bool _running;

    public static void Run(string toastScript, string logFile, string dataRoot, bool spy)
    {
        _toastScript = toastScript;
        _logFile = logFile;
        _dataRoot = dataRoot;
        _spy = spy;
        LoadEvents();
        _proc = WindowProc;
        _regMsgs = RegisterMsgs();
        bool created;
        using (var mtx = new System.Threading.Mutex(true, "Global\\UpsmonEventMsgToast", out created))
        {
            if (!created) { Log("Second instance blocked"); return; }
            Log("=== UpsmonToast listener start spy=" + spy + " ===");
            RunLoop();
        }
    }

    static void LoadEvents()
    {
        Seed();
        string ini = Path.Combine(_dataRoot, "UPSMON.ini");
        string lang = "English";
        if (File.Exists(ini))
        {
            foreach (string line in File.ReadAllLines(ini, Encoding.Default))
            {
                if (line.StartsWith("DefLang=", StringComparison.OrdinalIgnoreCase))
                    lang = line.Substring(8).Trim();
            }
        }
        string table = Path.Combine(@"C:\Program Files (x86)\UPSMONPRO", lang + ".txt");
        if (!File.Exists(table)) table = Path.Combine(@"C:\Program Files (x86)\UPSMONPRO", "English.txt");
        if (!File.Exists(table)) return;
        foreach (string line in File.ReadAllLines(table, Encoding.Default))
        {
            int eq = line.IndexOf('=');
            if (eq <= 0 || !line.StartsWith("Event", StringComparison.OrdinalIgnoreCase)) continue;
            int num;
            if (!int.TryParse(line.Substring(5, eq - 5), out num) || num < 2 || num > 22) continue;
            _events[(byte)num] = line.Substring(eq + 1).Trim();
        }
    }

    static void Seed()
    {
        _events[2] = "Connection Restore";
        _events[3] = "Power Failure";
        _events[4] = "Power Restore";
        _events[6] = "UPS Battery Low";
        _events[7] = "Battery Failed";
        _events[8] = "Overload";
        _events[9] = "UPS Failed";
        _events[13] = "System Shutdown";
    }

    static string Describe(byte code)
    {
        string t;
        return _events.TryGetValue(code, out t) ? t : ("UPSMON event " + code);
    }

    static byte PcmToEvent(byte pcm)
    {
        switch (pcm)
        {
            case 1: return 3; case 2: return 4; case 3: return 5; case 4: return 6;
            case 5: return 7; case 8: return 13; case 10: return 9; case 11: return 8;
            case 12: return 2; default: return pcm;
        }
    }

    static void RunLoop()
    {
        IntPtr inst = Native.GetModuleHandle(null);
        var wc = new Native.WndClass();
        wc.LpszClassName = "TnUPSMONProEventMesg";
        wc.LpfnWndProc = _proc;
        wc.HInstance = inst;
        wc.HbrBackground = new IntPtr(6);
        Native.RegisterClass(ref wc);
        _hwnd = Native.CreateWindowEx(0, wc.LpszClassName, "UPSMON PRO Event Message",
            0x00CA0000 | 0x20000000, -32000, -32000, 420, 190, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
        Native.ShowWindow(_hwnd, 0);
        _running = true;
        Native.Msg msg;
        while (_running && Native.GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessage(ref msg);
        }
    }

    static uint[] RegisterMsgs()
    {
        string[] names = { "PROStart", "EventMsg1", "DelayEvent", "ShowDisp", "UPSMON", "EventMsg" };
        var ids = new uint[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            ids[i] = Native.RegisterWindowMessage(names[i]);
            Log("RegisterWindowMessage(\"" + names[i] + "\")=" + ids[i]);
        }
        return ids;
    }

    static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == 0x0010) { Native.PostQuitMessage(0); return IntPtr.Zero; }
        if (IsCustom(msg)) Handle(hWnd, msg, wParam, lParam);
        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    static bool IsCustom(uint msg)
    {
        if (msg == WM_COPYDATA) return true;
        if (msg >= WM_USER && msg < WM_USER + 0x400) return true;
        foreach (uint r in _regMsgs) if (r != 0 && msg == r) return true;
        return false;
    }

    static void Handle(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Log(DescribeMsg(msg, wParam, lParam));
        if (_spy) return;
        string body = ParseBody(msg, wParam, lParam);
        if (string.IsNullOrWhiteSpace(body)) body = "UPSMON Pro event";
        ShowToast("UPSMON Pro", body);
    }

    static string ParseBody(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_COPYDATA && lParam != IntPtr.Zero)
        {
            var cds = (Native.CopyData)Marshal.PtrToStructure(lParam, typeof(Native.CopyData));
            if (cds.CbData > 0 && cds.LpData != IntPtr.Zero)
            {
                byte[] b = new byte[Math.Min(cds.CbData, 512)];
                Marshal.Copy(cds.LpData, b, 0, b.Length);
                if (b.Length >= 4 && b[0] == (byte)'P' && b[1] == (byte)'C' && b[2] == (byte)'M')
                    return Describe(PcmToEvent(b[3]));
                return Encoding.Default.GetString(b).TrimEnd('\0');
            }
        }
        int code = (int)(wParam.ToInt64() & 0xFF);
        if (code >= 2 && code <= 22) return Describe((byte)code);
        return null;
    }

    static void ShowToast(string title, string body)
    {
        try
        {
            string t = title.Replace("\"", "`\"");
            string b = body.Replace("\"", "`\"");
            var psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + _toastScript + "\" -Title \"" + t + "\" -Body \"" + b + "\"";
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            using (var p = Process.Start(psi)) { if (p != null) p.WaitForExit(8000); }
        }
        catch (Exception ex) { Log("Toast error: " + ex.Message); }
    }

    static string DescribeMsg(uint msg, IntPtr wParam, IntPtr lParam)
    {
        return string.Format("msg=0x{0:X4} wParam=0x{1:X} lParam=0x{2:X}", msg, wParam.ToInt64(), lParam.ToInt64());
    }

    static void Log(string s)
    {
        try
        {
            string dir = Path.GetDirectoryName(_logFile);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(_logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }

    static class Native
    {
        public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WndClass
        {
            public uint Style; public WndProc LpfnWndProc; public int CbClsExtra; public int CbWndExtra;
            public IntPtr HInstance; public IntPtr HIcon; public IntPtr HCursor; public IntPtr HbrBackground;
            public string LpszMenuName; public string LpszClassName;
        }
        [StructLayout(LayoutKind.Sequential)] public struct Msg
        {
            public IntPtr Hwnd; public uint Message; public IntPtr WParam; public IntPtr LParam;
            public uint Time; public int PtX; public int PtY;
        }
        [StructLayout(LayoutKind.Sequential)] public struct CopyData
        {
            public IntPtr DwData; public int CbData; public IntPtr LpData;
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern ushort RegisterClass(ref WndClass lpWndClass);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr CreateWindowEx(int ex, string cls, string title, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool GetMessage(out Msg lpMsg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref Msg lpMsg);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref Msg lpMsg);
        [DllImport("user32.dll")] public static extern void PostQuitMessage(int nExitCode);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern uint RegisterWindowMessage(string s);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
'@

if (-not ('UpsmonToastListener' -as [type])) {
    Add-Type -TypeDefinition $listenerCode -Language CSharp
}

[UpsmonToastListener]::Run($ToastScript, $LogFile, $DataRoot, [bool]$Spy)
