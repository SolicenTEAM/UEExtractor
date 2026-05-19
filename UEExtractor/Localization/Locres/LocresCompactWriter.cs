using Solicen.Localization.UE4;
using System.Text;

namespace LocresWriter
{
    public static class LocresCompactWriter
    {
        private static readonly byte[] LocresMagic =
        {
            0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
            0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B
        };

        private const byte VersionCompact = 0x01;

        // When true, writes the NTE (Neverness to Everness) locres header:
        // magic(16) + ue_version(1) + nte_version_int32(4) + offset_int64(8) = 29 bytes.
        // nte_version=1 means strings are NOT encrypted (< 10000 threshold).
        public static bool NTEFormat = false;
        /// <summary>
        /// Секция ключей: записывает строку.
        /// Пустая строка: Int32(0) и больше ничего.
        /// Непустая: Int32(strlen+1) + ASCII bytes + 0x00
        /// Для Unicode: отрицательная длина + UTF-16LE + 0x00 0x00
        /// </summary>
        private static void WriteKeyString(BinaryWriter w, string s)
        {
            if (s.Length == 0)
            {
                w.Write((int)0);
                return;
            }

            bool ascii = s.All(c => c < 128);
            if (ascii)
            {
                w.Write((int)(s.Length + 1));
                w.Write(Encoding.ASCII.GetBytes(s));
                w.Write((byte)0x00);
            }
            else
            {
                w.Write(-(s.Length + 1));
                w.Write(Encoding.Unicode.GetBytes(s));
                w.Write((short)0);
            }
        }

        private static int KeyStringSize(string s)
        {
            if (s.Length == 0)
                return 4; // только Int32(0)

            bool ascii = s.All(c => c < 128);
            return ascii
                ? 4 + s.Length + 1
                : 4 + s.Length * 2 + 2;
        }

        /// <summary>
        /// Строковая таблица: Int32(strlen) + bytes + 0x00
        /// Длина НЕ включает нулевой терминатор
        /// </summary>
        private static void WriteValueString(BinaryWriter w, string s)
        {
            if (s.Length == 0)
            {
                w.Write((int)0);
                return;
            }

            bool ascii = s.All(c => c < 128);
            if (ascii)
            {
                w.Write((int)s.Length+1);
                w.Write(Encoding.ASCII.GetBytes(s));
                w.Write((byte)0x00);
            }
            else
            {
                w.Write(-s.Length-1);
                w.Write(Encoding.Unicode.GetBytes(s));
                w.Write((short)0);
            }
        }

        public static byte[] Write(List<LocresResult> entries)
        {
            // Filter out entries with empty key to avoid "invalid localized string index" warnings
            int skipped = entries.Count(e => string.IsNullOrEmpty(e.Key));
            if (skipped > 0)
            {
                Solicen.CLI.Console.WriteLine($"[Yellow][Locres] Skipping {skipped} entries with empty key.");
                entries = entries.Where(e => !string.IsNullOrEmpty(e.Key)).ToList();
            }

            var nsGroups = new List<(string ns, List<LocresResult> keys)>();
            var nsOrder = new Dictionary<string, int>();

            foreach (var e in entries)
            {
                if (!nsOrder.TryGetValue(e.Namespace, out int idx))
                {
                    idx = nsGroups.Count;
                    nsOrder[e.Namespace] = idx;
                    nsGroups.Add((e.Namespace, new List<LocresResult>()));
                }
                nsGroups[idx].keys.Add(e);
            }

            // Общее количество строк
            int totalStrings = entries.Count;

            // Вычисляем размер секции ключей
            int keySectionSize = 4; // NamespaceCount
            foreach (var (ns, keys) in nsGroups)
            {
                keySectionSize += KeyStringSize(ns);  // namespace name
                keySectionSize += 4;                   // KeyCount

                foreach (var entry in keys)
                {
                    var actualKeySz = !string.IsNullOrEmpty(ns) &&
                                      entry.Key.StartsWith(ns + "::", StringComparison.Ordinal)
                        ? entry.Key[(ns.Length + 2)..]
                        : entry.Key;
                    keySectionSize += KeyStringSize(actualKeySz); // key name
                    keySectionSize += 4;  // hash
                    keySectionSize += 4;  // string index
                }
            }

            // Header size:
            //   Standard : 16 (magic) + 1 (version) + 8 (offset)           = 25 bytes
            //   NTE      : 16 (magic) + 1 (version) + 4 (nte_ver) + 8 (offset) = 29 bytes
            int headerSize = NTEFormat ? 29 : 25;
            long stringTableOffset = headerSize + keySectionSize;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                // === HEADER ===
                w.Write(LocresMagic);
                w.Write(VersionCompact);
                if (NTEFormat)
                    w.Write((int)1); // nte_version=1: unencrypted (< 10000 threshold)
                w.Write(stringTableOffset);

                // === СЕКЦИЯ КЛЮЧЕЙ ===
                w.Write((int)nsGroups.Count);

                int strIdx = 0;
                foreach (var (ns, keys) in nsGroups)
                {
                    WriteKeyString(w, ns);
                    w.Write((int)keys.Count);

                    foreach (var entry in keys)
                    {
                        // Strip namespace prefix from compositeKey to recover the actual locres key.
                        // e.g. "NS::NS::Key" with Namespace="NS" → actual key = "NS::Key"
                        var actualKey = !string.IsNullOrEmpty(entry.Namespace) &&
                                        entry.Key.StartsWith(entry.Namespace + "::", StringComparison.Ordinal)
                            ? entry.Key[(entry.Namespace.Length + 2)..]
                            : entry.Key;
                        WriteKeyString(w, actualKey);
                        w.Write(LocresSharp.Crc.StrCrc32(LocresHelper.UnEscapeKey(entry.Source)));
                        w.Write(strIdx);
                        strIdx++;
                    }
                }

                // === СЕКЦИЯ СТРОК ===
                w.Write((uint)totalStrings);

                foreach (var (ns, keys) in nsGroups)
                {
                    foreach (var entry in keys)
                    {
                        string value = string.IsNullOrEmpty(entry.Translation)
                            ? entry.Source
                            : entry.Translation;

                        WriteValueString(w, LocresHelper.UnEscapeKey(value));
                    }
                }

                w.Flush();
                return ms.ToArray();
            }
        }

