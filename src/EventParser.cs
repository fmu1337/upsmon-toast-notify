using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UpsmonEventMsg
{
    internal static class EventParser
    {
        public static string ParseText(EventCatalog catalog, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_COPYDATA && lParam != IntPtr.Zero)
            {
                string fromCopy = ParseCopyData(catalog, lParam);
                if (!string.IsNullOrWhiteSpace(fromCopy)) return fromCopy;
            }

            if (lParam != IntPtr.Zero)
            {
                string s1 = NativeMethods.ReadDelphiShortString(lParam, 256);
                if (s1.Length >= 2) return s1.Trim();

                string s2 = NativeMethods.ReadAnsiString(lParam, 256);
                if (s2.Length >= 2) return s2.Trim();
            }

            int code = (int)(wParam.ToInt64() & 0xFF);
            if (code >= 2 && code <= 22)
                return catalog.DescribeEvent((byte)code);

            if (lParam != IntPtr.Zero)
            {
                byte[] head = new byte[16];
                Marshal.Copy(lParam, head, 0, head.Length);
                if (head[0] == (byte)'P' && head[1] == (byte)'C' && head[2] == (byte)'M')
                    return DescribePcm(catalog, head[3], null);
            }

            return null;
        }

        static string ParseCopyData(EventCatalog catalog, IntPtr lParam)
        {
            var cds = (NativeMethods.CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(NativeMethods.CopyDataStruct));
            if (cds.CbData <= 0 || cds.LpData == IntPtr.Zero) return null;

            int len = Math.Min(cds.CbData, 512);
            byte[] bytes = new byte[len];
            Marshal.Copy(cds.LpData, bytes, 0, len);

            if (len >= 4 && bytes[0] == (byte)'P' && bytes[1] == (byte)'C' && bytes[2] == (byte)'M')
            {
                string extra = ExtractEmbeddedStrings(bytes, 4);
                return DescribePcm(catalog, bytes[3], extra);
            }

            string text = Encoding.Default.GetString(bytes).TrimEnd('\0').Trim();
            if (text.Length >= 1 && text.Length <= 2)
            {
                int value;
                if (int.TryParse(text, out value) && value >= 2 && value <= 22)
                    return catalog.DescribeEvent((byte)value);
            }
            if (text.Length >= 2) return text;
            return null;
        }

        static string DescribePcm(EventCatalog catalog, byte pcmCode, string extra)
        {
            byte eventIndex = PcmCodeToEventIndex(pcmCode);
            string body = catalog.DescribeEvent(eventIndex);
            if (!string.IsNullOrWhiteSpace(extra)) body += " — " + extra;
            return body;
        }

        static string ExtractEmbeddedStrings(byte[] bytes, int offset)
        {
            var parts = new StringBuilder();
            int i = offset;
            while (i < bytes.Length)
            {
                int len = bytes[i];
                if (len <= 0 || len > 120 || i + 1 + len > bytes.Length) break;
                string part = Encoding.Default.GetString(bytes, i + 1, len);
                if (part.Length > 0)
                {
                    if (parts.Length > 0) parts.Append(" | ");
                    parts.Append(part);
                }
                i += 1 + len;
            }
            return parts.ToString();
        }

        static byte PcmCodeToEventIndex(byte pcmCode)
        {
            switch (pcmCode)
            {
                case 1: return 3;
                case 2: return 4;
                case 3: return 5;
                case 4: return 6;
                case 5: return 7;
                case 6: return 17;
                case 7: return 19;
                case 8: return 13;
                case 9: return 14;
                case 10: return 9;
                case 11: return 8;
                case 12: return 2;
                case 13: return 2;
                default: return pcmCode;
            }
        }
    }
}
