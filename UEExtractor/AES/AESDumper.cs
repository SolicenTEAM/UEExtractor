using System.Diagnostics;
namespace Solicen.Localization.UE4;
/// <summary>
/// AES key extractor for Unreal Engine 4/5 executables.
/// Based on AESDumpster by GHFear (https://github.com/GHFear/AESDumpster).
/// C# port with optimizations: fast masked pattern search, entropy-based filtering,
/// false positive removal, and direct key output (console / file).
/// </summary>
public class AESDumper
{
    private readonly bool _verbose;

    // Original patterns and DWORD offsets (from AESDumpster)
    private static readonly string[] Patterns =
    {
        "C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ?",
        "C7 ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ?",
        "C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? 48 ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ?",
        "C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? ? C7 ? ? ? ? ? C3"
    };

    private static readonly int[][] DwordOffsets =
    {
        new[] { 3, 10, 17, 24, 35, 42, 49, 56 },
        new[] { 2, 9, 16, 23, 30, 37, 44, 51 },
        new[] { 3, 10, 21, 28, 35, 42, 49, 56 },
        new[] { 51, 45, 38, 31, 24, 17, 10, 3 }
    };

    private static readonly HashSet<string> FalsePositives = new HashSet<string>
    {
        "FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9FFD9",
        "67E6096A85AE67BB72F36E3C3AF54FA57F520E518C68059BABD9831F19CDE05B",
        "D89E05C107D57C3617DD703039590EF7310BC0FF11155868A78FF964A44FFABE",
        "9A99593F9A99593F0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53BAE47A13F",
        "6F168073B9B21449D742241700068ADABC306FA9AA3831164DEE8DE34E0EFBB0",
        "0AD7633FCDCC4C3DCDCCCC3D52B8BE3F9A99593F9A99593FC9767E3FE17A543F",
        "168073C7B21449C7430C00064310BC304314AA3843184DEE431C4E0E83C4205B",
        "E6096AC7AE67BBC7430C3AF543107F5243148C684318ABD9431C19CD436C2000",
        "9E05C1C7D57C36C7430C39594310310B431411154318A78F431CA44F436C1C00",
        "9E05C1C7D57C36C7DD7030C7590EF7C70BC0FFC7155868C78FF964C7A44FFABE",
        "168073C7B21449C7422417C7068ADAC7306FA9C7383116C7EE8DE3C74E0EFBB0",
        "0AD7633FCDCC4C3D00C742143DC742183FC7421C3FC742203FC742247E3FC742",
        "0000803F0AD7A33E0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53B54AE47A1",
        "0AD7A33E0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53BAE47A13F58583934",
        "0AD7A33E0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53BAE47A13F38583934",
        "0000803F0AD7A33E0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53B34AE47A1",
        "0000803F0000803F0AD7A33E0AD7633F52B8BE3FE17A543FCDCC4C3D2C4260E5",
        "0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53BAE47A13F5839343C4CC9767E",
        "0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53BAE47A13F5839343C4CC9767E",
        "07D57C3617DD703039590EF7310BC0FF11155868A78FF964A44FFABE6C1C0000",
        "85AE67BB72F36E3C3AF54FA57F520E518C68059BABD9831F19CDE05B6C200000",
        "E6096AC7AE67BBC7F36E3CC7F54FA5C7520E51C768059BC7D9831FC719CDE05B",
        "0AD7A33E0AD7633F52B8BE3FE17A543FCDCC4C3D4260E53BAE47A13F3C583934",
        "E4D6E74FE4D667500044AC47926595380080DC43000A9B46000080BF000080BF",
        "D04C8F7D71ECC047D8A60970FBA31C9E9EC1250BBBF6459AC480947212E1DB8C"
    };

    // Precompiled pattern masks for fast search
    private class PatternMask
    {
        public byte?[] Mask { get; }
        public int FirstNonNullIndex { get; }
        public byte FirstNonNullValue { get; }

