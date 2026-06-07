using System;

using System.Threading;



namespace UpsmonEventMsg

{

    internal static class Program

    {

        const string SingleInstanceMutex = "Global\\UpsmonEventMsg_v309";



        static int Main(string[] args)

        {

            bool spy = HasFlag(args, "--spy");

            bool test = HasFlag(args, "--test-toast");



            if (HasFlag(args, "--help") || HasFlag(args, "-h"))

            {

                Console.WriteLine("EventMsg.exe - Windows toast replacement for UPSMON PRO popups");

                Console.WriteLine("  (no args)      Wait for UPSMON event, show toast, exit");

                Console.WriteLine("  --spy          Log UPSMON messages to event-msg.log (no toast, stays open)");

                Console.WriteLine("  --test-toast   Show one sample toast and exit");

                return 0;

            }



            if (test)

            {

                ToastNotifier.Show("UPSMON Pro", "Test: Windows notification instead of popup dialog", waitForDisplay: true);

                return 0;

            }



            // UPSMON WinExec's a new instance while the previous toast is still showing.

            if (!spy && InstanceForwarder.TryForward(args))

                return 0;



            bool created;

            using (var mutex = new Mutex(true, SingleInstanceMutex, out created))

            {

                if (!created)

                {

                    if (InstanceForwarder.TryForward(args))

                        return 0;

                    return 0;

                }



                EventLogger.Log("=== UpsmonEventMsg start spy=" + spy + " data=" + UpsmonPaths.DataRoot + " ===");

                if (!spy)

                    RivalListenerCleanup.RemoveRivalListeners();



                using (var host = new EventMessageHost(spy))

                    host.Run();

            }



            EventLogger.Log("EventMsg exit");

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


