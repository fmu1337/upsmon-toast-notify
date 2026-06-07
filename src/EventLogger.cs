using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UpsmonEventMsg
{
    internal static class EventLogger
    {
        static readonly object Gate = new object();
        static string _logPath;

        public static string LogPath
        {
            get
            {
                if (_logPath != null) return _logPath;
                _logPath = Path.Combine(UpsmonPaths.DataRoot, "event-msg.log");
                return _logPath;
            }
        }

        public static void Log(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message;
            lock (Gate)
            {
                try { File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
        }
    }

    internal static class UpsmonPaths
    {
        static string _dataRoot;

        public static string DataRoot
        {
            get
            {
                if (_dataRoot != null) return _dataRoot;
                _dataRoot = ResolveDataRoot();
                return _dataRoot;
            }
        }

        public static string IniPath { get { return Path.Combine(DataRoot, "UPSMON.ini"); } }

        static string ResolveDataRoot()
        {
            foreach (string candidate in CandidateRoots())
            {
                if (Directory.Exists(candidate)) return candidate;
            }
            return @"C:\Users\Public\UPSMON-Pro";
        }

        static IEnumerable<string> CandidateRoots()
        {
            yield return ReadUserPathFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserPath.dat"));
            yield return ReadUserPathFile(@"C:\Program Files (x86)\UPSMONPRO\UserPath.dat");
            yield return Environment.GetEnvironmentVariable("UPSMON_DATA");
            yield return @"C:\Users\Public\UPSMON-Pro";
        }

        static string ReadUserPathFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path, Encoding.Default).Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text.TrimEnd('\\', '/');
            }
            catch { return null; }
        }
    }
}
