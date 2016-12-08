using System.Collections.Generic;
using System.Text;

namespace MetafileDifference
{
    public static class ByteArrayExtensions
    {
        private static readonly char[] HexDigits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        public static string ToHexString(this IEnumerable<byte> bytes)
        {
            var sb = new StringBuilder();

            foreach (var b in bytes)
                sb.Append(HexDigits[b >> 4]).Append(HexDigits[b & 0xF]);

            return sb.ToString();
        }
    }
}
