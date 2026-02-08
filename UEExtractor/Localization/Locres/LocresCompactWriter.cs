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
            // Группируем по Namespace, сохраняя порядок появления
            // Включаем ВСЕ записи, в том числе с пустым Namespace
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
                    keySectionSize += KeyStringSize(entry.Key); // key name
                    keySectionSize += 4;  // hash
                    keySectionSize += 4;  // string index
                }
            }

            // Header = 16 (magic) + 1 (version) + 8 (offset) = 25
            long stringTableOffset = 25 + keySectionSize;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                // === HEADER ===
                w.Write(LocresMagic);
                w.Write(VersionCompact);
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
                        WriteKeyString(w, entry.Key);
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
            => File.WriteAllBytes(path, Write(entries));

        public static void WriteToFile(string path, LocresResult[] entries)
            => File.WriteAllBytes(path, Write(entries.ToList()));
    }

}