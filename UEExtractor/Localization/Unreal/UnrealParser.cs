using System.Text;

namespace Solicen.Localization.UE4
{
    /// <summary>
    /// Unified FString parser for .uexp/.uasset data.
    /// Replaces the slower UnrealUepx and UnrealUasset parsers with a single
    /// allocation-free scan over the raw byte array.
    /// <para>
    /// Two FString layouts are recognized (both may occur in the same asset):
    /// <list type="bullet">
    /// <item>Format A (String → Hash): <c>01 1F &lt;text&gt; 00 1F &lt;32 hex chars&gt; 00</c></item>
    /// <item>Format B (Hash → String): <c>21 00 00 00 &lt;32 hex chars&gt; 00 &lt;int32 len&gt; &lt;text&gt; 00</c>,
    /// where <c>21 00 00 00</c> is the int32 length prefix 33 (32 hash chars + \0)
    /// and <c>len</c> is the string length INCLUDING the trailing \0.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Strings are UTF8 by default; Latin1-encoded strings (starting with a
    /// 0x00 marker byte and containing encoded smart punctuation) are also
    /// supported — see <see cref="GetString"/>.
    /// </para>
    /// </summary>
    internal class UnrealParser
    {
        private const int HashLength = 32;
        private const int MaxStringLength = 10000;

        /// <summary>
        /// Lookup table of valid hash characters: 0-9, A-F, a-f.
        /// Indexed by byte value for O(1) hot-path checks.
        /// </summary>
        private static readonly bool[] HexCharTable = new bool[256];

        static UnrealParser()
        {
            foreach (var b in Enumerable.Range(48, 10).Concat(Enumerable.Range(65, 6)).Concat(Enumerable.Range(97, 6)))
                HexCharTable[b] = true;
        }

        /// <summary>
        /// Scans the raw asset data and extracts all Hash/String pairs
        /// in both formats (A: String → Hash, B: Hash → String).
        /// Results are deduplicated by hash.
        /// </summary>
        /// <param name="rawData">Full byte content of a .uexp or .uasset file.</param>
        /// <returns>List of unique <see cref="LocresResult"/> entries (Key = hash, Source = string).</returns>
        public static List<LocresResult> Parse(byte[] rawData)
        {
            var results = new List<LocresResult>();
            var seenKeys = new HashSet<string>();
            int formatA = 0, formatB = 0;

            int i = 0;
            while (i < rawData.Length - 2)
            {
                if (rawData[i] == 0x21 && rawData[i + 1] == 0x00 && rawData[i + 2] == 0x00 && rawData[i + 3] == 0x00)
                {
                    // Format B: Hash → String
                    int next = TryParseHashThenString(rawData, i, results, seenKeys);
                    if (next > i) { formatB++; i = next; continue; }
                }
                else if (rawData[i] == 0x01 && rawData[i + 1] == 0x1F)
                {
                    // Format A: String → Hash
                    int next = TryParseStringThenHash(rawData, i, results, seenKeys);
                    if (next > i) { formatA++; i = next; continue; }
                }
                i++;
            }

            return results;
        }