        public PatternMask(byte?[] mask)
        {
            Mask = mask;
            FirstNonNullIndex = -1;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].HasValue)
                {
                    FirstNonNullIndex = i;
                    FirstNonNullValue = mask[i].Value;
                    break;
                }
            }
        }
    }

    private static readonly List<PatternMask> CompiledPatterns = new List<PatternMask>();

    static AESDumper()
    {
        foreach (var pattern in Patterns)
        {
            var tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var mask = new byte?[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "?" || tokens[i] == "??")
                    mask[i] = null;
                else
                    mask[i] = Convert.ToByte(tokens[i], 16);
            }
            CompiledPatterns.Add(new PatternMask(mask));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AESDumper"/> class.
    /// </summary>
    /// <param name="verbose">If true, outputs detailed progress to the console.</param>
    public AESDumper(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Extracts the AES encryption key from the specified Unreal Engine executable.
    /// </summary>
    /// <param name="filePath">Path to the .exe file.</param>
    /// <param name="expectedKey">Optional expected key (64 hex chars) for verification.</param>
    /// <param name="minEntropy">Minimum Shannon entropy of the key's hex representation to be considered (default 3.8).</param>
    /// <returns>The extracted key as a 64-character hex string, or null if not found.</returns>
    public string ExtractKey(string filePath, string expectedKey = null, double minEntropy = 3.8)
    {
        if (!File.Exists(filePath))
        {
            if (_verbose) Console.WriteLine("[!] File not found.");
            return null;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            if (_verbose) Console.WriteLine($"[!] Error reading file: {ex.Message}");
            return null;
        }

        if (_verbose)
        {
            Console.WriteLine($"[*] File size: {data.Length} bytes ({data.Length / 1024.0 / 1024.0:F1} MB)");
            Console.WriteLine("[*] Searching for AES initialization patterns...");
        }

        var sw = Stopwatch.StartNew();

        var candidates = new List<(string key, double entropy, int offset)>();
        var seen = new HashSet<string>();

        for (int patternIdx = 0; patternIdx < CompiledPatterns.Count; patternIdx++)
        {
            var offsets = FindPatternOffsets(data, CompiledPatterns[patternIdx]);
            foreach (var off in offsets)
            {
                if (TryExtractKey(data, off, patternIdx, out string keyHex, out double entropy))
                {
                    if (seen.Add(keyHex))
                        candidates.Add((keyHex, entropy, off));
                }
            }
        }

        sw.Stop();

        if (_verbose)
        {
            Console.WriteLine($"[*] Search time: {sw.Elapsed.TotalSeconds:F3} sec");
            Console.WriteLine($"[*] Unique candidates: {candidates.Count}");
        }

        // Filter by entropy and false positives
        var filtered = candidates
            .Where(c => c.entropy >= minEntropy && !FalsePositives.Contains(c.key.ToUpperInvariant()))
            .OrderByDescending(c => c.entropy)
            .ToList();

        if (_verbose) Console.WriteLine($"[*] After filtering (entropy >= {minEntropy}): {filtered.Count}");

        if (filtered.Count > 0)
        {
            var best = filtered[0];
            if (_verbose)
            {
                Console.WriteLine();
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = best.entropy >= 3.8 ? ConsoleColor.Green :
                                          best.entropy >= 3.5 ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.WriteLine($"Key: 0x{best.key} | Entropy: {best.entropy:F6}");
                Console.ForegroundColor = originalColor;
                Console.WriteLine($"    Offset: 0x{best.offset:X8}");

                if (!string.IsNullOrEmpty(expectedKey) &&
                    string.Equals(best.key, expectedKey, StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("    MATCH with expected key!");
                    Console.ForegroundColor = originalColor;
                }
            }
            best.key = best.key != null ? "0x" + best.key : best.key;
            return best.key;
        }

        // If no key found by patterns, try direct binary search for expected key
        if (!string.IsNullOrEmpty(expectedKey))
        {
            var keyBytes = StringToByteArray(expectedKey);
            int pos = IndexOfBytes(data, keyBytes);
            if (pos >= 0)
            {
                if (_verbose) Console.WriteLine($"[+] Expected key found by binary search at offset 0x{pos:X8}");
                return expectedKey;
            }
        }

        if (_verbose) Console.WriteLine("[!] No AES key found.");
        return null;
    }

    /// <summary>
    /// Extracts the AES key and writes it to a text file.
    /// </summary>
    /// <param name="filePath">Path to the executable.</param>
    /// <param name="outputFilePath">Path for the output file.</param>
    /// <param name="expectedKey">Optional expected key for verification.</param>
    /// <param name="minEntropy">Minimum entropy threshold.</param>
    /// <returns>True if a key was extracted and written, false otherwise.</returns>
    public bool ExtractKeyToFile(string filePath, string outputFilePath, string expectedKey = null, double minEntropy = 3.8)
    {
        string key = ExtractKey(filePath, expectedKey, minEntropy);
        if (key == null) return false;

        try
        {
            File.WriteAllText(outputFilePath, key);
            if (_verbose) Console.WriteLine($"[+] Key written to: {outputFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            if (_verbose) Console.WriteLine($"[!] Failed to write file: {ex.Message}");
            return false;
        }
    }

    #region Private helpers

    private static List<int> FindPatternOffsets(byte[] data, PatternMask pattern)
    {
        var offsets = new List<int>();
        byte?[] mask = pattern.Mask;
        int maskLen = mask.Length;
        int dataLen = data.Length;

        // Quick search using first non-wildcard byte
        if (pattern.FirstNonNullIndex >= 0)
        {
            byte firstByte = pattern.FirstNonNullValue;
            int firstIdx = pattern.FirstNonNullIndex;
            for (int i = 0; i <= dataLen - maskLen; i++)
            {
                if (data[i + firstIdx] != firstByte) continue;

                bool match = true;
                for (int j = 0; j < maskLen; j++)
                {
                    if (mask[j].HasValue && data[i + j] != mask[j].Value)
                    {
                        match = false;
                        break;
                    }
                }
                if (match) offsets.Add(i);
            }
        }
        else // all wildcards (shouldn't happen with current patterns)
        {
            for (int i = 0; i <= dataLen - maskLen; i++)
                offsets.Add(i);
        }

        return offsets;
    }

    private static bool TryExtractKey(byte[] data, int baseOffset, int patternIndex, out string keyHex, out double entropy)
    {
        keyHex = null;
        entropy = 0.0;
        var offsets = DwordOffsets[patternIndex];
        var chunks = new byte[4 * offsets.Length];
        try
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                int pos = baseOffset + offsets[i];
                if (pos + 4 > data.Length) return false;
                Array.Copy(data, pos, chunks, i * 4, 4);
            }
        }
        catch
        {
            return false;
        }

        keyHex = BitConverter.ToString(chunks).Replace("-", "").ToUpperInvariant();
        entropy = CalcEntropy(keyHex);
        return true;
    }

    private static double CalcEntropy(string hexKey)
    {
        if (string.IsNullOrEmpty(hexKey)) return 0.0;
        var freq = new Dictionary<char, int>();
        foreach (char c in hexKey)
        {
            if (freq.ContainsKey(c))
                freq[c]++;
            else
                freq[c] = 1;
        }
        double entropy = 0.0;
        int len = hexKey.Length;
        foreach (var count in freq.Values)
        {
            double p = (double)count / len;
            if (p > 0)
                entropy -= p * Math.Log(p, 2);
        }
        return entropy;
    }

    private static byte[] StringToByteArray(string hex)
    {
        int numberChars = hex.Length;
        byte[] bytes = new byte[numberChars / 2];
        for (int i = 0; i < numberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    private static int IndexOfBytes(byte[] data, byte[] pattern)
    {
        int len = pattern.Length;
        int limit = data.Length - len;
        for (int i = 0; i <= limit; i++)
        {
            int j;
            for (j = 0; j < len; j++)
            {
                if (data[i + j] != pattern[j])
                    break;
            }
            if (j == len) return i;
        }
        return -1;
    }

    #endregion
}