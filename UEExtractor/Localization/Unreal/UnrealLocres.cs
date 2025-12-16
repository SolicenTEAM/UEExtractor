using System.Collections.Concurrent;
using System.Text;

namespace Solicen.Localization.UE4
{
    public class LocresResult
    {
        public string Url  { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;

        public string Namespace   { get; set; } = string.Empty;
        public string Key         { get; set; } 
        public string Source      { get; set; }
        public string Translation { get; set; } = string.Empty;

        public LocresResult(string Key, string Source, string Translation = null, string Namespace = "")
        {
            this.Key = Key;
            this.Source = Source;
            this.Translation = Translation;
            this.Namespace = Namespace;
        }

    }
    public class UnrealLocres
    {
        public string errorParseText = "UE4 error while parse folder to create CSV Locres file.";
        public static string FilePATH = string.Empty;
        public static string UEVersion = "4_24";
        public static string AES = string.Empty;

        #region LocresCSV file Setup
        public static bool WriteSkippedCSV = false;
        /// <summary>
        /// CSVWritter for skipped lines;
        /// </summary>
        public static CSV.Writer SkippedCSV = new CSV.Writer(string.Empty);
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

        public enum ExportType  { None, TextProperty, StringTable, DataTable }
        public static string[] ExcludePath = { "mesh", "texture", "material", "decal", "model", "_tex/", "/sound/", "/effects/", 
                                               "animation", "/fx/", "/vfx/", "/meshes/", "/megascans/", "/music/" };
        public static bool ExtractLocres = false;
        public static bool AllFolders = false;
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
            var excludeTypes = new[] { "Texture2D", "SoundWave", "StaticMesh", "Material", "MaterialInstanceConstant",
                                       "Skeleton", "AnimSequence", "PhysicsAsset", "Font", "CurveTable", "SoundCue" };
            pDirectory = directory;

