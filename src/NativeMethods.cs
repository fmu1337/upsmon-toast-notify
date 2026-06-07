using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    internal static class NativeMethods
    {
        public const int WM_COPYDATA = 0x004A;
        public const int WM_USER = 0x0400;

        [StructLayout(LayoutKind.Sequential)]
        public struct CopyDataStruct
        {
            public IntPtr DwData;
            public int CbData;
            public IntPtr LpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern ushort RegisterClass(ref WndClass lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName,
            int dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref Msg lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref Msg lpMsg);

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

        [DllImport("user32.dll")]
        public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        public const int WM_TIMER = 0x0113;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetPrivateProfileString(
            string section, string key, string def, StringBuilder retVal,
            int size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetPrivateProfileInt(string section, string key, int def, string filePath);

        public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WndClass
        {
            public uint Style;
            public WndProc LpfnWndProc;
            public int CbClsExtra;
            public int CbWndExtra;
            public IntPtr HInstance;
            public IntPtr HIcon;
            public IntPtr HCursor;
            public IntPtr HbrBackground;
            public string LpszMenuName;
            public string LpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Msg
        {
            public IntPtr Hwnd;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int PtX;
            public int PtY;
        }

        public static string ReadAnsiString(IntPtr ptr, int maxLen)
        {
            if (ptr == IntPtr.Zero) return string.Empty;
            var bytes = new byte[maxLen];
            Marshal.Copy(ptr, bytes, 0, maxLen);
            int len = Array.IndexOf(bytes, (byte)0);
            if (len < 0) len = maxLen;
            return Encoding.Default.GetString(bytes, 0, len);
        }

        public static string ReadDelphiShortString(IntPtr ptr, int maxLen)
        {
            if (ptr == IntPtr.Zero) return string.Empty;
            byte len = Marshal.ReadByte(ptr);
            if (len == 0 || len > maxLen - 1) return ReadAnsiString(IntPtr.Add(ptr, 1), maxLen - 1);
            var bytes = new byte[len];
            Marshal.Copy(IntPtr.Add(ptr, 1), bytes, 0, len);
            return Encoding.Default.GetString(bytes);
        }

        public static string DescribeCopyData(IntPtr lParam)
        {
            var cds = (CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(CopyDataStruct));
            if (cds.CbData <= 0 || cds.LpData == IntPtr.Zero) return "(empty COPYDATA)";
            int len = Math.Min(cds.CbData, 512);
            var bytes = new byte[len];
            Marshal.Copy(cds.LpData, bytes, 0, len);
            var text = Encoding.Default.GetString(bytes).TrimEnd('\0');
            var hex = BitConverter.ToString(bytes, 0, Math.Min(len, 64));
            return "dwData=" + cds.DwData + " text=\"" + text + "\" hex=" + hex;
        }
    }
}
