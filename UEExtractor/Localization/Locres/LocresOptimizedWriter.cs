using CUE4Parse.Utils;
using CUE4Parse.UE4.Objects.Core.i18N;
using Solicen.Localization.UE4;
using System.Text;

namespace LocresWriter
{
    /// <summary>
    /// Writes .locres files in the "Optimized" formats, which store pre-hashed
    /// namespaces/keys and a reference-counted string LUT:
    ///
    ///   Optimized_CRC32             (v2) - namespace/key hashed with FCrc::StrCrc32.
    ///   Optimized_CityHash64_UTF16  (v3) - namespace/key hashed with CityHash64 over UTF-16.
    ///
    /// Unlike the Compact v1 writer, these versions store the namespace/key hash
    /// alongside the string, so the engine can resolve namespaced keys correctly.
    /// </summary>
    public static class LocresOptimizedWriter
    {
        private static readonly byte[] LocresMagic =
        {
            0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
            0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B
        };

        // ── FString helpers ──────────────────────────────────────────────

        private static void WriteKeyString(BinaryWriter w, string s)
        {
            if (s.Length == 0) { w.Write(0); return; }
            bool ascii = s.All(c => c < 128);
            if (ascii)
            {
                w.Write(s.Length + 1);
                w.Write(Encoding.ASCII.GetBytes(s));
                w.Write((byte)0);
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
            if (s.Length == 0) return 4;
            return s.All(c => c < 128) ? 4 + s.Length + 1 : 4 + s.Length * 2 + 2;
        }

        private static void WriteValueString(BinaryWriter w, string s)
        {
            if (s.Length == 0) { w.Write(0); return; }
            bool ascii = s.All(c => c < 128);
            if (ascii)
            {
                w.Write(s.Length + 1);
                w.Write(Encoding.ASCII.GetBytes(s));
                w.Write((byte)0);
            }
            else
            {
                w.Write(-(s.Length + 1));
                w.Write(Encoding.Unicode.GetBytes(s));
                w.Write((short)0);
            }
        }

        // ── Key hashing ──────────────────────────────────────────────────

        /// <summary>
        /// Replicates Unreal's uint64 -> uint32 hash fold (TypeHash.h).
        /// </summary>
        private static uint CityHash64ToUInt32(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            ulong h = CityHash.CityHash64(Encoding.Unicode.GetBytes(s));
            return (uint)h + ((uint)(h >> 32) * 23);
        }

        private static uint HashNamespace(ELocResVersion version, string ns)
            => version == ELocResVersion.Optimized_CityHash64_UTF16
                ? CityHash64ToUInt32(ns)
                : LocresSharp.Crc.StrCrc32(ns);

        private static uint HashKey(ELocResVersion version, string key)
            => version == ELocResVersion.Optimized_CityHash64_UTF16
                ? CityHash64ToUInt32(key)
                : LocresSharp.Crc.StrCrc32(key);

        // ── actualKey helper ─────────────────────────────────────────────

        private static string ActualKey(string ns, string compositeKey)
        {
            // Strip all leading occurrences of "ns::" to handle cases where
            // the CSV write/read cycle double-prefixes composite keys.
            string key = compositeKey;
            while (!string.IsNullOrEmpty(ns) && key.StartsWith(ns + "::", StringComparison.Ordinal))
                key = key[(ns.Length + 2)..];
            return key;
        }

        // ── Main write entry point ───────────────────────────────────────

        /// <summary>
        /// Serializes <paramref name="entries"/> in the requested Optimized locres format.
        /// Supported versions: <see cref="ELocResVersion.Optimized_CRC32"/> (v2) and
        /// <see cref="ELocResVersion.Optimized_CityHash64_UTF16"/> (v3).
        /// </summary>
        public static byte[] Write(
            List<LocresResult> entries,
            ELocResVersion version = ELocResVersion.Optimized_CRC32)
        {
            if (version < ELocResVersion.Optimized_CRC32 || version > ELocResVersion.Optimized_CityHash64_UTF16)
                throw new ArgumentOutOfRangeException(nameof(version),
                    $"Optimized writer only supports Optimized_CRC32 (v2) and Optimized_CityHash64_UTF16 (v3), got {version}.");

            int skipped = entries.Count(e => string.IsNullOrEmpty(e.Key));
            if (skipped > 0)
            {
                Solicen.CLI.Console.WriteLine($"[Yellow][Locres] Skipping {skipped} entries with empty key.");
                entries = entries.Where(e => !string.IsNullOrEmpty(e.Key)).ToList();
            }

            // Group by namespace preserving order
            var nsGroups = new List<(string ns, List<LocresResult> keys)>();
            var nsOrder  = new Dictionary<string, int>();
            foreach (var e in entries)
            {
                string ns = e.Namespace ?? string.Empty;
                if (!nsOrder.TryGetValue(ns, out int idx))
                {
                    idx = nsGroups.Count;
                    nsOrder[ns] = idx;
                    nsGroups.Add((ns, new List<LocresResult>()));
                }
                nsGroups[idx].keys.Add(e);
            }

            bool v3 = version == ELocResVersion.Optimized_CityHash64_UTF16;
            int totalStrings = entries.Count;

            // Pre-compute actualKeys (needed for size and write)
            var actualKeys = nsGroups.Select(g =>
                g.keys.Select(e => ActualKey(g.ns, e.Key)).ToList()).ToList();

            // ── keySectionSize ────────────────────────────────────────────
            // Optimized layout: EntriesCount(4) + NamespaceCount(4) +
            //   per namespace : nsHash(4) + FString(ns) + KeyCount(4)
            //   per key       : keyHash(4) + FString(key) + sourceHash(4) + strIdx(4)
            int keySectionSize = 4 + 4;
            for (int gi = 0; gi < nsGroups.Count; gi++)
            {
                var (ns, keys) = nsGroups[gi];
                keySectionSize += 4;             // ns Hash
                keySectionSize += KeyStringSize(ns);
                keySectionSize += 4;             // KeyCount
                for (int ki = 0; ki < keys.Count; ki++)
                {
                    keySectionSize += 4;         // key Hash
                    keySectionSize += KeyStringSize(actualKeys[gi][ki]);
                    keySectionSize += 4;         // sourceHash
                    keySectionSize += 4;         // strIdx
                }
            }

            // Header: magic(16) + ue_ver(1) + stringTableOffset(8) = 25
            const int headerSize = 25;
            long stringTableOffset = headerSize + keySectionSize;

            using var ms = new MemoryStream();
            using var w  = new BinaryWriter(ms, Encoding.UTF8);

            // === HEADER ===
            w.Write(LocresMagic);
            w.Write((byte)version);
            w.Write(stringTableOffset);

            // === KEY SECTION ===
            w.Write((uint)totalStrings);       // EntriesCount
            w.Write((uint)nsGroups.Count);     // NamespaceCount

            int strIdx = 0;
            for (int gi = 0; gi < nsGroups.Count; gi++)
            {
                var (ns, keys) = nsGroups[gi];

                // Prefer the game-preserved hash (v3) when available, otherwise compute.
                uint nsHash = v3 && keys.Count > 0 && keys[0].NsHash != 0
                    ? keys[0].NsHash
                    : HashNamespace(version, ns);
                w.Write(nsHash);
                WriteKeyString(w, ns);
                w.Write((uint)keys.Count);

                for (int ki = 0; ki < keys.Count; ki++)
                {
                    var entry = keys[ki];
                    var aKey  = actualKeys[gi][ki];

                    uint keyHash = v3 && entry.KeyHash != 0
                        ? entry.KeyHash
                        : HashKey(version, aKey);
                    w.Write(keyHash);
                    WriteKeyString(w, aKey);
                    w.Write(LocresSharp.Crc.StrCrc32((entry.Source ?? string.Empty).Unescape()));
                    w.Write(strIdx++);
                }
            }

            // Assert: actual key section size must match the pre-calculated offset
            w.Flush();
            long actualKSEnd = ms.Position;
            if (actualKSEnd != stringTableOffset)
                Solicen.CLI.Console.WriteLine(
                    $"[Red][BUG] keySectionSize mismatch! calculated stringTableOffset={stringTableOffset}, actual pos after key section={actualKSEnd} (diff={(actualKSEnd - stringTableOffset):+0;-0})");

            // === STRING TABLE (pointed to by stringTableOffset) ===
            w.Write((uint)totalStrings);
            for (int gi = 0; gi < nsGroups.Count; gi++)
            {
                foreach (var entry in nsGroups[gi].keys)
                {
                    string value = string.IsNullOrEmpty(entry.Translation)
                        ? entry.Source ?? string.Empty
                        : entry.Translation;
                    WriteValueString(w, value.Unescape());
                    w.Write(1); // RefCount = 1
                }
            }

            w.Flush();
            return ms.ToArray();
        }

        public static byte[] Write(LocresResult[] entries, ELocResVersion version = ELocResVersion.Optimized_CRC32)
            => Write(entries.ToList(), version);

        public static void WriteToFile(
            string path,
            List<LocresResult> entries,
            ELocResVersion version = ELocResVersion.Optimized_CRC32)
        {
            File.WriteAllBytes(path, Write(entries, version));
        }

        public static void WriteToFile(
            string path,
            LocresResult[] entries,
            ELocResVersion version = ELocResVersion.Optimized_CRC32)
            => WriteToFile(path, entries.ToList(), version);
    }
}