            using var reader = new UnrealArchiveReader(directory, UEVersion);
            reader.ProcessAllAssets((path, stream) =>
            {
                if (ExtractLocres && path.Contains("/Localization/") && path.EndsWith("Game.locres"))
                {
                    var _fileName = Path.GetFileName(path);
                    var _saveDirectory = Path.GetDirectoryName(SkippedCSV.FilePath);
                    File.WriteAllBytes($"{_saveDirectory}\\{_fileName}", reader.LoadAsset(path).GetBuffer());
                }
                if (!AllFolders && ExcludePath.Any(x => path.ToLower().Contains(x))) return;
                if (SkipUassetFile && path.EndsWith(".uasset")) return;
                if (SkipUexpFile && path.EndsWith(".uexp")) return;

                List<LocresResult> fileResults = new List<LocresResult>();

                var eType = path.EndsWith(".uasset") ? IsTable(stream) : ExportType.None;
                if (eType == ExportType.StringTable)
                {
                    reader.LoadStringTable(path, (TableNamespace, Keys) =>
                    {
                        var newResult = TableToLocres(TableNamespace, Keys);
                        fileResults.AddRange(newResult);
                    });
                }
                else if (eType == ExportType.DataTable)
                {             
                    var res = reader.LoadDataTable(Path.ChangeExtension(path, ".uasset"));
                    var newResult = TableToLocres(res);
                    fileResults.AddRange(newResult);
                }
                else if (eType == ExportType.None)
                {                             
                    using (stream)
                    {
                        fileResults = UnrealUepx.ExtractDataFromStream(stream);
                        var newResult = UE4.UnrealUasset.ExtractDataFromStream(stream, path)
                            .Where(x => fileResults.Any(q => q.Key != x.Key)).ToList();
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
                .OrderBy(result => result.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Value.Source.Length) // Сначала по длине строки
                .ThenBy(result => result.Value.Source)  // Затем по алфавиту (для одинаковой длины)
                .ToDictionary(result => result.Key, result => result.Value);
            

            // Преобразуем отсортированный словарь обратно в ConcurrentDictionary
            var sortedConcurrentResults = new ConcurrentDictionary<string, LocresResult>(allResults);

            GC.Collect(2);
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

        // Ищем "StringTable" // 
        public static bool IsStringTable(byte[] buffer)
        {
            if (BinaryParser.FindSequence(buffer, new ReadOnlySpan<byte>(
                new byte[] { 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x54, 0x61, 0x62, 0x6C, 0x65 }).ToArray()) != -1)
            {
                return true;
            }
            return false;
        }

        // Ищем "ArrayProperty" (признак DataTable)
        public static bool IsArrayProperty(byte[] buffer)
        {
            if (BinaryParser.FindSequence(buffer.ToArray(), new ReadOnlySpan<byte>(
                new byte[] { 0x41, 0x72, 0x72, 0x61, 0x79, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79 }).ToArray()) != -1)
            {
                return true;
            }
            return false;
        }

        // Ищем "TextProperty" 
        public static bool IsTextProperty(byte[] buffer)
        {
            if (BinaryParser.FindSequence(buffer.ToArray(), new ReadOnlySpan<byte>(
                new byte[] { 0x54, 0x65, 0x78, 0x74, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79 }).ToArray()) != -1)
            {
                return true;
            }
            return false;
        }

        public static ExportType IsTable(Stream stream)
        {
            const int bufferSize = 1024;
            long originalPosition = stream.Position; // Сохраняем исходную позицию
            try
            {
                // Выделяем буфер на стеке. Это очень быстро и не создает мусора в куче.
                Span<byte> buffer = stackalloc byte[bufferSize];
                
                stream.Read(buffer);

                // Ищем "StringTable"
                if (IsStringTable(buffer.ToArray()))
                {
                    return ExportType.StringTable;
                }

                // Ищем "ArrayProperty" (признак DataTable)
                if (IsArrayProperty(buffer.ToArray()))
                {
                    if (IsTextProperty(buffer.ToArray()))
                    {
                        return ExportType.DataTable;
                    }
                }

                return ExportType.None;
            }
            finally
            {
                stream.Position = originalPosition; // Всегда восстанавливаем исходную позицию потока
            }
        }

        public static LocresResult[] TableToLocres(List<(string Namespace, string Key, string SourceString)> results)
        {
            List<LocresResult> result = new List<LocresResult>();
            foreach (var res in results)
            {
                result.Add(new LocresResult($"{res.Key}", LocresHelper.EscapeKey(res.SourceString), Namespace: res.Namespace));
            }
            return result.ToArray();
        }

        public static LocresResult[] TableToLocres(string TableNamespace, Dictionary<string,string> keys)
        {
            List<LocresResult> result = new List<LocresResult>();
            foreach (var key in keys)
            {
                result.Add(new LocresResult($"{key.Key}", LocresHelper.EscapeKey(key.Value), Namespace: TableNamespace));
            }
            return result.ToArray();
        }

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
                    var line = result.Namespace != string.Empty ? $"{result.Namespace}::" : "";
                    if (ForceQmarksOutput)
                       line += ($"{CSV.EscapeCsvField(result.Key)}{separator}\"{result.Source}\"{separator}{result.Translation}");
                    else
                       line += ($"{CSV.EscapeCsvField(result.Key)}{separator}{CSV.EscapeCsvField(result.Source)}{separator}{CSV.EscapeCsvField(result.Translation)}");
                    writer.WriteLine(line);
                }

                // Write the footer comments
                var programName = typeof(UnrealLocres).Assembly.GetName().Name;
                if (ForceMark) writer.WriteLine($"# Extracted with {programName} & Solicen");
            }
        }

        public static LocresResult[] LoadFromCSV(string path)
        {
            var result = new List<LocresResult>();
            var delimiter = TableSeparator ? "|" : ",";
            CSV.Parse(path, (row) =>
            {
                if (row.TryGetColumn(2, out string value) && value == "Translation") { }
                else
                {
                    var _keyNamespace = row.Columns[0];

                    var _Namespace = _keyNamespace.Contains("::") ? _keyNamespace.Split("::")[0] : string.Empty;
                    var _Key = _keyNamespace.Contains("::") ? _keyNamespace.Split("::")[1] : _keyNamespace;

                    var _Source = row.Columns[1];
                    var _Translation = row.Columns[2];

                    var res = new LocresResult(_Key, _Source, _Translation, _Namespace);
                    result.Add(res);
                }
                

            });       
            return result.ToArray();
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
