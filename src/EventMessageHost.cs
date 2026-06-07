using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// Stock EventMsg protocol: invisible TnUPSMONProEventMesg window + Timer1.
    /// No modal dialog — toast instead of Label1 + OK.
    /// </summary>
    internal sealed class EventMessageHost : IDisposable
    {
        const int SwHide = 0;
        const int WsOverlapped = 0x00000000;
        const int WsCaption = 0x00C00000;
        const int WsSysmenu = 0x00080000;
        const int WsMinimize = 0x20000000;
        const uint Timer1 = 1;
        const uint Timer1Ms = 500;

        readonly bool _spy;
        readonly NativeMethods.WndProc _wndProcDelegate;
        uint[] _registeredMessages = new uint[0];
        EventCatalog _catalog;
        IntPtr _hwnd = IntPtr.Zero;
        bool _running;
        bool _closed;

        public EventMessageHost(bool spy)
        {
            _spy = spy;
            _wndProcDelegate = WindowProc;
        }

        EventCatalog Catalog
        {
            get { return _catalog ?? (_catalog = EventCatalog.Load()); }
        }

        public void Run()
        {
            IntPtr hInstance = NativeMethods.GetModuleHandle(null);
            var wc = new NativeMethods.WndClass
            {
                LpszClassName = "TnUPSMONProEventMesg",
                LpfnWndProc = _wndProcDelegate,
                HInstance = hInstance,
                HbrBackground = new IntPtr(5 + 1)
            };
            NativeMethods.RegisterClass(ref wc);

            _hwnd = NativeMethods.CreateWindowEx(
                0, wc.LpszClassName, "UPSMON PRO Event Message",
                WsOverlapped | WsCaption | WsSysmenu | WsMinimize,
                -32000, -32000, 420, 190,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());

            NativeMethods.ShowWindow(_hwnd, SwHide);
            NativeMethods.UpdateWindow(_hwnd);

            _registeredMessages = RegisterCandidateMessages();

            if (!_spy)
                NativeMethods.SetTimer(_hwnd, new IntPtr(Timer1), Timer1Ms, IntPtr.Zero);

            EventLogger.Log(_spy ? "Spy mode" : "Waiting for UPSMON event");

            _running = true;
            NativeMethods.Msg msg;
            while (_running && NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        public void Dispose()
        {
            _running = false;
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }

        uint[] RegisterCandidateMessages()
        {
            string[] names =
            {
                "PROStart", "EventMsg1", "UPSMONEvent", "UPSMONMsg", "UpsmonEvent",
                "DelayEvent", "ShowDisp", "UPSMON", "EventMsg", "PowercomEvent"
            };
            var ids = new uint[names.Length];
            for (int i = 0; i < names.Length; i++)
                ids[i] = NativeMethods.RegisterWindowMessage(names[i]);
            return ids;
        }

        IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == 0x0010)
            {
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            if (msg == NativeMethods.WM_TIMER && wParam.ToInt64() == Timer1)
            {
                OnTimer1();
                return IntPtr.Zero;
            }

            if (IsEventMessage(msg))
                OnEventMessage(msg, wParam, lParam);

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        void OnTimer1()
        {
            if (_spy || _closed) return;

            string text = EventRecordReader.ReadPendingMessage();
            if (string.IsNullOrWhiteSpace(text)) return;

            EventLogger.Log("Event: " + text);
            ShowToastAndExit(text);
        }

        void OnEventMessage(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (_spy)
            {
                EventLogger.Log(DescribeMessage(msg, wParam, lParam));
                return;
            }

            string text = EventParser.ParseText(Catalog, msg, wParam, lParam);
            if (string.IsNullOrWhiteSpace(text)) return;

            EventLogger.Log("Event: " + text);
            ShowToastAndExit(text);
        }

        void ShowToastAndExit(string body)
        {
            if (_closed) return;
            _closed = true;

            if (_hwnd != IntPtr.Zero)
                NativeMethods.KillTimer(_hwnd, new IntPtr(Timer1));

            ToastNotifier.Show(Catalog.Title, body, waitForDisplay: true);
            NativeMethods.PostQuitMessage(0);
        }

        bool IsEventMessage(uint msg)
        {
            if (msg == NativeMethods.WM_COPYDATA) return true;
            if (msg >= NativeMethods.WM_USER && msg < NativeMethods.WM_USER + 0x400) return true;
            for (int i = 0; i < _registeredMessages.Length; i++)
                if (_registeredMessages[i] != 0 && msg == _registeredMessages[i]) return true;
            return false;
        }

        static string DescribeMessage(uint msg, IntPtr wParam, IntPtr lParam)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("msg=0x{0:X4} wParam=0x{1:X} lParam=0x{2:X}", msg, wParam.ToInt64(), lParam.ToInt64());
            if (msg == NativeMethods.WM_COPYDATA && lParam != IntPtr.Zero)
                sb.Append(" ").Append(NativeMethods.DescribeCopyData(lParam));
            return sb.ToString();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
