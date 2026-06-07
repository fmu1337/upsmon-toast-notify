using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    internal sealed class EventMessageHost : IDisposable
    {
        const int SwHide = 0;
        const int WsOverlapped = 0x00000000;
        const int WsCaption = 0x00C00000;
        const int WsSysmenu = 0x00080000;
        const int WsMinimize = 0x20000000;

        readonly EventCatalog _catalog;
        readonly bool _spy;
        readonly NativeMethods.WndProc _wndProcDelegate;
        readonly uint[] _registeredMessages;
        IntPtr _hwnd = IntPtr.Zero;
        bool _running;

        public EventMessageHost(EventCatalog catalog, bool spy)
        {
            _catalog = catalog;
            _spy = spy;
            _wndProcDelegate = WindowProc;
            _registeredMessages = RegisterCandidateMessages();
        }

        public void Run()
        {
            IntPtr hInstance = NativeMethods.GetModuleHandle(null);
            var wc = new NativeMethods.WndClass
            {
                LpszClassName = "TnUPSMONProEventMesg",
                LpfnWndProc = _wndProcDelegate,
                HInstance = hInstance,
                HbrBackground = new IntPtr(5 + 1) // COLOR_WINDOW + 1
            };
            NativeMethods.RegisterClass(ref wc);

            _hwnd = NativeMethods.CreateWindowEx(
                0,
                wc.LpszClassName,
                "UPSMON PRO Event Message",
                WsOverlapped | WsCaption | WsSysmenu | WsMinimize,
                -32000, -32000, 420, 190,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());

            NativeMethods.ShowWindow(_hwnd, SwHide);
            NativeMethods.UpdateWindow(_hwnd);
            EventLogger.Log(_spy ? "Spy window ready" : "Toast EventMsg ready class=TnUPSMONProEventMesg");

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
            {
                ids[i] = NativeMethods.RegisterWindowMessage(names[i]);
                EventLogger.Log("RegisterWindowMessage(\"" + names[i] + "\")=" + ids[i]);
            }
            return ids;
        }

        IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == 0x0010) // WM_CLOSE
            {
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            if (IsCustomMessage(msg))
                HandleCustomMessage(hWnd, msg, wParam, lParam);

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        bool IsCustomMessage(uint msg)
        {
            if (msg == NativeMethods.WM_COPYDATA) return true;
            if (msg >= NativeMethods.WM_USER && msg < NativeMethods.WM_USER + 0x400) return true;
            for (int i = 0; i < _registeredMessages.Length; i++)
                if (_registeredMessages[i] != 0 && msg == _registeredMessages[i]) return true;
            return false;
        }

        void HandleCustomMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            string summary = DescribeMessage(msg, wParam, lParam);
            EventLogger.Log(summary);

            if (_spy) return;

            EventPayload payload = ParsePayload(msg, wParam, lParam);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Body))
                payload = new EventPayload { Title = _catalog.Title, Body = summary, Critical = false };

            ToastNotifier.Show(payload.Title, payload.Body);
        }

        EventPayload ParsePayload(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_COPYDATA && lParam != IntPtr.Zero)
                return ParseCopyData(lParam);

            // Try lParam as Delphi short string / ANSI string.
            if (lParam != IntPtr.Zero)
            {
                string s1 = NativeMethods.ReadDelphiShortString(lParam, 256);
                if (s1.Length >= 2) return BuildFromText(s1);

                string s2 = NativeMethods.ReadAnsiString(lParam, 256);
                if (s2.Length >= 2) return BuildFromText(s2);
            }

            // Many UPSMON builds send event id in wParam low byte.
            int code = (int)(wParam.ToInt64() & 0xFF);
            if (code >= 2 && code <= 22)
            {
                return new EventPayload
                {
                    Title = _catalog.Title,
                    Body = _catalog.DescribeEvent((byte)code),
                    Critical = IsCriticalEvent((byte)code)
                };
            }

            // Binary blob at lParam: [ 'P','C','M', eventCode, ... ]
            if (lParam != IntPtr.Zero)
            {
                byte[] head = new byte[16];
                Marshal.Copy(lParam, head, 0, head.Length);
                if (head[0] == (byte)'P' && head[1] == (byte)'C' && head[2] == (byte)'M')
                {
                    byte eventIndex = PcmCodeToEventIndex(head[3]);
                    return new EventPayload
                    {
                        Title = _catalog.Title,
                        Body = _catalog.DescribeEvent(eventIndex),
                        Critical = IsCriticalEvent(eventIndex)
                    };
                }
            }

            return null;
        }

        EventPayload ParseCopyData(IntPtr lParam)
        {
            var cds = (NativeMethods.CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(NativeMethods.CopyDataStruct));
            if (cds.CbData <= 0 || cds.LpData == IntPtr.Zero) return null;

            int len = Math.Min(cds.CbData, 512);
            byte[] bytes = new byte[len];
            Marshal.Copy(cds.LpData, bytes, 0, len);

            if (len >= 4 && bytes[0] == (byte)'P' && bytes[1] == (byte)'C' && bytes[2] == (byte)'M')
            {
                byte pcmCode = bytes[3];
                byte eventIndex = PcmCodeToEventIndex(pcmCode);
                string extra = ExtractEmbeddedStrings(bytes, 4);
                string body = _catalog.DescribeEvent(eventIndex);
                if (!string.IsNullOrWhiteSpace(extra)) body += " — " + extra;
                return new EventPayload
                {
                    Title = _catalog.Title,
                    Body = body,
                    Critical = IsCriticalEvent(eventIndex)
                };
            }

            string text = Encoding.Default.GetString(bytes).TrimEnd('\0');
            if (text.Length >= 2) return BuildFromText(text);
            return null;
        }

        static EventPayload BuildFromText(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return null;
            bool critical = text.IndexOf("shutdown", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("battery low", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("power fail", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;
            return new EventPayload { Title = "UPSMON Pro", Body = text, Critical = critical };
        }

        static string ExtractEmbeddedStrings(byte[] bytes, int offset)
        {
            var parts = new StringBuilder();
            int i = offset;
            while (i < bytes.Length)
            {
                int len = bytes[i];
                if (len <= 0 || len > 120 || i + 1 + len > bytes.Length) break;
                string part = Encoding.Default.GetString(bytes, i + 1, len);
                if (part.Length > 0)
                {
                    if (parts.Length > 0) parts.Append(" | ");
                    parts.Append(part);
                }
                i += 1 + len;
            }
            return parts.ToString();
        }

        static byte PcmCodeToEventIndex(byte pcmCode)
        {
            // Reverse of UPSMONPro PCM mapping (see decomp-ser-commands FUN_0046148c).
            switch (pcmCode)
            {
                case 1: return 3;   // power failure
                case 2: return 4;   // power restore
                case 3: return 5;   // self test
                case 4: return 6;   // battery low
                case 5: return 7;   // battery failed
                case 6: return 17;  // bypass recover
                case 7: return 19;  // immediate reboot
                case 8: return 13;  // system shutdown
                case 9: return 14;  // battery normal
                case 10: return 9;  // UPS failed
                case 11: return 8;  // overload
                case 12: return 2;  // connection restore
                case 13: return 2;  // connection error / restore pair
                default: return pcmCode;
            }
        }

        static bool IsCriticalEvent(byte eventIndex)
        {
            return eventIndex == 3 || eventIndex == 6 || eventIndex == 7
                || eventIndex == 8 || eventIndex == 9 || eventIndex == 13 || eventIndex == 21;
        }

        static string DescribeMessage(uint msg, IntPtr wParam, IntPtr lParam)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("msg=0x{0:X4} wParam=0x{1:X} lParam=0x{2:X}", msg, wParam.ToInt64(), lParam.ToInt64());
            if (msg == NativeMethods.WM_COPYDATA && lParam != IntPtr.Zero)
                sb.Append(" ").Append(NativeMethods.DescribeCopyData(lParam));
            else if (lParam != IntPtr.Zero)
            {
                sb.Append(" s=\"").Append(NativeMethods.ReadDelphiShortString(lParam, 200)).Append("\"");
                sb.Append(" a=\"").Append(NativeMethods.ReadAnsiString(lParam, 200)).Append("\"");
            }
            return sb.ToString();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        sealed class EventPayload
        {
            public string Title;
            public string Body;
            public bool Critical;
        }
    }
}