        /// <summary>
        /// Parses Format A (String → Hash): <c>01 1F &lt;text&gt; 00 1F &lt;32 hex&gt; 00</c>.
        /// <para>
        /// The string length is derived as the distance to the nearest 0x00
        /// terminator (via <see cref="Array.IndexOf"/>, allocation-free).
        /// Latin1 variant: a leading 0x00 right after the 1F separator is a
        /// encoding marker, not a terminator: <c>01 1F 00 &lt;latin1 text&gt; 00 1F &lt;hash&gt; 00</c>.
        /// </para>
        /// </summary>
        /// <param name="data">Raw asset bytes.</param>
        /// <param name="i">Current scan offset, pointing at the <c>01 1F</c> sequence.</param>
        /// <param name="results">Output list to append successfully parsed entries to.</param>
        /// <param name="seenKeys">Hash deduplication set.</param>
        /// <returns>
        /// The offset to resume scanning from (end of the parsed record),
        /// or -1 if the data at this offset does not form a valid record.
        /// </returns>
        private static int TryParseStringThenHash(byte[] data, int i, List<LocresResult> results, HashSet<string> seenKeys)
        {
            int strStart = i + 2;
            if (strStart >= data.Length) return -1;

            // A leading 00 after 1F is a Latin1 encoding marker, not a terminator
            bool latin1 = data[strStart] == 0x00;
            int searchFrom = latin1 ? strStart + 1 : strStart;

            int zero = Array.IndexOf(data, (byte)0x00, searchFrom, data.Length - searchFrom);
            if (zero < 0 || zero + 1 >= data.Length) return -1;

            // The string must be immediately followed by the 1F separator, then the hash
            if (data[zero + 1] != 0x1F) return -1;

            int hashStart = zero + 2;
            if (hashStart + HashLength >= data.Length) return -1;
            if (data[hashStart + HashLength] != 0x00) return -1;

            // Trim garbage prefix: keep only the text after the last 01 1F pair before the terminator
            int contentStart = strStart;
            for (int s = zero - 2; s >= strStart; s--)
            {
                if (data[s] == 0x01 && data[s + 1] == 0x1F)
                {
                    contentStart = s + 2;
                    while (contentStart < zero && data[contentStart] == 0x1F) contentStart++;
                    break;
                }
            }

            int length = zero - contentStart;
            if (length <= 0 || length >= MaxStringLength) return -1;

            string hash = Encoding.UTF8.GetString(data, hashStart, HashLength);
            if (!IsValidHash(hash)) return -1;

            // GetString distinguishes UTF8 and Latin1 by the 0x00 marker inside the content
            string source = GetString(data, contentStart, length).Escape();

            AddResult(results, seenKeys, hash, source);
            return hashStart + HashLength + 1;
        }

        /// <summary>
        /// Parses Format B (Hash → String): <c>21 00 00 00 &lt;32 hex&gt; 00 &lt;int32 len&gt; &lt;text&gt; 00</c>.
        /// <para>
        /// The leading <c>21 00 00 00</c> is the int32 length prefix 33 of the
        /// hash FString (32 hex chars + \0). The trailing int32 is the string
        /// length INCLUDING its \0 terminator; validation requires the \0 to
        /// lie exactly at the end of the declared span and forbids interior
        /// zero bytes (garbage filter).
        /// </para>
        /// </summary>
        /// <param name="data">Raw asset bytes.</param>
        /// <param name="i">Current scan offset, pointing at the <c>21 00 00 00</c> sequence.</param>
        /// <param name="results">Output list to append successfully parsed entries to.</param>
        /// <param name="seenKeys">Hash deduplication set.</param>
        /// <returns>
        /// The offset to resume scanning from (end of the parsed record),
        /// or -1 if the data at this offset does not form a valid record.
        /// </returns>
        private static int TryParseHashThenString(byte[] data, int i, List<LocresResult> results, HashSet<string> seenKeys)
        {
            int hashStart = i + 4; // 21 00 00 00 = int32 33 (32 hash chars + \0)
            if (hashStart + HashLength + 5 > data.Length) return -1;

            // Hash: hex characters only, followed by a 00 terminator
            for (int c = 0; c < HashLength; c++)
                if (!HexCharTable[data[hashStart + c]]) return -1;
            if (data[hashStart + HashLength] != 0x00) return -1;

            int lenPos = hashStart + HashLength + 1;
            int strLen = BitConverter.ToInt32(data, lenPos); // string length including the trailing \0
            if (strLen <= 1 || strLen >= MaxStringLength) return -1;

            int strStart = lenPos + 4;
            int nullPos = strStart + strLen - 1;
            if (nullPos >= data.Length || data[nullPos] != 0x00) return -1;

            int contentLen = strLen - 1;
            // Garbage filter: interior zero bytes mean this is not a valid string payload
            for (int c = 0; c < contentLen; c++)
                if (data[strStart + c] == 0x00) return -1;

            string hash = Encoding.UTF8.GetString(data, hashStart, HashLength);
            if (!IsValidHash(hash)) return -1;

            string source = GetString(data, strStart, contentLen).Escape();

            AddResult(results, seenKeys, hash, source);
            return nullPos + 1;
        }

