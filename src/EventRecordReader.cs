using System;
using System.IO;
using System.Text;

namespace UpsmonEventMsg
{
    /// <summary>Same event text stock EventMsg reads from EventRecord.CSV.</summary>
    internal static class EventRecordReader
    {
        public static string ReadPendingMessage()
        {
            string path = ResolvePath();
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

        static string ResolvePath()
        {
            string root = UpsmonPaths.DataRoot;
            string nested = Path.Combine(root, DateTime.Now.Month.ToString(), "EventRecord.CSV");
            if (File.Exists(nested)) return nested;
            return Path.Combine(root, "EventRecord.CSV");
        }
    }
}