        public static void WriteToFile(string path, List<LocresResult> entries)
        {
            var data = Write(entries);
            File.WriteAllBytes(path, data);
            PrintVerification(data);
        }

        public static void WriteToFile(string path, LocresResult[] entries)
            => WriteToFile(path, entries.ToList());

        // Reads back the written binary and prints a verification summary.
        private static void PrintVerification(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var r = new BinaryReader(ms);

                r.ReadBytes(16); // magic
                r.ReadByte();    // version
                if (NTEFormat) r.ReadInt32(); // nte_version
                r.ReadInt64();   // string table offset

                int nsCount = r.ReadInt32();
                int totalEntries = 0;
                var keyIndex = new List<(string Ns, string Key, int StrIdx)>();

                for (int i = 0; i < nsCount; i++)
                {
                    var ns = ReadKeyString(r);
                    int keyCount = r.ReadInt32();
                    totalEntries += keyCount;
                    for (int j = 0; j < keyCount; j++)
                    {
                        var key = ReadKeyString(r);
                        r.ReadInt32(); // hash
                        int strIdx = r.ReadInt32();
                        keyIndex.Add((ns, key, strIdx));
                    }
                }

                uint strCount = r.ReadUInt32();
                var strings = new List<string>((int)strCount);
                for (int i = 0; i < (int)strCount; i++)
                    strings.Add(ReadValueString(r));

                Solicen.CLI.Console.WriteLine($"[Green]Locres verified: {nsCount} namespace(s), {totalEntries} entr{(totalEntries == 1 ? "y" : "ies")}, {strings.Count(s => s.Length > 0)} non-empty string(s).");

                // Print first 30 entries as sanity check
                int shown = Math.Min(30, keyIndex.Count);
                for (int i = 0; i < shown; i++)
                {
                    var (ns, key, idx) = keyIndex[i];
                    var val = idx < strings.Count ? strings[idx] : "?";
                    Solicen.CLI.Console.WriteLine($"[DarkGray]  [{ns}] {key} = {(val.Length > 60 ? val[..60] + "…" : val)}");
                }
            }
            catch (Exception ex)
            {
                Solicen.CLI.Console.WriteLine($"[Red][Verify] Failed to read back locres: {ex.Message}");
            }
        }

        private static string ReadKeyString(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len == 0) return string.Empty;
            if (len > 0)
            {
                var bytes = r.ReadBytes(len - 1);
                r.ReadByte(); // null terminator
                return Encoding.ASCII.GetString(bytes);
            }
            else
            {
                int charCount = (-len) - 1;
                var bytes = r.ReadBytes(charCount * 2);
                r.ReadInt16(); // null terminator
                return Encoding.Unicode.GetString(bytes);
            }
        }

        private static string ReadValueString(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len == 0) return string.Empty;
            if (len > 0)
            {
                var bytes = r.ReadBytes(len - 1);
                r.ReadByte();
                return Encoding.ASCII.GetString(bytes);
            }
            else
            {
                int charCount = (-len) - 1;
                var bytes = r.ReadBytes(charCount * 2);
                r.ReadInt16();
                return Encoding.Unicode.GetString(bytes);
            }
        }
    }

}