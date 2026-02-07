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

        public enum ExportType  { None, TextProperty, StringTable, DataTable, Table, BlueprintClass }
        public static string[] ExcludePath = { "mesh", "texture", "material", "decal", "model", "_tex/", "/sound/", "/effects/", 
                                               "animation", "/fx/", "/vfx/", "/meshes/", "/megascans/", "/music/", "blueprints" };
        public static bool EngineSpecified = false;
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

        public static string GetUasset(string path)
        {
            return Path.ChangeExtension(path, ".uasset");
        }
        public static ConcurrentDictionary<string, LocresResult> ProcessDirectory(string directory)
        {
            var allResults = new ConcurrentDictionary<string, LocresResult>();
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
                var eType = path.EndsWith(".uasset") ? GetExportType(stream) : ExportType.None;
                    eType = path.Contains("BP_") ? ExportType.BlueprintClass : eType;

                switch (eType)
                {

                    case ExportType.Table:
                        {
                            var newResult = new LocresResult[0];

                            // StringTable 
                            reader.LoadStringTable(path, (TableNamespace, Keys) =>
                            {
                                newResult = TableToLocres(TableNamespace, Keys);
                                fileResults.AddRange(newResult);
                            });

                            // DataTable
                            if (newResult.Length == 0)
                            {
                                reader.LoadDataTable(GetUasset(path), (Table) =>
                                {
                                    newResult = TableToLocres(Table);
                                    fileResults.AddRange(newResult);
                                });
                            }
                        }
                        break;
                    case ExportType.BlueprintClass:
                        {
                            // Потенциально содержит KismetString | EX_StringConst
                            // Обработка возможна только через KissE (Kismet Editor)
                            break;
                        }
                    default:
                        {
                            using (stream)
                            {
                                fileResults = UnrealUepx.ExtractDataFromStream(stream);
                                var newResult = UnrealUasset.ExtractDataFromStream(stream, path)
                                    .Where(x => fileResults.Any(q => q.Key != x.Key)).ToList();
                                fileResults.AddRange(newResult);
                            }
                            break;
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

                    var outputValue = result.Namespace != string.Empty ?
                    $"\t{result.Namespace}::{result.Key}\t{result.Source}\t" : $"\t{result.Key}\t{result.Source}\t";

                    Console.WriteLine(outputValue);
                    allResults[result.Key] = result;
                }
            });

            #region Zero Data All
            if (allResults.Count == 0) ZeroDataMessage();
            #endregion

            // Преобразуем отсортированный словарь обратно в ConcurrentDictionary
            var sortedConcurrentResults = new ConcurrentDictionary<string, 
                LocresResult>(allResults.Where(x => !IsNotAllowedString(x.Value.Source)));

            GC.Collect(2);
            return sortedConcurrentResults;
        }

        public static bool IsNotAllowedString(string value)
        {
            return (
                string.IsNullOrWhiteSpace(value)
                || value.Trim().Length < 2
                || value == "None"
                || value.IsGUID()
                || value.IsAllNumber()
                || value.IsAllDot()
                || value.IsBoolean()
                || value.IsPath()
                || value.IsAllOne()
                || value.IsStringDigit());
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

        // Ищем "BlueprintGeneratedClass" //
        public static bool IsBlueprintGeneratedClass(Span<byte> buffer)
        {
            if (BinaryParser.FindSequence(buffer, new ReadOnlySpan<byte>(
            new byte[] { 
                0x42, 0x6C, 0x75, 0x65, 0x70, 0x72, 0x69, 0x6E, 0x74, 0x47, 0x65, 0x6E, 
                0x65, 0x72, 0x61, 0x74, 0x65, 0x64, 0x43, 0x6C, 0x61, 0x73, 0x73 }).ToArray()) != -1)
            {
                return true;
            }
            return false;
        }

        // Ищем "StringTable" // 
        public static bool IsStringTable(Span<byte> buffer)
        {
            bool result = false;

            // Определяем является ли это таблицей в ZenLoader
            if (BinaryParser.FindSequence(buffer, UnrealFormat.Table.AnyTable) != -1)
            {
                // Если это таблица ищем в первых 1024 байтах строку StringTable
                if (BinaryParser.FindSequence(buffer, 
                    UnrealFormat.Table.StringTable) != -1)
                    result = true;
                
                if (BinaryParser.FindSequence(buffer, 
                    UnrealFormat.Table.ST_Exp) != -1)
                    result = true;
                
            }

            
            // Ищем структуру из не-ZenLoader файла
            if (BinaryParser.FindSequence(buffer, new ReadOnlySpan<byte>(
                new byte[] { 0xC1, 0x83, 0x2A, 0x9E }).ToArray()) != -1)
            {
                result = true;
            }
            

            // Fallback версия определения через StringTable
            if (BinaryParser.FindSequence(buffer, UnrealFormat.Table.StringTable) != -1)
            {
                return true;
            }
       
            return result;
        }

        // Ищем "ArrayProperty" (признак DataTable)
        public static bool IsArrayProperty(Span<byte> buffer)
        {
            if (BinaryParser.FindSequence(buffer, 
                UnrealFormat.Property.ArrayProperty) != -1)
            {
                return true;
            }
            return false;
        }

        // Ищем "TextProperty" 
        public static bool IsTextProperty(Span<byte> buffer)
        {
            if (BinaryParser.FindSequence(buffer, 
                UnrealFormat.Property.TextProperty) != -1)
            {
                return true;
            }
            return false;
        }

        public static bool IsTable(Span<byte> buffer)
        {
            // Определяем таблицу // one-uasset // ZenLoader
            if (BinaryParser.FindSequence(buffer, UnrealFormat.Table.AnyTable) != -1)
                return true;

            // Находим fallback - StringTable // uexp, uasset // Non-ZenLoader
            if (BinaryParser.FindSequence(buffer, UnrealFormat.Table.StringTable) != -1)
                return true;
  
            // Находим fallback - DataTable // uexp, uasset // Non-ZenLoader
            if (BinaryParser.FindSequence(buffer, UnrealFormat.Table.DataTable) != -1)
                return true;

            return false;
        }

        public static bool IsTexture(Span<byte> buffer)
        {
            if (BinaryParser.FindSequence(buffer, 
                UnrealFormat.Texture.PF_DXT5) != -1 
                || 
                BinaryParser.FindSequence(buffer,
                UnrealFormat.Texture.B8G8R8A8) != -1)
                return true;
            
            return false;
        }


        public static ExportType GetExportType(Stream stream)
        {
            const int bufferSize = 1024;
            long originalPosition = stream.Position; // Сохраняем исходную позицию
            try
            {
                // Выделяем буфер на стеке. Это очень быстро и не создает мусора в куче.
                Span<byte> buffer = stackalloc byte[bufferSize];              
                stream.Read(buffer);

                if (IsTexture(buffer))
                    return ExportType.BlueprintClass;

                if (IsBlueprintGeneratedClass(buffer))
                    return ExportType.BlueprintClass;

                if (IsTable(buffer))
                    return ExportType.Table;
        
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

                int index = 0;
                string[] lines = new string[results.Values.Count];
                foreach (var result in results.Values)
                {
                    var line = result.Namespace != string.Empty ? $"{result.Namespace}::" : "";
                    if (ForceQmarksOutput) line += ($"{CSV.EscapeCsvField(result.Key)}{separator}\"{result.Source}\"{separator}{result.Translation}");
                    else line += ($"{CSV.EscapeCsvField(result.Key)}{separator}{CSV.EscapeCsvField(result.Source)}{separator}{CSV.EscapeCsvField(result.Translation)}");         
                    lines[index] = line; index++;
                }

                lines = lines
                .OrderBy(line =>
                {
                    // Извлекаем префикс для сортировки
                    int separatorIndex = line.IndexOf("::");
                    if (separatorIndex >= 0)
                    {
                        return line.Substring(0, separatorIndex);
                    }
                    return line; // Если нет "::", сортируем по всей строке
                })
                .ThenBy(line => line) // Вторичная сортировка по всей строке
                .ToArray();


                foreach (var line in lines)
                    writer.WriteLine(line);

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
