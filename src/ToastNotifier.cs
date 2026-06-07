using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;

namespace UpsmonEventMsg
{
    internal static class ToastNotifier
    {
        const string ToastTag = "UpsmonEvent";
        const string ToastGroup = "UpsmonPro";

        static bool _bootstrapped;

        public static void Show(string title, string body, bool waitForDisplay = false)
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(exe))
                exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EventMsg.exe");

            try
            {
                ToastShortcut.Ensure(exe);
                ShowWinRtToast(title, body, ToastShortcut.AppId);
                EventLogger.Log("Toast: " + body);
            }
            catch (Exception ex)
            {
                EventLogger.Log("Toast error: " + ex.Message);
                ShowBalloon(title, body, true);
            }

            if (waitForDisplay)
                Thread.Sleep(4500);
        }

#if DEV_BUILD
        public static void ShowReplaceTest()
        {
            Show("UPSMON Pro", "First — do not close", waitForDisplay: false);
            Thread.Sleep(2500);
            Show("UPSMON Pro", "Second — should replace first", waitForDisplay: true);
        }
#endif

        static void BootstrapWinRt()
        {
            if (_bootstrapped) return;
            _bootstrapped = true;

            string framework = Environment.Is64BitProcess ? "Framework64" : "Framework";
            string winRt = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\" + framework + @"\v4.0.30319\System.Runtime.WindowsRuntime.dll");
            if (File.Exists(winRt))
                Assembly.LoadFrom(winRt);

            ResolveWinRtType("Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime");
            ResolveWinRtType("Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType=WindowsRuntime");
        }

        static void ShowWinRtToast(string title, string body, string appId)
        {
            BootstrapWinRt();

            Type managerType = ResolveWinRtType(
                "Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime");
            Type docType = ResolveWinRtType(
                "Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType=WindowsRuntime");
            Type toastType = ResolveWinRtType(
                "Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime");

            DismissAppToasts(managerType, appId);

            string xml = BuildToastXml(title, body);
            object doc = Activator.CreateInstance(docType);
            docType.GetMethod("LoadXml", new[] { typeof(string) }).Invoke(doc, new object[] { xml });

            object toast = Activator.CreateInstance(toastType, doc);
            ConfigureToast(toast);

            object notifier = managerType
                .GetMethod("CreateToastNotifier", new[] { typeof(string) })
                .Invoke(null, new object[] { appId });
            notifier.GetType().GetMethod("Show").Invoke(notifier, new[] { toast });
        }

        static void ConfigureToast(object toast)
        {
            SetProperty(toast, "Tag", ToastTag);
            SetProperty(toast, "Group", ToastGroup);
            SetProperty(toast, "SuppressPopup", false);
        }

        static void SetProperty(object target, string name, object value)
        {
            PropertyInfo prop = target.GetType().GetProperty(name);
            if (prop != null && prop.CanWrite)
                prop.SetValue(target, value, null);
        }

        static void DismissAppToasts(Type managerType, string appId)
        {
            try
            {
                object history = managerType.GetProperty("History").GetValue(null, null);
                if (history == null) return;

                TryHistory(history, "Remove", ToastTag, ToastGroup, appId);
                TryHistory(history, "RemoveGroup", ToastGroup, appId);
                TryHistory(history, "Clear", appId);
                Thread.Sleep(150);
            }
            catch { }
        }

        static void TryHistory(object history, string name, params object[] args)
        {
            foreach (MethodInfo method in history.GetType().GetMethods())
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length != args.Length) continue;
                for (int i = 0; i < p.Length; i++)
                {
                    if (args[i] != null && !p[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                        goto next;
                }
                try { method.Invoke(history, args); } catch { }
                return;
                next: ;
            }
        }

        static Type ResolveWinRtType(string fullName)
        {
            Type type = Type.GetType(fullName, false);
            if (type != null) return type;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName, false);
                if (type != null) return type;
            }

            throw new InvalidOperationException("WinRT type not found: " + fullName);
        }

        static string BuildToastXml(string title, string body)
        {
            string escapedTitle = SecurityElement.Escape(title) ?? string.Empty;
            string escapedBody = SecurityElement.Escape(body) ?? string.Empty;
            return new StringBuilder()
                .AppendLine("<toast activationType=\"foreground\" duration=\"long\" priority=\"high\">")
                .AppendLine("  <audio src=\"ms-winsoundevent:Notification.Default\"/>")
                .AppendLine("  <visual><binding template=\"ToastGeneric\">")
                .AppendLine("    <text>" + escapedTitle + "</text>")
                .AppendLine("    <text>" + escapedBody + "</text>")
                .AppendLine("  </binding></visual>")
                .AppendLine("</toast>")
                .ToString();
        }

        static void ShowBalloon(string title, string body, bool wait)
        {
            if (!wait)
            {
                var thread = new Thread(() => ShowBalloonSync(title, body));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                return;
            }

            ShowBalloonSync(title, body);
        }

        static void ShowBalloonSync(string title, string body)
        {
            using (var icon = new System.Windows.Forms.NotifyIcon())
            {
                icon.Icon = System.Drawing.SystemIcons.Information;
                icon.Visible = true;
                icon.ShowBalloonTip(8000, title, body, System.Windows.Forms.ToolTipIcon.Info);
                Thread.Sleep(9000);
                icon.Visible = false;
            }
        }
    }
}
