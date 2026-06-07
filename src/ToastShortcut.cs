using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace UpsmonEventMsg
{
    internal static class ToastShortcut
    {
        public const string AppId = "Powercom.UPSMONPro.EventMsg";

        static readonly Guid ShellLinkClsid = new Guid("00021401-0000-0000-C000-000000000046");
        static readonly Guid PropertyStoreGuid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        static readonly PropertyKey AppUserModelIdKey = new PropertyKey(
            new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        public static void Ensure(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EventMsg.exe");

            try { SetCurrentProcessExplicitAppUserModelID(AppId); } catch { }

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "UPSMON Pro");
            Directory.CreateDirectory(dir);
            string lnk = Path.Combine(dir, "UPSMON Notifications.lnk");

            CreateShortcut(lnk, exePath);
            SetAppUserModelId(lnk, AppId);
        }

        static void CreateShortcut(string path, string target)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
            shortcut.GetType().InvokeMember(
                "TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcut.GetType().InvokeMember(
                "WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut,
                new object[] { Path.GetDirectoryName(target) });
            shortcut.GetType().InvokeMember(
                "Description", System.Reflection.BindingFlags.SetProperty, null, shortcut,
                new object[] { "UPSMON Pro notifications" });
            shortcut.GetType().InvokeMember(
                "Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }

        static void SetAppUserModelId(string shortcutPath, string appId)
        {
            IShellLinkW link = (IShellLinkW)new CShellLink();
            IPersistFile persist = (IPersistFile)link;
            persist.Load(shortcutPath, 2); // STGM_READWRITE

            IPropertyStore store = (IPropertyStore)link;
            PropVariant value = PropVariant.FromString(appId);
            PropertyKey key = AppUserModelIdKey;
            store.SetValue(ref key, ref value);
            store.Commit();
            persist.Save(shortcutPath, true);
            value.Clear();
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class CShellLink { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPath, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PropertyKey
        {
            public Guid FmtId;
            public uint Pid;

            public PropertyKey(Guid fmtId, uint pid)
            {
                FmtId = fmtId;
                Pid = pid;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PropVariant
        {
            [FieldOffset(0)] public ushort Vt;
            [FieldOffset(8)] public IntPtr Pointer;

            const ushort VtLpWSTR = 31;

            public static PropVariant FromString(string value)
            {
                var pv = new PropVariant { Vt = VtLpWSTR, Pointer = Marshal.StringToCoTaskMemUni(value) };
                return pv;
            }

            public void Clear()
            {
                PropVariantClear(ref this);
            }

            [DllImport("ole32.dll")]
            static extern int PropVariantClear(ref PropVariant pvar);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
        }
    }
}
