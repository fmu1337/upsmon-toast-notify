using System;
using System.Threading;

namespace UpsmonEventMsg
{
    internal static class Program
    {
        const string SingleInstanceMutex = "Global\\UpsmonEventMsg_v321";

        static int Main(string[] args)
        {
#if DEV_BUILD
            return RunDev(args);
#else
            return RunProduction(args);
#endif
        }

        static int RunProduction(string[] args)
        {
            if (HasDevFlag(args))
                return 0;

            // Kill PowerShell / legacy windows first — they steal FindWindow from UPSMON.
            RivalListenerCleanup.RemoveRivalListeners();

            // Always toast ourselves from EventRecord when possible.
            // (June/July outages: we forwarded to a foreign hwnd and exited with no toast.)
            if (TryToastFromEventRecord())
                return 0;

            // Another EventMsg may already be waiting — forward only to EventMsg.exe.
            if (StockForwarder.TryForwardToRunningInstance())
                return 0;

            RunHost(spy: false);
            return 0;
        }

        static int RunDev(string[] args)
        {
            bool spy = HasFlag(args, "--spy");
            bool test = HasFlag(args, "--test-toast");
            bool testReplace = HasFlag(args, "--test-toast-replace");

            if (HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                Console.WriteLine("EventMsg-Dev.exe — debug build (do not install over UPSMON)");
                Console.WriteLine("  (no args)             Same as production EventMsg.exe");
                Console.WriteLine("  --spy                 Log Win32 messages to event-msg.log");
                Console.WriteLine("  --test-toast          Sample notification");
                Console.WriteLine("  --test-toast-replace  Two toasts; second replaces first");
                return 0;
            }

            if (testReplace)
            {
#if DEV_BUILD
                ToastNotifier.ShowReplaceTest();
#endif
                return 0;
            }

            if (test)
            {
                ToastNotifier.Show("UPSMON Pro", "Test notification", waitForDisplay: true);
                return 0;
            }

            RivalListenerCleanup.RemoveRivalListeners();

            if (!spy && TryToastFromEventRecord())
                return 0;

            if (!spy && StockForwarder.TryForwardToRunningInstance())
                return 0;

            RunHost(spy);
            return 0;
        }

        /// <summary>
        /// UPSMON writes EventRecord then WinExecs us — poll briefly so we don't miss the line.
        /// </summary>
        static bool TryToastFromEventRecord()
        {
            for (int i = 0; i < 15; i++)
            {
                string text = EventRecordReader.ReadPendingMessage();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    EventLogger.Log("Event: " + text);
                    ToastNotifier.Show("UPSMON Pro", text, waitForDisplay: true);
                    return true;
                }
                Thread.Sleep(80);
            }
            return false;
        }

        static void RunHost(bool spy)
        {
            bool created;
            using (var mutex = new Mutex(true, SingleInstanceMutex, out created))
            {
                if (!created)
                {
                    if (!TryToastFromEventRecord())
                        StockForwarder.TryForwardToRunningInstance();
                    return;
                }

                RivalListenerCleanup.RemoveRivalListeners();

                using (var host = new EventMessageHost(spy))
                    host.Run();
            }
        }

        static bool HasDevFlag(string[] args)
        {
            return HasFlag(args, "--spy")
                || HasFlag(args, "--test-toast")
                || HasFlag(args, "--test-toast-replace")
                || HasFlag(args, "--help")
                || HasFlag(args, "-h");
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
