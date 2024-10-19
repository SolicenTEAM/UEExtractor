using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime;

namespace Solicen.Localization.UE4
{
    public class LocresResult
    {
        public string Key { get; set; }
        public string Source { get; set; }
        public string Translation { get; set; } = string.Empty;

        public LocresResult(string key, string source)
        {
            Key = key;
            Source = source;
        }

    }
    public class UnrealLocres
    {
        public string fileTextFilter = "All localizations files|*.uasset;*.locres;*.umap|Uasset File|*.uasset|Locres File|*.locres|Umap File|*.umap";
        public string errorParseText = "UE4 error while parse folder to create CSV Locres file.";

        #region LocresCSV file Setup
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
        #endregion

        private static bool hashInKey = false;
        public static void IncludeHashInKeyValue()
        {
            UnrealUasset.InculdeHashInKeyValue = true; 
            UnrealUepx.InculdeHashInKeyValue = true;
            hashInKey = true;
        }

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

            var files = filesExtensions.SelectMany(ext => Directory.GetFiles(directory, ext, SearchOption.AllDirectories)).ToArray();

            files = SkipUassetFile ? files.Where(x => !x.EndsWith(".uasset")).ToArray() : files;
            files = SkipUexpFile   ? files.Where(x => !x.EndsWith(".uexp")).ToArray()   : files;

            Parallel.For(0, files.Length, i =>
            {         
                List<LocresResult> fileResults = new List<LocresResult>();
                string file = files[i]; string filePATH = file.Replace(directory, "..");
                Console.WriteLine(filePATH);
                
                fileResults = UnrealUepx.ExtractDataFromFile(file);

                if (file.EndsWith(".uexp"))
                {
                    var newResult = UE4.UnrealUasset.ExtractDataFromFile(file);
                    fileResults.AddRange(newResult);
                }

                if (file.EndsWith(".uasset"))
                {
                    var newResult = UE4.UnrealUasset.ExtractDataFromFile(file).Where(x => fileResults.Any(q => q.Key != x.Key)).ToList();
                    fileResults.AddRange(newResult);
                }
                foreach (var result in fileResults)
                {
                    Console.WriteLine($"{result.Key} | {result.Source} |");
                    allResults[result.Key] = result;
                }
            });

            // Сортируем результаты по длине строки Source
            var sortedResults = allResults
                .OrderBy(result => result.Value.Source.Length)  // Сначала по длине строки
                .ThenBy(result => result.Value.Source)  // Затем по алфавиту (для одинаковой длины)
                .ToDictionary(result => result.Key, result => result.Value);

            // Преобразуем отсортированный словарь обратно в ConcurrentDictionary
            var sortedConcurrentResults = new ConcurrentDictionary<string, LocresResult>(sortedResults);
            return sortedConcurrentResults;
        }

        public static void WriteToCsv(ConcurrentDictionary<string, LocresResult> results, string outputCsv)
        {
            using (var writer = new StreamWriter(outputCsv, false, Encoding.UTF8))
            {
                // Write the header comments
                if (ForceMark) writer.WriteLine("# UnrealEngine .locres asset");

                var keyHeader = hashInKey ? "[key][hash]" : "key";
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
            if (field.Contains(',')) field = $"\"{field}\"";          
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
    }
}
