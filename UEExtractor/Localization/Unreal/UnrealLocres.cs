﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime;
using LocresSharp;
using System.Diagnostics;

namespace Solicen.Localization.UE4
{
    public class LocresResult
    {
        public string Url  { get; set; }
        public string Hash { get; set; }

        public string Key { get; set; }
        public string Source { get; set; }
        public string Translation { get; set; } = string.Empty;

        public LocresResult(string key, string source, string translation = null)
        {
            Key = key;
            Source = source;
            Translation = translation;
        }

    }
    public class UnrealLocres
    {
        public string fileTextFilter = "All localizations files|*.uasset;*.locres;*.umap|Uasset File|*.uasset|Locres File|*.locres|Umap File|*.umap";
        public string errorParseText = "UE4 error while parse folder to create CSV Locres file.";
        public static string FilePATH = string.Empty;
        public static string UEVersion = "5_3";
        public static string AES = string.Empty;

        #region LocresCSV file Setup
        public static bool WriteSkippedCSV = false;
        /// <summary>
        /// CSVWritter for skipped lines;
        /// </summary>
        public static CSVWriter SkippedCSV = new CSVWriter(string.Empty);
        /// <summary>
        /// Skip all .uasset files.
        /// </summary>
        public static bool SkipUassetFile = false;
        /// <summary>
        /// Skip all .uexp files.
        /// </summary>
        public static bool SkipUexpFile = false;
        /// <summary>
        /// Enable force qmarks chars around strings in output.
        /// </summary>
        public static bool ForceQmarksOutput = false;
        /// <summary>
        /// Enable comms header and footer of the csv.
        /// </summary>
        public static bool ForceMark = false;

        public static bool WriteLocres = false;
        #endregion

        public static bool PickyMode = false;
        public static bool IncludeUrlInKeyValue  = false;
        public static bool IncludeHashInKeyValue = false;
        public static string pDirectory = string.Empty;
        public static bool TableSeparator = false;
        public static bool ContainsUpperUpper(string input)
        {
            var s = string.Join("", input.Where(x => x != ' '));
            return s.All(char.IsUpper);
        }

