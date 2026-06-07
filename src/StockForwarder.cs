using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>Second instance: like stock FindWindow + SendMessage, then exit.</summary>
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

            string text = EventRecordReader.ReadPendingMessage();
            if (string.IsNullOrWhiteSpace(text)) return false;

            SendText(hwnd, text);
            EventLogger.Log("Forwarded to running EventMsg: " + text);
            return true;
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
