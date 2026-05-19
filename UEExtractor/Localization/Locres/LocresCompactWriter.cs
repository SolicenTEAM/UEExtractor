using Solicen.Localization.UE4;
using System.Security.Cryptography;
using System.Text;
using CUE4Parse.Utils;

namespace LocresWriter
{
    public static class LocresCompactWriter
    {
        private static readonly byte[] LocresMagic =
        {
            0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
            0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B
        };

        private const byte VersionCompact          = 0x01;
        private const byte VersionCityHash64       = 0x03;

        // NTE AES-256-ECB key (from CUE4Parse NTE handler)
        private static readonly byte[] NTEKey = Convert.FromHexString(
            "396d4330686f704b4e6a5377694364684e56375974435765754476484c513238");

        /// <summary>
        /// Standard Compact v1 (default).
        /// Set NTEFormat=true for NTE unencrypted (v1, nte_ver=1).
        /// Set NTEEncrypted=true (implies NTEFormat) for NTE encrypted v3 (nte_ver=10100).
        /// </summary>
        public static bool NTEFormat    = false;
        public static bool NTEEncrypted = false;

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

        // ── NTE encryption ───────────────────────────────────────────────

        // Encrypts a plain string the same way NTE's game writes it:
        //   UTF-8(plain + "HottaLocresSplit") → zero-pad to 16 → AES-256-ECB → Base64-URL-safe
        private static string EncryptNTEString(string plain)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plain + "HottaLocresSplit");
            int padded = ((plainBytes.Length + 15) / 16) * 16;
            var buf = new byte[padded];
            plainBytes.CopyTo(buf, 0);

            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode    = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            var enc = aes.CreateEncryptor(NTEKey, null).TransformFinalBlock(buf, 0, buf.Length);
            return Convert.ToBase64String(enc).Replace('+', '-').Replace('/', '_');
        }

        // ── CityHash64 helper for v3 key hashes ──────────────────────────

        private static uint KeyHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return (uint)CityHash.CityHash64(Encoding.Unicode.GetBytes(s));
        }

        // ── actualKey helper ─────────────────────────────────────────────

        private static string ActualKey(string ns, string compositeKey)
            => !string.IsNullOrEmpty(ns) && compositeKey.StartsWith(ns + "::", StringComparison.Ordinal)
               ? compositeKey[(ns.Length + 2)..]
               : compositeKey;

        // ── Main write entry point ───────────────────────────────────────

        public static byte[] Write(List<LocresResult> entries)
        {
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
                if (!nsOrder.TryGetValue(e.Namespace, out int idx))
                {
                    idx = nsGroups.Count;
                    nsOrder[e.Namespace] = idx;
                    nsGroups.Add((e.Namespace, new List<LocresResult>()));
                }
                nsGroups[idx].keys.Add(e);
            }

            int totalStrings = entries.Count;
            bool v3          = NTEEncrypted; // v3 key format with StrHash + EntriesCount
            bool encrypted   = NTEEncrypted;

            // Pre-compute actualKeys (needed for size and write)
            var actualKeys = nsGroups.Select(g =>
                g.keys.Select(e => ActualKey(g.ns, e.Key)).ToList()).ToList();

            // ── keySectionSize ────────────────────────────────────────────
            int keySectionSize = 0;
            if (v3) keySectionSize += 4; // EntriesCount
            keySectionSize += 4;          // NamespaceCount
            for (int gi = 0; gi < nsGroups.Count; gi++)
            {
                var (ns, keys) = nsGroups[gi];
                if (v3) keySectionSize += 4;  // ns StrHash
                keySectionSize += KeyStringSize(ns);
                keySectionSize += 4; // KeyCount
                for (int ki = 0; ki < keys.Count; ki++)
                {
                    if (v3) keySectionSize += 4;  // key StrHash
                    keySectionSize += KeyStringSize(actualKeys[gi][ki]);
                    keySectionSize += 4; // sourceHash
                    keySectionSize += 4; // strIdx
                }
            }

            // ── Header size ───────────────────────────────────────────────
            // Standard   : magic(16) + ue_ver(1) + offset(8)                    = 25
            // NTE no-enc : magic(16) + ue_ver(1) + nte_ver(4) + offset(8)       = 29
            // NTE enc    : magic(16) + ue_ver(1) + nte_ver(4) + int32(4) + offset(8) = 33
            // NOTE: UE4 serializes bool as int32 (4 bytes), not 1 byte.
            int headerSize = encrypted ? 33 : (NTEFormat ? 29 : 25);
            long stringTableOffset = headerSize + keySectionSize;

            using var ms = new MemoryStream();
            using var w  = new BinaryWriter(ms, Encoding.UTF8);

            // === HEADER ===
            w.Write(LocresMagic);
            w.Write(encrypted ? VersionCityHash64 : VersionCompact);
            if (NTEFormat || encrypted)
                w.Write(encrypted ? 10100 : 1); // NTE version int32
            if (encrypted)
                w.Write((int)1); // isEncrypted = true (UE4 serializes bool as int32, 4 bytes)
            w.Write(stringTableOffset);

            // === KEY SECTION ===
            if (v3) w.Write((uint)totalStrings); // EntriesCount
            w.Write((uint)nsGroups.Count);

            int strIdx = 0;
            for (int gi = 0; gi < nsGroups.Count; gi++)
            {
                var (ns, keys) = nsGroups[gi];
                if (v3) w.Write(KeyHash(ns));
                WriteKeyString(w, ns);
                w.Write((uint)keys.Count);

                for (int ki = 0; ki < keys.Count; ki++)
                {
                    var entry  = keys[ki];
                    var aKey   = actualKeys[gi][ki];
                    if (v3) w.Write(KeyHash(aKey));
                    WriteKeyString(w, aKey);
                    w.Write(LocresSharp.Crc.StrCrc32(LocresHelper.UnEscapeKey(entry.Source)));
                    w.Write(strIdx++);
                }
            }

            // Assert: actual key section size must match the pre-calculated offset
            w.Flush();
            long actualKSEnd = ms.Position;
            if (actualKSEnd != stringTableOffset)
                Solicen.CLI.Console.WriteLine(
                    $"[Red][BUG] keySectionSize mismatch! calculated stringTableOffset={stringTableOffset}, actual pos after key section={actualKSEnd} (diff={(actualKSEnd - stringTableOffset):+0;-0})");
            else
                Solicen.CLI.Console.WriteLine($"[DarkGray][Locres] keySectionSize OK ({keySectionSize} bytes), offset={stringTableOffset}");

            // === STRING TABLE ===
            w.Write((uint)totalStrings);
            for (int gi = 0; gi < nsGroups.Count; gi++)
            {
                foreach (var entry in nsGroups[gi].keys)
                {
                    string value = string.IsNullOrEmpty(entry.Translation)
                        ? entry.Source : entry.Translation;
                    string plain = LocresHelper.UnEscapeKey(value);

                    if (encrypted)
                    {
                        WriteValueString(w, EncryptNTEString(plain));
                        w.Write(-1); // RefCount = -1 (no ref tracking)
                    }
                    else
                    {
                        WriteValueString(w, plain);
                    }
                }
            }

            w.Flush();
            return ms.ToArray();
        }

        public static void WriteToFile(string path, List<LocresResult> entries)
        {
            var data = Write(entries);
            File.WriteAllBytes(path, data);
            PrintVerification(data);
        }

        public static void WriteToFile(string path, LocresResult[] entries)
            => WriteToFile(path, entries.ToList());

        // ── Self-verification ─────────────────────────────────────────────

        private static void PrintVerification(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var r  = new BinaryReader(ms);

                r.ReadBytes(16); // magic
                byte ueVer = r.ReadByte();
                bool isV3  = ueVer >= 3;
                if (NTEFormat || NTEEncrypted)
                {
                    int nteVer = r.ReadInt32();
                    if (nteVer >= 10100) r.ReadInt32(); // isEncrypted (UE4 bool = int32)
                }
                r.ReadInt64(); // offset

                if (isV3) r.ReadUInt32(); // EntriesCount
                int nsCount      = (int)r.ReadUInt32();
                int totalEntries = 0;
                var keyIndex     = new List<(string Ns, string Key, int StrIdx)>();

                for (int i = 0; i < nsCount; i++)
                {
                    if (isV3) r.ReadUInt32(); // ns StrHash
                    var ns       = ReadKeyString(r);
                    int keyCount = (int)r.ReadUInt32();
                    totalEntries += keyCount;
                    for (int j = 0; j < keyCount; j++)
                    {
                        if (isV3) r.ReadUInt32(); // key StrHash
                        var key    = ReadKeyString(r);
                        r.ReadInt32(); // sourceHash
                        int sIdx   = r.ReadInt32();
                        keyIndex.Add((ns, key, sIdx));
                    }
                }

                uint strCount = r.ReadUInt32();
                var strings   = new List<string>((int)strCount);
                for (int i = 0; i < (int)strCount; i++)
                {
                    strings.Add(ReadValueString(r));
                    if (isV3) r.ReadInt32(); // RefCount
                }

                bool enc = NTEEncrypted;
                Solicen.CLI.Console.WriteLine(
                    $"[Green]Locres verified: {nsCount} namespace(s), {totalEntries} entr{(totalEntries == 1 ? "y" : "ies")}, " +
                    $"{strings.Count(s => s.Length > 0)} non-empty string(s){(enc ? " [NTE encrypted v3]" : NTEFormat ? " [NTE v1]" : "")}.");

                int shown = Math.Min(30, keyIndex.Count);
                for (int i = 0; i < shown; i++)
                {
                    var (ns, key, idx) = keyIndex[i];
                    string raw = idx < strings.Count ? strings[idx] : "?";
                    // For encrypted mode, raw is the Base64 ciphertext; try to decrypt for display
                    string display = enc ? TryDecryptForDisplay(raw) : raw;
                    Solicen.CLI.Console.WriteLine(
                        $"[DarkGray]  [{ns}] {key} = {(display.Length > 60 ? display[..60] + "…" : display)}");
                }
            }
            catch (Exception ex)
            {
                Solicen.CLI.Console.WriteLine($"[Red][Verify] Failed to read back locres: {ex.Message}");
            }
        }

        private static string TryDecryptForDisplay(string base64)
        {
            try
            {
                var enc = Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
                using var aes = System.Security.Cryptography.Aes.Create();
                aes.Mode    = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                var dec = aes.CreateDecryptor(NTEKey, null).TransformFinalBlock(enc, 0, enc.Length);
                return Encoding.UTF8.GetString(dec).Split("HottaLocresSplit")[0];
            }
            catch { return $"[enc:{base64[..Math.Min(20,base64.Length)]}…]"; }
        }

        private static string ReadKeyString(BinaryReader r)
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
                int chars = (-len) - 1;
                var bytes = r.ReadBytes(chars * 2);
                r.ReadInt16();
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
                int chars = (-len) - 1;
                var bytes = r.ReadBytes(chars * 2);
                r.ReadInt16();
                return Encoding.Unicode.GetString(bytes);
            }
        }
    }
}