        static bool ContainsAsciiOrNumbers(string s)
        {
            foreach (char c in s)
            {
                if (c > 127 || !char.IsDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        public static ConcurrentDictionary<string, LocresResult> ProcessDirectory(string directory)
        {
            var allResults = new ConcurrentDictionary<string, LocresResult>();
            var filesExtensions = new[] { "*.uexp", "*.uasset" };
            pDirectory = directory;

            using var reader = new UnrealArchiveReader(directory);
            reader.ProcessAllAssets((path, stream) =>
            {
                if (SkipUassetFile && path.EndsWith(".uasset")) return;
                if (SkipUexpFile && path.EndsWith(".uexp")) return;

                List<LocresResult> fileResults = new List<LocresResult>();
                using (stream)
                {
                    fileResults = UnrealUepx.ExtractDataFromStream(stream);
                    if (path.EndsWith(".uexp"))
                    {
                        var newResult = UE4.UnrealUasset.ExtractDataFromStream(stream, path);
                        fileResults.AddRange(newResult);
                    }
                    if (path.EndsWith(".uasset"))
                    {
                        var newResult = UE4.UnrealUasset.ExtractDataFromStream(stream, path).Where(x => fileResults.Any(q => q.Key != x.Key)).ToList();
                        fileResults.AddRange(newResult);
                    }
                }

                #region Zero Data
                if (fileResults.Count == 0 && PickyMode) ZeroDataMessage();
                #endregion

                foreach (var result in fileResults)
                {
                    if (result == null) continue;
                    if (UnrealLocres.IncludeHashInKeyValue) result.Key = $"[{result.Key}][{result.Hash}]";
                    if (UnrealLocres.IncludeUrlInKeyValue) result.Key = $"[{result.Url}]{result.Key}";

                    Console.WriteLine($" - {result.Key} | {result.Source} |");
                    allResults[result.Key] = result;
                }
            });

            #region Zero Data All
            if (allResults.Count == 0) ZeroDataMessage();
            #endregion

            // Сортируем результаты по длине строки Source
            var sortedResults = allResults
                .OrderBy(result => result.Value.Source.Length)  // Сначала по длине строки
                .ThenBy(result => result.Value.Source)  // Затем по алфавиту (для одинаковой длины)
                .ToDictionary(result => result.Key, result => result.Value);
            

            // Преобразуем отсортированный словарь обратно в ConcurrentDictionary
            var sortedConcurrentResults = new ConcurrentDictionary<string, LocresResult>(allResults);

            GC.Collect();
            return sortedConcurrentResults;
        }

        /*
        public static void LocresMerge(string locresFile1, string locresFile2)
        {
            var result = new List<LocresResult>();
            var _tempL1 = ZenParser.ParseLocres(File.ReadAllBytes(locresFile1));
            Console.WriteLine($"Total lines in locres1: {_tempL1.Count}");
            var _tempL2 = ZenParser.ParseLocres(File.ReadAllBytes(locresFile2));
            Console.WriteLine($"Total lines in locres2: {_tempL1.Count}");

            result.AddRange(_tempL1);
            result.AddRange(_tempL2.Where(x => result.Any(key => key.Key != x.Key))); // Добавляем только без дубликатов

            var filePath = $"{AppDomain.CurrentDomain}\\{Path.GetFileNameWithoutExtension(locresFile1)}_NEW.locres";
            Console.WriteLine($"New locres has been merged to: {filePath}");
            WriteToLocres(result.ToArray(), filePath);
        }
        */

        public static void WriteToLocres(LocresResult[] results, string outputLocres)
        {
            LocresWriter locres = new LocresWriter(outputLocres, "");
            locres.Write(results);
        }
        public static void WriteToLocres(ConcurrentDictionary<string, LocresResult> results, string outputLocres)
        {
            var result = results.Select(x => x.Value).ToArray();
            WriteToLocres(result, outputLocres);
        }

        public static void WriteToCsv(ConcurrentDictionary<string, LocresResult> results, string outputCsv)
        {
            using (var writer = new StreamWriter(outputCsv, false, Encoding.UTF8))
            {
                // Write the header comments
                if (ForceMark) writer.WriteLine("# UnrealEngine .locres asset");

                var keyHeader = IncludeHashInKeyValue ? "[key][hash]" : "key";
                keyHeader = IncludeUrlInKeyValue ? $"[url]{keyHeader}" : keyHeader;

                if (!TableSeparator) 
                    writer.WriteLine($"{keyHeader},source,Translation"); // Write the column headers

                // Write the data rows
                var separator = TableSeparator ? "|" : ",";
                foreach (var result in results.Values)
                {
                    if (ForceQmarksOutput)
                        writer.WriteLine($"{EscapeCsvField(result.Key)}{separator}\"{result.Source}\"{separator}{result.Translation}");
                    else
                        writer.WriteLine($"{EscapeCsvField(result.Key)}{separator}{EscapeCsvField(result.Source)}{separator}{EscapeCsvField(result.Translation)}");
                }

                // Write the footer comments
                var programName = typeof(UnrealLocres).Assembly.GetName().Name;
                if (ForceMark) writer.WriteLine($"# Extracted with {programName} & Solicen");
            }
        }

        private static string EscapeCsvField(string field)
        {
            if (field == null) return field;                 // Return empty string
            if (TableSeparator) return field;                // Don't write a QMarks between | string | 
            if (field.Contains(',')) field = $"\"{field}\""; // Return QMarks between " string " if detect comma symbol in line.          
            return field;
        }

        public static byte[] Combine(byte[] remainder, byte[] buffer, int bytesRead)
        {
            byte[] result = new byte[remainder.Length + bytesRead];
            Array.Copy(remainder, 0, result, 0, remainder.Length);
            Array.Copy(buffer, 0, result, remainder.Length, bytesRead);
            return result;
        }

        public class ChunkResult
        {
            public List<LocresResult> Results { get; }
            public byte[] Remainder { get; }

            public ChunkResult(List<LocresResult> results, byte[] remainder)
            {
                Results = results;
                Remainder = remainder;
            }
        }

        public class LocresFileWriter
        {
            private const ushort LocresVersion = 1;

            public void WriteLocresFile(string outputPath, List<LocresResult> locresResults)
            {
                using (var writer = new BinaryWriter(File.Open(outputPath, FileMode.Create), Encoding.UTF8))
                {
                    // Write the header
                    writer.Write(LocresVersion); // Version
                    writer.Write((ushort)0); // Placeholder for data count, we'll come back to it later
                    long countPosition = writer.BaseStream.Position - sizeof(ushort);

                    // Write the string data
                    var stringTable = new Dictionary<string, int>();
                    foreach (var result in locresResults)
                    {
                        WriteString(writer, result.Key, stringTable);
                        WriteString(writer, result.Source, stringTable);
                        WriteString(writer, result.Translation, stringTable);
                    }

                    // Go back and write the correct data count
                    long endPosition = writer.BaseStream.Position;
                    writer.BaseStream.Seek(countPosition, SeekOrigin.Begin);
                    writer.Write((ushort)locresResults.Count);
                    writer.BaseStream.Seek(endPosition, SeekOrigin.Begin);
                }
            }

            private void WriteString(BinaryWriter writer, string value, Dictionary<string, int> stringTable)
            {
                if (stringTable.TryGetValue(value, out int index))
                {
                    writer.Write((byte)1); // String is already in the table
                    writer.Write(index);
                }
                else
                {
                    writer.Write((byte)0); // New string
                    byte[] bytes = Encoding.UTF8.GetBytes(value);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                    stringTable[value] = stringTable.Count;
                }
            }
        }

        #region Output messages
        private static void ZeroDataMessage()
        {
            Console.WriteLine(
            "\nThe extracted data is equal to: 0.\n" +
            "This is strange, it looks like a bug or something that needs to be solved.\n" +
            "Contact me using my contacts here : (https://github.com/SolicenTEAM ) and I will try to solve your problem.\n" +
            "You can also open a 'Issue' here  : (https://github.com/SolicenTEAM/UEExtractor/issues) I'll notice it.\n" +
            "Thank you in advance, and thank you for your patience!\n");
        }
        #endregion
    }
}