        /// <summary>
        /// Appends a parsed entry to the results, filtering out empty and
        /// single-character strings and duplicate hashes.
        /// </summary>
        /// <returns>True if the entry was added; otherwise false.</returns>
        private static bool AddResult(List<LocresResult> results, HashSet<string> seenKeys, string hash, string source)
        {
            if (source.Length == 0 || source.Length == 1) return false;
            if (!seenKeys.Add(hash)) return false;
            results.Add(new LocresResult(hash, source));
            return true;
        }

        /// <summary>
        /// Decodes a string payload, auto-detecting the encoding:
        /// if the content contains a 0x00 byte (the leading encoding marker),
        /// it is Latin1 with encoded smart punctuation; otherwise plain UTF8.
        /// Ported from UnrealUasset.GetString.
        /// </summary>
        private static string GetString(byte[] source, int index, int count)
        {
            if (count <= 0) return string.Empty;
            if (BytesContainZero(source, index, count))
            {
                var str = Encoding.Latin1.GetString(source, index, count).Remove(0, 1);
                return Latin1DecodeFix(str);
            }
            return Encoding.UTF8.GetString(source, index, count);
        }

        /// <summary>
        /// Checks whether the byte span contains any 0x00 byte.
        /// Used to detect Latin1-encoded strings (which carry a 0x00 marker).
        /// </summary>
        private static bool BytesContainZero(byte[] data, int index, int count)
        {
            int end = index + count;
            for (int i = index; i < end; i++)
                if (data[i] == 0) return true;
            return false;
        }

        /// <summary>
        /// Repairs Latin1-decoded smart punctuation: special characters are
        /// stored as two-byte sequences (control byte + 0x20) and are restored
        /// to their proper Unicode characters here.
        /// Ported from UnrealUasset.Latin1DecodeFix.
        /// </summary>
        private static string Latin1DecodeFix(string str)
        {
            var specialReplacements = new Dictionary<string, char>
            {
                { "\u0019\u0020", '’' },    // RIGHT SINGLE QUOTATION MARK
                { "\u0018\u0020", '‘' },    // LEFT SINGLE QUOTATION MARK
                { "\u001C\u0020", '“' },    // LEFT DOUBLE QUOTATION MARK
                { "\u001D\u0020", '”' },    // RIGHT DOUBLE QUOTATION MARK
                { "\u0026\u0020", '…' },    // ELLIPSIS
                { "\u0014\u0020", '—' },    // EM DASH
                { "\u0013\u0020", '–' },    // EN DASH
            };
            foreach (var rep in specialReplacements)
            {
                str = str.Replace(rep.Key, $"{rep.Value}");
            }
            return str.Replace("\0", "");
        }

        /// <summary>
        /// Validates a candidate hash string: must be exactly 32 characters,
        /// contain only hex digits (per <see cref="StringExtensions.IsGUID"/>),
        /// not be all digits and not be a single repeated character.
        /// </summary>
        /// <returns>True if the string looks like a valid 32-char hex hash.</returns>
        static bool IsValidHash(string hash)
        {
            if (hash.Length != HashLength  // Length must be exactly 32 characters
                || hash.IsAllNumber()      // Reject strings made of digits only
                || hash.IsAllSame()        // Reject strings like DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD
                || !hash.IsGUID()          // Only characters allowed in a hash
                ) return false;
            return true;
        }
    }
}
