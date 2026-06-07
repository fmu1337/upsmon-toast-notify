using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// Old v1/v2 installs left a PowerShell listener with the same window class.
    /// UPSMON uses FindWindow(class, title) and sends WM_COPYDATA to the first match.
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

                if (TryTerminateProcess(ownerPid))
                {
                    removed++;
                    EventLogger.Log("Removed legacy listener pid=" + ownerPid + " hwnd=0x"
                        + hWnd.ToInt64().ToString("X"));
                }
                else
                {
                    EventLogger.Log("WARNING: legacy listener pid=" + ownerPid
                        + " hwnd=0x" + hWnd.ToInt64().ToString("X")
                        + " blocks UPSMON messages. Run tools\\Remove-LegacyListener.ps1 as Administrator.");
                }
                return true;
            }, IntPtr.Zero);

            if (removed > 0)
                System.Threading.Thread.Sleep(500);

            IntPtr found = FindWindow(TargetClass, TargetTitle);
            if (found != IntPtr.Zero)
            {
                uint foundPid;
                GetWindowThreadProcessId(found, out foundPid);
                if (foundPid != selfPid && foundPid != 0)
                {
                    EventLogger.Log("WARNING: FindWindow still points to foreign pid=" + foundPid
                        + ". UPSMON events will not reach this EventMsg until it is removed.");
                }
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
