using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace UpsmonEventMsg
{
    internal sealed class EventMessageHost : IDisposable
    {
        const int SwHide = 0;
        const int WsOverlapped = 0x00000000;
        const int WsCaption = 0x00C00000;
        const int WsSysmenu = 0x00080000;
        const int WsMinimize = 0x20000000;
        const uint WaitTimerId = 1;
        const uint WaitTimeoutMs = 45000;
        const int ExitDelayMs = 2000;

        readonly bool _spy;
        readonly bool _ephemeral;
        readonly NativeMethods.WndProc _wndProcDelegate;
        uint[] _registeredMessages = new uint[0];
        EventCatalog _catalog;
        IntPtr _hwnd = IntPtr.Zero;
        bool _running;
        int _finished;

        public EventMessageHost(bool spy)
        {
            _spy = spy;
            _ephemeral = !spy;
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

            var className = new StringBuilder(256);
            NativeMethods.GetClassName(_hwnd, className, className.Capacity);
            EventLogger.Log((_spy ? "Spy window ready" : "EventMsg ready (toast and exit)")
                + " hwnd=0x" + _hwnd.ToInt64().ToString("X")
                + " class=" + className);

            _registeredMessages = RegisterCandidateMessages();

            if (_ephemeral)
                NativeMethods.SetTimer(_hwnd, new IntPtr(WaitTimerId), WaitTimeoutMs, IntPtr.Zero);

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
            if (msg == 0x0010) // WM_CLOSE
            {
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            if (msg == NativeMethods.WM_TIMER && wParam.ToInt64() == WaitTimerId)
            {
                OnWaitTimeout();
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

            // UPSMON delivers the popup text via WM_COPYDATA (PCM...).
            if (msg != NativeMethods.WM_COPYDATA)
                return;

            EventPayload payload = ParsePayload(msg, wParam, lParam);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Body))
                payload = new EventPayload { Title = Catalog.Title, Body = summary, Critical = false };

            ToastNotifier.Show(payload.Title, payload.Body);
            FinishAfterToast();
        }

        void OnWaitTimeout()
        {
            if (_spy || Interlocked.CompareExchange(ref _finished, 1, 0) != 0)
                return;

            EventLogger.Log("Wait timeout — no WM_COPYDATA");
            byte[] fromLog = UpsmonEventPayload.BuildFromRecentEventLog();
            if (fromLog != null && fromLog.Length > 0)
            {
                string text = Encoding.Default.GetString(fromLog).TrimEnd('\0');
                if (text.Length > 0)
                    ToastNotifier.Show(Catalog.Title, text);
            }

            ScheduleExit(ExitDelayMs);
        }

        void FinishAfterToast()
        {
            if (_spy) return;

            if (_hwnd != IntPtr.Zero)
                NativeMethods.KillTimer(_hwnd, new IntPtr(WaitTimerId));
            ScheduleExit(ExitDelayMs);
        }

        void ScheduleExit(int delayMs)
        {
            IntPtr hwnd = _hwnd;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(delayMs);
                if (hwnd != IntPtr.Zero)
                    NativeMethods.PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            });
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
                    Title = Catalog.Title,
                    Body = Catalog.DescribeEvent((byte)code),
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
                        Title = Catalog.Title,
                        Body = Catalog.DescribeEvent(eventIndex),
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
                string body = Catalog.DescribeEvent(eventIndex);
                if (!string.IsNullOrWhiteSpace(extra)) body += " — " + extra;
                return new EventPayload
                {
                    Title = Catalog.Title,
                    Body = body,
                    Critical = IsCriticalEvent(eventIndex)
                };
            }

            string text = Encoding.Default.GetString(bytes).TrimEnd('\0');
            if (text.Length >= 1 && text.Length <= 2)
            {
                byte eventIndex;
                if (TryParseEventIndexText(text, out eventIndex))
                {
                    return new EventPayload
                    {
                        Title = Catalog.Title,
                        Body = Catalog.DescribeEvent(eventIndex),
                        Critical = IsCriticalEvent(eventIndex)
                    };
                }
            }
            if (text.Length >= 2) return BuildFromText(text);
            return null;
        }

        static bool TryParseEventIndexText(string text, out byte eventIndex)
        {
            eventIndex = 0;
            text = text.Trim();
            if (text.Length == 0) return false;
            int value;
            if (!int.TryParse(text, out value)) return false;
            if (value < 2 || value > 22) return false;
            eventIndex = (byte)value;
            return true;
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
