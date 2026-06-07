using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>
    /// Windows 10/11 Action Center toast (via PowerShell WinRT — reliable on .NET 4.x).
    /// </summary>
    internal static class ToastNotifier
    {
        const string AppId = "Powercom.UPSMONPro.EventMsg";

        public static void Show(string title, string body, bool isCritical = false)
        {
            try
            {
                EnsureShortcut(AppId);
                string script = ResolveToastScript();
                if (string.IsNullOrEmpty(script) || !File.Exists(script))
                {
                    FallbackBalloon(title, body);
                    return;
                }

                string args = string.Format(
                    "-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -Title \"{1}\" -Body \"{2}\" -AppId \"{3}\"",
                    script,
                    EscapeArg(title),
                    EscapeArg(body),
                    AppId);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                EventLogger.Log("Toast error: " + ex.Message);
                FallbackBalloon(title, body);
            }
        }

        static string ResolveToastScript()
        {
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "Show-Toast.ps1"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Show-Toast.ps1")
            };
            foreach (string path in candidates)
                if (File.Exists(path)) return path;
            return candidates[0];
        }

        static string EscapeArg(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\"", "`\"");
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
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
            shortcut.GetType().InvokeMember(
                "TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcut.GetType().InvokeMember(
                "Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { description });
            shortcut.GetType().InvokeMember(
                "Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
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
