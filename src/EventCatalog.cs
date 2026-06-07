using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UpsmonEventMsg
{
    internal sealed class EventCatalog
    {
        readonly Dictionary<int, string> _byId = new Dictionary<int, string>();
        readonly Dictionary<byte, string> _byCode = new Dictionary<byte, string>();

        public static EventCatalog Load()
        {
            var cat = new EventCatalog();
            cat.LoadLanguageTable();
            return cat;
        }

        void LoadLanguageTable()
        {
            string lang = ReadLanguage();
            string table = ResolveLanguageFile(lang);
            if (!File.Exists(table))
                table = ResolveLanguageFile("English");

            if (!File.Exists(table))
            {
                SeedDefaults();
                return;
            }

            foreach (string line in File.ReadAllLines(table, Encoding.Default))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (!key.StartsWith("Event", StringComparison.OrdinalIgnoreCase)) continue;
                int num;
                if (!int.TryParse(key.Substring(5), out num)) continue;
                _byId[num] = val;
            }

            // Event index in UPSMON master maps 1:1 to Event2.. in English.txt for most cases.
            for (int i = 2; i <= 22; i++)
            {
                string text;
                if (_byId.TryGetValue(i, out text))
                    _byCode[(byte)i] = text;
            }

            if (!_byId.ContainsKey(1)) _byId[1] = "Event Description";
            if (!_byId.ContainsKey(0)) _byId[0] = "Event Date Time";
        }

        void SeedDefaults()
        {
            _byCode[2] = "Connection Restore";
            _byCode[3] = "Power Failure";
            _byCode[4] = "Power Restore";
            _byCode[5] = "UPS Self Test";
            _byCode[6] = "UPS Battery Low";
            _byCode[7] = "Battery Failed";
            _byCode[8] = "Overload";
            _byCode[9] = "UPS Failed";
            _byCode[10] = "UPS Shutdown";
            _byCode[11] = "UPS Shutdown";
            _byCode[12] = "UPS Started";
            _byCode[13] = "System Shutdown";
            _byCode[14] = "Battery Normal";
            _byCode[15] = "Battery Test Time";
            _byCode[16] = "UPS Bypass";
            _byCode[17] = "UPS Bypass Recover";
            _byCode[18] = "System Reboot";
            _byCode[19] = "Immediate Reboot";
            _byCode[20] = "Cancel Reboot";
            _byCode[21] = "Immediate Shutdown";
            _byCode[22] = "Cancel Shutdown";
        }

        string ReadLanguage()
        {
            var sb = new StringBuilder(64);
            NativeMethods.GetPrivateProfileString("Main", "DefLang", "English", sb, sb.Capacity, UpsmonPaths.IniPath);
            string lang = sb.ToString().Trim();
            if (lang.Length == 0) lang = "English";
            return lang;
        }

        static string ResolveLanguageFile(string lang)
        {
            string install = AppDomain.CurrentDomain.BaseDirectory;
            string[] names = { lang + ".txt", lang.ToLowerInvariant() + ".txt" };
            foreach (string name in names)
            {
                string p1 = Path.Combine(install, name);
                if (File.Exists(p1)) return p1;
                string p2 = Path.Combine(@"C:\Program Files (x86)\UPSMONPRO", name);
                if (File.Exists(p2)) return p2;
            }
            return Path.Combine(@"C:\Program Files (x86)\UPSMONPRO", "English.txt");
        }

        public string DescribeEvent(byte code)
        {
            string text;
            if (_byCode.TryGetValue(code, out text)) return text;
            return "UPSMON event " + code;
        }

        public string Title { get { return "UPSMON Pro"; } }
    }
}
