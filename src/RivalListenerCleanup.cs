using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// Old v1/v2 installs left a PowerShell listener with the same window class.
    /// UPSMON uses FindWindow(class, title) and sends WM_COPYDATA to the first match.
    /// Only kill non-EventMsg owners — never our own toast process.
    /// </summary>
    internal static class RivalListenerCleanup
    {
        const string TargetClass = "TnUPSMONProEventMesg";
        const string TargetTitle = "UPSMON PRO Event Message";
        const int ProcessTerminate = 0x0001;

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hProcess);

        public static void RemoveRivalListeners()
        {
            uint selfPid = (uint)Process.GetCurrentProcess().Id;
            int removed = 0;

            EnumWindows((hWnd, lParam) =>
            {
                var cls = new StringBuilder(256);
                if (GetClassName(hWnd, cls, cls.Capacity) == 0) return true;
                if (cls.ToString() != TargetClass) return true;

                uint ownerPid;
                GetWindowThreadProcessId(hWnd, out ownerPid);
                if (ownerPid == 0 || ownerPid == selfPid) return true;

                if (IsOurEventMsg(ownerPid)) return true;

                string ownerName = SafeProcessName(ownerPid);
                if (TryTerminateProcess(ownerPid))
                {
                    removed++;
                    EventLogger.Log("Removed rival listener pid=" + ownerPid
                        + " name=" + ownerName
                        + " hwnd=0x" + hWnd.ToInt64().ToString("X"));
                }
                else
                {
                    EventLogger.Log("WARNING: rival listener pid=" + ownerPid
                        + " name=" + ownerName
                        + " hwnd=0x" + hWnd.ToInt64().ToString("X")
                        + " blocks UPSMON. Run tools\\Remove-LegacyListener.ps1 as Administrator.");
                }
                return true;
            }, IntPtr.Zero);

            if (removed > 0)
                System.Threading.Thread.Sleep(400);
        }

        public static bool IsOurEventMsg(uint pid)
        {
            string name = SafeProcessName(pid);
            return name.Equals("EventMsg", StringComparison.OrdinalIgnoreCase)
                || name.Equals("EventMsg-Dev", StringComparison.OrdinalIgnoreCase);
        }

        static string SafeProcessName(uint pid)
        {
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                    return p.ProcessName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        static bool TryTerminateProcess(uint pid)
        {
            if (pid == 0) return false;
            IntPtr hProcess = OpenProcess(ProcessTerminate, false, pid);
            if (hProcess == IntPtr.Zero) return false;
            try
            {
                return TerminateProcess(hProcess, 0);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }
}
