using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// UPSMON WinExec's EventMsg while our listener is already up. Stock Delphi exe
    /// FindWindow + SendMessage WM_COPYDATA to the running window and exits.
    /// </summary>
    internal static class InstanceForwarder
    {
        const string TargetClass = "TnUPSMONProEventMesg";
        const string TargetTitle = "UPSMON PRO Event Message";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static bool TryForward(string[] args)
        {
            IntPtr hwnd = FindWindow(TargetClass, TargetTitle);
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                EventLogger.Log("Forward failed: listener window not found");
                return false;
            }

            uint ownerPid;
            GetWindowThreadProcessId(hwnd, out ownerPid);
            if (ownerPid == (uint)Process.GetCurrentProcess().Id)
                return false;

            byte[] payload = BuildPayload(args);
            if (payload == null || payload.Length == 0)
            {
                EventLogger.Log("Forward failed: empty payload cmdline=" + SummarizeCommandLine());
                return false;
            }

            IntPtr lParam = PackCopyData(payload);
            if (lParam == IntPtr.Zero)
            {
                EventLogger.Log("Forward failed: PackCopyData");
                return false;
            }

            IntPtr data = IntPtr.Zero;
            try
            {
                SendMessage(hwnd, NativeMethods.WM_COPYDATA, IntPtr.Zero, lParam);
                data = Marshal.ReadIntPtr(lParam, IntPtr.Size); // LpData offset after DwData
                EventLogger.Log("Forwarded WM_COPYDATA to hwnd=0x" + hwnd.ToInt64().ToString("X")
                    + " pid=" + ownerPid + " bytes=" + payload.Length
                    + " text=\"" + Encoding.Default.GetString(payload).TrimEnd('\0') + "\"");
                return true;
            }
            finally
            {
                if (data == IntPtr.Zero)
                {
                    var cds = (NativeMethods.CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(NativeMethods.CopyDataStruct));
                    data = cds.LpData;
                }
                if (data != IntPtr.Zero) Marshal.FreeHGlobal(data);
                Marshal.FreeHGlobal(lParam);
            }
        }

        static byte[] BuildPayload(string[] args)
        {
            byte[] fromLog = UpsmonEventPayload.BuildFromRecentEventLog();
            if (fromLog != null && fromLog.Length > 0)
                return fromLog;

            string cmd = ExtractCommandLinePayload();
            if (!string.IsNullOrWhiteSpace(cmd))
                return Encoding.Default.GetBytes(cmd);

            if (args != null && args.Length > 0)
            {
                var sb = new StringBuilder();
                foreach (string a in args)
                {
                    if (string.IsNullOrWhiteSpace(a) || a.StartsWith("--", StringComparison.Ordinal))
                        continue;
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(a);
                }
                if (sb.Length > 0)
                    return Encoding.Default.GetBytes(sb.ToString());
            }

            return null;
        }

        static string ExtractCommandLinePayload()
        {
            string line = Environment.CommandLine;
            if (string.IsNullOrWhiteSpace(line)) return null;

            string exe = Process.GetCurrentProcess().MainModule.FileName;
            if (string.IsNullOrEmpty(exe))
                exe = typeof(Program).Assembly.Location;

            line = line.Trim();
            if (line.Length >= 2 && line[0] == '"')
            {
                int end = line.IndexOf('"', 1);
                if (end > 0)
                {
                    string quotedExe = line.Substring(1, end - 1);
                    line = line.Substring(end + 1).TrimStart();
                    if (PathsEqual(quotedExe, exe)) return line;
                }
            }

            if (line.StartsWith(exe, StringComparison.OrdinalIgnoreCase))
                return line.Substring(exe.Length).TrimStart();

            return line;
        }

        static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        }

        static string SummarizeCommandLine()
        {
            string s = Environment.CommandLine ?? "";
            if (s.Length > 200) s = s.Substring(0, 200) + "...";
            return s;
        }

        static IntPtr PackCopyData(byte[] bytes)
        {
            int size = Marshal.SizeOf(typeof(NativeMethods.CopyDataStruct));
            IntPtr lParam = Marshal.AllocHGlobal(size);
            IntPtr data = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, data, bytes.Length);
                var cds = new NativeMethods.CopyDataStruct
                {
                    DwData = IntPtr.Zero,
                    CbData = bytes.Length,
                    LpData = data
                };
                Marshal.StructureToPtr(cds, lParam, false);
                // COPYDATASTRUCT owns LpData until receiver returns; LocalFree only the struct shell.
                return lParam;
            }
            catch
            {
                Marshal.FreeHGlobal(data);
                Marshal.FreeHGlobal(lParam);
                throw;
            }
        }
    }
}
