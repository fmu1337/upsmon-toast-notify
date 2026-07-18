using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// Forward only to a live EventMsg.exe. Never hand events to PowerShell / strangers.
    /// </summary>
    internal static class StockForwarder
    {
        const string ClassName = "TnUPSMONProEventMesg";
        const string WindowTitle = "UPSMON PRO Event Message";

        public static bool TryForwardToRunningInstance()
        {
            IntPtr hwnd = NativeMethods.FindWindow(ClassName, WindowTitle);
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return false;

            uint ownerPid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out ownerPid);
            if (ownerPid == 0 || ownerPid == (uint)Process.GetCurrentProcess().Id) return false;

            if (!RivalListenerCleanup.IsOurEventMsg(ownerPid))
            {
                EventLogger.Log("Skip forward: foreign window pid=" + ownerPid
                    + " name=" + SafeName(ownerPid) + " (will toast ourselves)");
                return false;
            }

            string text = EventRecordReader.ReadPendingMessage();
            if (string.IsNullOrWhiteSpace(text)) return false;

            SendText(hwnd, text);
            EventLogger.Log("Forwarded to EventMsg pid=" + ownerPid + ": " + text);
            return true;
        }

        static string SafeName(uint pid)
        {
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                    return p.ProcessName;
            }
            catch { return "?"; }
        }

        static void SendText(IntPtr hwnd, string text)
        {
            byte[] bytes = Encoding.Default.GetBytes(text);
            IntPtr block = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.CopyDataStruct)));
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
                Marshal.StructureToPtr(cds, block, false);
                NativeMethods.SendMessage(hwnd, NativeMethods.WM_COPYDATA, IntPtr.Zero, block);
            }
            finally
            {
                Marshal.FreeHGlobal(data);
                Marshal.FreeHGlobal(block);
            }
        }
    }
}
