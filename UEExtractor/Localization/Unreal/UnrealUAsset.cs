﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Solicen.Localization.UE4.UnrealLocres;

namespace Solicen.Localization.UE4
{
    public static class UnrealUasset
    {
        private static readonly byte[] StartSequence = { 0x29, 0x01, 0x1F };
        private static readonly byte[] SeparatorSequence = { 0x00, 0x1F };
        private const int HashLength = 32; // 32 hex characters

        public static bool parallelProcessing = true;
        public static bool InculdeHashInKeyValue = false;
        public static bool IncludeUrlInKeyValue = false;

        public static List<LocresResult> ExtractDataFromStream(Stream stream, string path, bool includeInvalidData = false)
        {
            var results = new ConcurrentBag<LocresResult>();

            const int chunkSize = 40480; // Define a reasonable chunk size
            long fileLength = stream.Length;

            byte[] buffer = new byte[chunkSize];
            long position = 0;
            byte[] remainder = Array.Empty<byte>();

            if (parallelProcessing)
            {
                // Parallel processing setup
                int chunkCount = (int)Math.Ceiling((double)fileLength / chunkSize);
                var tasks = new List<Task>();

                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                {
                    int currentChunkIndex = chunkIndex; // Capture the index for the lambda expression
                    tasks.Add(Task.Run(() => ProcessChunk(stream, path, currentChunkIndex, chunkSize, results)));
                }
                Task.WaitAll(tasks.ToArray());

                // Explicitly clear remainder array
                Array.Clear(remainder, 0, remainder.Length);
            }
            else
            {
                while (position < fileLength)
                {
                    int bytesRead = stream.Read(buffer, 0, chunkSize);
                    if (bytesRead == 0) break;

                    // Combine remainder from previous chunk with current chunk
                    byte[] chunk = UnrealLocres.Combine(remainder, buffer, bytesRead);

                    // Process the combined chunk
                    var chunkResult = ExtractFromChunk(chunk);

                    // Add results from current chunk to the final results
                    foreach (var result in chunkResult.Results)
                    {
                        results.Add(result);
                    }

                    // Update remainder
                    remainder = chunkResult.Remainder;
                    position += bytesRead;

                    // Explicitly clear buffer and chunk arrays
                    Array.Clear(buffer, 0, buffer.Length);
                    Array.Clear(chunk, 0, chunk.Length);
                }

                // Handle any remaining data in the last chunk
                if (remainder.Length > 0)
                {
                    var finalResults = ExtractFromChunk(remainder);
                    foreach (var result in finalResults.Results)
                    {
                        results.Add(result);
                    }
                }
                // Explicitly clear remainder array
                Array.Clear(remainder, 0, remainder.Length);
            }

            // Force garbage collection to free memory
            GC.Collect();

            return results.ToList();
        }

        private static void ProcessChunk(Stream stream, string path, int chunkIndex, int chunkSize, ConcurrentBag<LocresResult> results)
        {
            long position = chunkIndex * chunkSize;
            byte[] buffer = new byte[chunkSize];

            stream.Seek(position, SeekOrigin.Begin);
            int bytesRead = stream.Read(buffer, 0, chunkSize);
            if (bytesRead > 0)
            {
                var chunkResult = ExtractFromChunk(buffer.Take(bytesRead).ToArray());
                foreach (var result in chunkResult.Results)
                {
                    if (UnrealLocres.IncludeUrlInKeyValue) result.Url = path;
                    results.Add(result);
                }
            }
        }

        private static ChunkResult ExtractFromChunk(byte[] chunk)
        {
            List<LocresResult> results = new List<LocresResult>();
            int i = 0;

            while (i <= chunk.Length - StartSequence.Length)
            {
                int startIndex = IndexOf(chunk, StartSequence, i);
                if (startIndex == -1)
                    break;

                int stringStartIndex = startIndex + StartSequence.Length;
                int separatorIndex = IndexOf(chunk, SeparatorSequence, stringStartIndex);

                if (separatorIndex != -1)
                {
                    string decodedString = LocresHelper.EscapeKey(Encoding.UTF8.GetString(chunk, stringStartIndex, separatorIndex - stringStartIndex));
                    int hashStartIndex = separatorIndex + SeparatorSequence.Length;
                    int hashEndIndex = hashStartIndex + HashLength;

                    string decodedHash = Encoding.UTF8.GetString(chunk, hashStartIndex, HashLength).Trim();
                    if (IsValidHash(decodedHash))
                    {
                        var res = new LocresResult(decodedHash, decodedString);
                        if (UnrealLocres.IncludeHashInKeyValue) res.Hash = LocresSharp.Crc.StrCrc32(decodedString).ToString();
                        results.Add(res);

                        i = hashEndIndex + SeparatorSequence.Length;
                        continue;
                    }

                }

                i = startIndex + 1;
            }

            // Handle remainder data
            byte[] remainder = new byte[chunk.Length - i];

            // Explicitly clear chunk array
            Array.Clear(chunk, 0, chunk.Length);

            return new ChunkResult(results, remainder);
        }

        static bool IsValidHash(string hash)
        {
            if (hash.Length != 32) return false;           // Если длина хеш строки не равна 32 символам
            if (hash.All(char.IsDigit)) return false;      // Если все символы это только цифры
            if (hash.All(x => x == hash[0])) return false; // Если все символы одинаковые, пример: DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD

            foreach (char c in hash)                       // Проверка, только разрешенные символы для хеш строки
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static int IndexOf(byte[] array, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= array.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }
    }
}
