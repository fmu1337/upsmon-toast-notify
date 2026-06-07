using System;
using System.IO;
using System.Text;

namespace UpsmonEventMsg
{
    internal static class UpsmonEventPayload
    {
        public static string ReadRecentEventMessage()
        {
            string path = ResolveEventRecordPath();
            if (path == null || !File.Exists(path)) return null;

            try
            {
                var info = new FileInfo(path);
                if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalSeconds > 120) return null;

                string[] lines = File.ReadAllLines(path, Encoding.Default);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    int comma = line.LastIndexOf(',');
                    if (comma >= 0 && comma + 1 < line.Length)
                        return line.Substring(comma + 1).Trim();
                    return line;
                }
            }
            catch { }
            return null;
        }

        public static byte[] BuildFromRecentEventLog()
        {
            string message = ReadRecentEventMessage();
            if (string.IsNullOrWhiteSpace(message)) return null;
            return Encoding.Default.GetBytes(message);
        }

        static string ResolveEventRecordPath()
        {
            string root = UpsmonPaths.DataRoot;
            int month = DateTime.Now.Month;
            string nested = Path.Combine(root, month.ToString(), "EventRecord.CSV");
            if (File.Exists(nested)) return nested;
            return Path.Combine(root, "EventRecord.CSV");
        }
    }
}
