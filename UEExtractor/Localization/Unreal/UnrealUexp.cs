using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Solicen.Localization.UE4.UnrealLocres;

namespace Solicen.Localization.UE4
{
    public class UnrealUepx
    {
        #region Console Settings
        public static bool SkipUpperUpper = true;
        public static bool SkipUnderscore = true;
        public static bool IncludeInvalidData = false;
        public static bool IncludeHashInKeyValue = false;
        public static bool IncludeUrlInKeyValue = false;
        #endregion

        static bool ContainsUpperUpper(string input)
        {
            var s = string.Join("",input.Where(x => x != ' '));
            return s.All(char.IsUpper);
        }

        static HashSet<byte> allowedChars = new HashSet<byte>(Enumerable.Range(48, 10).Concat(Enumerable.Range(65, 6)).Concat(Enumerable.Range(97, 6)).Select(x => (byte)x));

        public static List<LocresResult> ExtractDataFromFile(string filePath)
        {
            var results = new ConcurrentBag<LocresResult>();
            const int chunkSize = 40480; // Define a reasonable chunk size
            var fileInfo = new FileInfo(filePath);
            long fileLength = fileInfo.Length;

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[chunkSize];
                long position = 0;
                byte[] remainder = Array.Empty<byte>();

                while (position < fileLength)
                {
                    int bytesRead = fileStream.Read(buffer, 0, chunkSize);
                    if (bytesRead == 0) break;

                    byte[] chunk;
                    if (remainder.Length > 0)
                    {
                        chunk = new byte[remainder.Length + bytesRead];
                        Array.Copy(remainder, 0, chunk, 0, remainder.Length);
                        Array.Copy(buffer, 0, chunk, remainder.Length, bytesRead);
                    }
                    else
                    {
                        chunk = new byte[bytesRead];
                        Array.Copy(buffer, 0, chunk, 0, bytesRead);
                    }

                    ProcessChunk(chunk, chunk.Length, allowedChars, results, IncludeInvalidData, out remainder);
                    position += bytesRead;

                    // Explicitly clear buffer and chunk arrays
                    Array.Clear(buffer, 0, buffer.Length);
                    Array.Clear(chunk, 0, chunk.Length);
                }
                if (remainder.Length > 0)
                {
                    ProcessChunk(remainder, remainder.Length, allowedChars, results, IncludeInvalidData, out remainder);
                }
            }

            // Force garbage collection to free memory
            GC.Collect();
            if (UnrealLocres.IncludeUrlInKeyValue)
            {
                var path = UnrealLocres.FilePATH;
                foreach(var result in results)
                {
                    result.Url = path;
                }
            }
            return results.ToList();
        }

        static bool IsValidHash(string hash)
        {
            if (hash.Length != 32) return false;
            if (hash.All(char.IsDigit)) return false;
            foreach (char c in hash)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        static bool IsValidDecode(string decode)
        {
            if (decode.StartsWith("\x00\x00\x00") || 
                decode.StartsWith("\x00\x16\x0F\x00")) 
                return false;

            return true;
        }

        static void ProcessChunk(byte[] buffer, int bytesRead, HashSet<byte> allowedChars, ConcurrentBag<LocresResult> results, bool includeInvalidData, out byte[] remainder)
        {
            int i = 0;
            remainder = Array.Empty<byte>();

            while (i < bytesRead - 37)
            {
                if (buffer.Length - i < 37)
                {
                    remainder = new byte[buffer.Length - i];
                    Array.Copy(buffer, i, remainder, 0, buffer.Length - i);
                    break;
                }

                byte[] hashCandidate = new byte[32];
                Array.Copy(buffer, i, hashCandidate, 0, 32);               

                if (!hashCandidate.All(allowedChars.Contains))
                {
                    i++;
                    continue;
                }

                try
                {
                    int size = BitConverter.ToInt32(buffer, i + 33);
                    int endPos = i + 37 + size - 1;

                    if (endPos > bytesRead || size <= 0)
                    {
                        i++;
                        continue;
                    }

                    if (endPos > buffer.Length)
                    {
                        remainder = new byte[buffer.Length - i];
                        Buffer.BlockCopy(buffer, i, remainder, 0, buffer.Length - i);
                        break;
                    }

                    byte[] stringData = new byte[size - 1];
                    Array.Copy(buffer, i + 37, stringData, 0, size - 1);

                    string hashDecoded = Encoding.UTF8.GetString(hashCandidate).Trim();
                    if (!IsValidHash(hashDecoded))
                    {
                        Console.WriteLine($"SKIP: {hashDecoded}:NULL | InvalidHash");
                        i = endPos;
                        continue;
                    }

                    string stringDecoded = null;
                    bool decodedSuccessfully = false;

                    try
                    {
                        stringDecoded = LocresHelper.EscapeKey(Encoding.UTF8.GetString(stringData));
                        if (IsValidDecode(stringDecoded))
                        {
                            decodedSuccessfully = true;
                        }
                        else
                        {
                            Console.WriteLine($"SKIP: {hashDecoded}:{stringDecoded} | InvalidDecode");
                            i = endPos;
                            continue;
                        }

                    }
                    catch (DecoderFallbackException) 
                    {
                        Console.WriteLine($"DecoderFallbackException: Unable to decode string at position {i}");
                    }
                

                    if (ContainsUpperUpper(stringDecoded) && SkipUpperUpper) {
                        Console.WriteLine($"SKIP: {hashDecoded}:{stringDecoded} | UpperUpper");
                        i = endPos; continue; }
                    if (stringDecoded.Contains('_') && SkipUnderscore) {
                        Console.WriteLine($"SKIP: {hashDecoded}:{stringDecoded} | Underscore");
                        i = endPos; continue; }
                    if (decodedSuccessfully)
                    {
                        if (includeInvalidData || !results.Any(r => r.Key == hashDecoded))
                        {
                            var res = new LocresResult(hashDecoded, stringDecoded);
                            if (UnrealLocres.IncludeHashInKeyValue) res.Hash = LocresSharp.Crc.StrCrc32(stringDecoded).ToString();
                            results.Add(res);
                        }
                    }

                    i = endPos;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing chunk: {ex.Message}");
                    i++;
                }
            }
        }
    }
}
