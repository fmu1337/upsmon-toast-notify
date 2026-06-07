using System;
using System.Threading;

namespace UpsmonEventMsg
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            bool spy = HasFlag(args, "--spy");
            bool test = HasFlag(args, "--test-toast");

            if (HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                Console.WriteLine("EventMsg.exe - Windows toast replacement for UPSMON PRO popups");
                Console.WriteLine("  (no args)      Run message listener + toast notifications");
                Console.WriteLine("  --spy          Log UPSMON messages to event-msg.log (no toast)");
                Console.WriteLine("  --test-toast   Show one sample toast and exit");
                return 0;
            }

            if (test)
            {
                ToastNotifier.Show("UPSMON Pro", "Test: Windows notification instead of popup dialog", false);
                return 0;
            }

            bool created;
            using (var mutex = new Mutex(true, "Global\\UpsmonEventMsgToast", out created))
            {
                if (!created)
                {
                    EventLogger.Log("Second instance blocked");
                    return 0;
                }

                var catalog = EventCatalog.Load();
                using (var host = new EventMessageHost(catalog, spy))
                {
                    EventLogger.Log("=== UpsmonEventMsg start spy=" + spy + " data=" + UpsmonPaths.DataRoot + " ===");
                    host.Run();
                }
            }

            return 0;
        }

        static bool HasFlag(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
