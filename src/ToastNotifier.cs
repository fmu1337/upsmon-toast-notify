using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// Windows 10/11 Action Center toast via WinRT (no PowerShell).
    /// </summary>
    internal static class ToastNotifier
    {
        const string AppId = "Powercom.UPSMONPro.EventMsg";
        static bool _bootstrapped;

        public static void Show(string title, string body, bool isCritical = false)
        {
            try
            {
                EnsureShortcut(AppId);
                ShowWinRtToast(title, body, AppId);
            }
            catch (Exception ex)
            {
                EventLogger.Log("Toast error: " + ex.Message);
                FallbackBalloon(title, body);
            }
        }

        static void BootstrapWinRt()
        {
            if (_bootstrapped) return;
            _bootstrapped = true;

            string winRt = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll");
            if (File.Exists(winRt))
                Assembly.LoadFrom(winRt);

            // Prime WinRT type loader (same types PowerShell loads).
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

            string xml = BuildToastXml(title, body);
            object doc = Activator.CreateInstance(docType);
            docType.GetMethod("LoadXml", new[] { typeof(string) }).Invoke(doc, new object[] { xml });

            object toast = Activator.CreateInstance(toastType, doc);
            object notifier = managerType
                .GetMethod("CreateToastNotifier", new[] { typeof(string) })
                .Invoke(null, new object[] { appId });
            notifier.GetType().GetMethod("Show").Invoke(notifier, new[] { toast });
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
                .AppendLine("<toast activationType=\"foreground\" duration=\"long\">")
                .AppendLine("  <audio silent=\"true\"/>")
                .AppendLine("  <visual><binding template=\"ToastGeneric\">")
                .AppendLine("    <text>" + escapedTitle + "</text>")
                .AppendLine("    <text>" + escapedBody + "</text>")
                .AppendLine("  </binding></visual>")
                .AppendLine("</toast>")
                .ToString();
        }

        static void EnsureShortcut(string appId)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "UPSMON Pro");
            Directory.CreateDirectory(dir);
            string lnk = Path.Combine(dir, "UPSMON Notifications.lnk");
            if (File.Exists(lnk)) return;

            string exe = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(exe))
                exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EventMsg.exe");
            CreateShortcut(lnk, exe, "UPSMON Pro notifications");
        }

        static void CreateShortcut(string path, string target, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            shortcut.GetType().InvokeMember(
                "TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcut.GetType().InvokeMember(
                "Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            shortcut.GetType().InvokeMember(
                "Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        static void FallbackBalloon(string title, string body)
        {
            var thread = new System.Threading.Thread(() =>
            {
                using (var icon = new System.Windows.Forms.NotifyIcon())
                {
                    icon.Icon = System.Drawing.SystemIcons.Information;
                    icon.Visible = true;
                    icon.ShowBalloonTip(8000, title, body, System.Windows.Forms.ToolTipIcon.Info);
                    System.Threading.Thread.Sleep(9000);
                    icon.Visible = false;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
