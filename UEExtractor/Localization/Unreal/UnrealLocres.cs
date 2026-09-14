using Solicen.Translator;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Solicen.Localization.UE4
{
    public class UnrealLocres
    {
        public string errorParseText = "UE4 error while parse folder to create CSV Locres file.";
        public static string FilePATH = string.Empty;
        public static string UEVersion = "4_24";
        public static string AES = string.Empty;
        public static string SearchKeyName = string.Empty;
        public static string SearchText = string.Empty;

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
        public static bool SkipUnderscore = false;
        public static bool SkipUppercase = false;
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

        private static Dictionary<string, string> SearchedText = new Dictionary<string, string>();

        #region Duplicates Zone
        /// <summary>
        /// It is used to count the found duplicate keys with different source values, only for values without Namespace.
        /// </summary>
        private static List<LocresResult> DuplicatesCollection = new List<LocresResult>();
        #endregion

        public enum ExportType  { 
            None, TextProperty, StringTable, DataTable, AnyTable, BlueprintGeneratedClass
        }
        public static string[] ExcludePath = { "/sound/", "/effects/", "/fx/", "/vfx/", "/meshes/", "/mesh/", "/textures/", "/megascans/", "/music/"};
        public static bool EngineSpecified = false;
        public static bool ExtractLocres = false;
        public static bool ReadAllLocres = false;
        public static bool AllFolders = false;
        public static bool PickyMode = false;
        public static bool IncludeUrlInKeyValue  = false;
        public static bool IncludeHashInKeyValue = false;
        public static string pDirectory = string.Empty;
        public static bool TableSeparator = false;
        public static bool VerboseOutput = false;
        public static string FilterPath = string.Empty;
        public static string GetUasset(string path) => Path.ChangeExtension(path, ".uasset");
        
        public static ConcurrentDictionary<string, LocresResult> ProcessDirectory(string directory)
        {
            var allResults = new ConcurrentDictionary<string, LocresResult>();
            pDirectory = directory;

            using var reader = new UnrealArchiveReader(directory, UEVersion);
            reader.ProcessAllAssets((path, stream) =>
            {
                if (path.EndsWith(".locres")) return; // Для локресов своя логика
                if (!AllFolders && ExcludePath.Any(x => path.ToLower().Contains(x))) return;
                if (SkipUassetFile && path.EndsWith(".uasset")) return;
                if (SkipUexpFile && path.EndsWith(".uexp")) return;

                List<LocresResult> fileResults = new List<LocresResult>();
                var eType = GetExportType(stream);
                try
                {
                    switch (eType)
                    {
                        case ExportType.AnyTable:
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
                                    reader.GetLocalizedStrings(GetUasset(path), (Table) =>
                                    {
                                        newResult = LocalizedToLocres(Table);
                                        fileResults.AddRange(newResult);
                                    });
                                }
                            }
                            break;
                        case ExportType.TextProperty:
                            {
                                // Содержит BlueprintGeneratedClass (Kismet String)
                                // Может содержать TextProperty (для locres)
                                var newResult = new LocresResult[0];
                                reader.GetLocalizedStrings(GetUasset(path), (Table) =>
                                {
                                    newResult = LocalizedToLocres(Table);
                                    fileResults.AddRange(newResult);
                                });
                                break;
                            }
                        default:
                            {
                                fileResults = UnrealParser.Parse(stream.ToArray());
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERR] Error while processing {path}: {ex.Message}");
                    SkippedCSV.WriteLine($"[ERR] Error while processing {path}: {ex.Message}");
                }
                finally
                {
                    stream.Close();
                }

                #region Zero Data
                if (fileResults.Count == 0 && PickyMode) ZeroDataMessage();
                #endregion

                // Pre-apply key decorations and build an O(1) duplicate index
                // (replaces per-entry List.Find which was O(n²) per file).
                foreach (var r in fileResults)
                {
                    if (r == null) continue;
                    if (UnrealLocres.IncludeHashInKeyValue) r.Key = $"[{r.Key}][{r.Hash}]";
                    if (UnrealLocres.IncludeUrlInKeyValue) r.Key = $"[{r.Url}]{r.Key}";
                }
                var keyFirstSource = new Dictionary<string, string>(fileResults.Count);
                foreach (var r in fileResults)
                {
                    if (r == null || keyFirstSource.ContainsKey(r.Key)) continue;
                    keyFirstSource[r.Key] = r.Source;
                }

                foreach (var result in fileResults)
                {
                    if (result == null) continue;
                    #region Skip if the conditions match
                    if (UnrealLocres.SkipUnderscore && result.Source.Contains("_")) continue;
                    if (UnrealLocres.SkipUppercase && result.Source.IsUpper()) continue;
                    #endregion
                    
                    if (UnrealLocres.SearchText != string.Empty && result.Source.Contains(SearchText))
                    {
                        SearchedText.Add(path, result.Source);
                    }

                    var outputValue = result.Namespace != string.Empty ?
                    $"\t{result.Namespace}::{result.Key}\t{result.Source}\t" : $"\t{result.Key}\t{result.Source}\t";
                    if (VerboseOutput) Console.WriteLine(outputValue);

                    #region Checking duplicates
                    if (keyFirstSource.TryGetValue(result.Key, out var firstSource) && firstSource != result.Source)
                    {
                        DuplicatesCollection.Add(result);
                        continue; // If we found duplicate then skip and write to collection of duplicates.
                    }
                    #endregion
                    #region Checking UE Developer Strings
                    if (result.Namespace == "UMG") continue;
                    if (result.Namespace.StartsWith("UnrealEd")) continue;
                    #endregion

                    if (!string.IsNullOrWhiteSpace(result.Namespace))
                        result.Key = $"{result.Namespace}::{result.Key}";
                    allResults[result.Key] = result;
                }
            });


            if (ReadAllLocres)
            {
                // Also read .locres files directly (handles games like NTE whose localization
                // is stored as pre-compiled .locres binaries rather than inside .uasset files).
                // Use hash-aware variant to preserve game-computed StrHash values for v3 round-trips.

                reader.ProcessLocresFilesWithHashes((ns, nsHash, key, keyHash, localizedString) =>
                {
                    if (string.IsNullOrWhiteSpace(localizedString)) return;
                    var compositeKey = ns != string.Empty ? $"{ns}::{key}" : key;
                    if (!allResults.ContainsKey(compositeKey))
                    {
                        var r = new LocresResult(compositeKey, localizedString.Escape(), Namespace: ns);
                        r.NsHash = nsHash;
                        r.KeyHash = keyHash;
                        allResults[compositeKey] = r;
                    }
                }, string.IsNullOrEmpty(FilterPath) ? null : FilterPath);
            }
            
            #region Warning Messages
            if (allResults.Count == 0) ZeroDataMessage();
            if (DuplicatesCollection.Count > 0) DuplicatesFoundMessage();
            #endregion

            // Filter in-place to avoid materializing a second full copy of all results.
            foreach (var kv in allResults)
            {
                var isNotAllowed = IsNotAllowedString(kv.Value.Source);
                if (isNotAllowed)
                    allResults.TryRemove(kv.Key, out _);
            }

            GC.Collect(2);
            if (SearchedText.Count > 0)
            {
                CLI.Console.Separator();
                foreach (var key in SearchedText)
                {
                    CLI.Console.WriteLine($"[Green]\tText found: '{key.Value}'\n\t[Yellow][{key.Key}]");
                }
                CLI.Console.Separator();
            }

            return allResults;
        }

        // Callback-based: delivers each locres group to the consumer as soon as it is
        // fully parsed, then drops its references — the consumer must persist (write CSV)
        // inside the callback so peak memory stays at one locres.
        public static bool ProcessLocresGroupedStreamed(string directory,
            Action<string, ConcurrentDictionary<string, LocresResult>> onGroup,
            string? extractDirectory = null)
        {
            pDirectory = directory;
            bool produced = false;
            ConcurrentDictionary<string, LocresResult>? current = null;
            string baseNameNow = string.Empty;

            using var reader = new UnrealArchiveReader(directory, UEVersion);
            reader.ReadLocresGrouped(
                beginGroup: baseName =>
                {
                    current = new ConcurrentDictionary<string, LocresResult>();
                    baseNameNow = baseName;
                },
                addEntry: (ns, nsHash, key, keyHash, value) =>
                {
                    if (string.IsNullOrEmpty(key)) return; // skip malformed entries only
                    var compositeKey = ns != string.Empty ? $"{ns}::{key}" : key;
                    var escaped = string.IsNullOrEmpty(value) ? string.Empty : LocresHelper.EscapeKey(value);
                    var r = new LocresResult(compositeKey, escaped, Namespace: ns)
                        { NsHash = nsHash, KeyHash = keyHash };
                    current!.TryAdd(compositeKey, r);
                },
                endGroup: () =>
                {
                    var dict = current!;
                    if (dict.Count == 0) return;
                    onGroup(baseNameNow, dict);
                    produced = true;
                },
                pathFilter: string.IsNullOrEmpty(FilterPath) ? null : FilterPath,
                extractDirectory: extractDirectory);

            return produced;
        }

        // Processes .locres files grouped by source file, returning one result dict per locres.
        // Used when the output path is a directory so each locres gets its own CSV.
        // Entries are streamed from the reader and accumulated directly into per-locres
        // dictionaries — no intermediate entry lists, so peak memory stays low.
        public static List<(string CsvBaseName, ConcurrentDictionary<string, LocresResult> Results)>
            ProcessLocresGrouped(string directory, string? extractDirectory = null)
        {
            var groups = new List<(string CsvBaseName, ConcurrentDictionary<string, LocresResult> Results)>();
            ProcessLocresGroupedStreamed(directory,
                (baseName, dict) => groups.Add((baseName, dict)), extractDirectory);
            return groups;
        }

        public static bool IsNotAllowedString(string value)
        {
            return (
                   value == "TextBlockDefaultValue"
                || string.IsNullOrWhiteSpace(value)
                || value.Trim().Length < 2
                || value == "None"
                || value.IsGUID()
                || value.IsAllNumber()
                || value.IsAllDot()
                || value.IsBoolean()
                || value.IsPath()
                || value.IsAllSame()
                || value.IsStringDigit());
        }

        // Ищем "BlueprintGeneratedClass" //
        public static bool IsBlueprintGeneratedClass(Span<byte> buffer)
        {
            if (BinaryParser.FindSequence(buffer, 
                UnrealFormat.Blueprint.BlueprintGeneratedClass) != -1)
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

        public static bool IsSound(Span<byte> buffer)
        {
            if (BinaryParser.FindSequence(buffer,
                UnrealFormat.Sound.SoundWave) != -1
                ||
                BinaryParser.FindSequence(buffer,
                UnrealFormat.Sound.libVorbis) != -1
                ||
                BinaryParser.FindSequence(buffer,
                UnrealFormat.Sound.LipSyncFrameSequence) != -1)
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

                if (IsTextProperty(buffer))
                    return ExportType.TextProperty;

                if (IsTable(buffer))
                    return ExportType.AnyTable;

                return ExportType.None;
            }
            finally
            {
                stream.Position = originalPosition; // Всегда восстанавливаем исходную позицию потока
            }
        }


        public static byte[] StreamToByte(Stream stream)
        {
            long originalPosition = stream.Position; 
            Span<byte> buffer = stackalloc byte[(int)stream.Length];
            stream.Read(buffer);
            stream.Position = originalPosition; 
            return buffer.ToArray();

        }
 
        public static LocresResult[] LocalizedToLocres(List<(string Namespace, string Key, string SourceString)> results)
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
                    var line = string.Empty;
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

                    var firstSep = _keyNamespace.IndexOf("::");
                    var _Namespace = firstSep >= 0 ? _keyNamespace[..firstSep] : string.Empty;
                    var _Key = _keyNamespace; // preserve full composite key for locres round-trip

                    var _Source = row.Columns[1];
                    var _Translation = row.Columns[2];

                    var res = new LocresResult(_Key, _Source, _Translation, _Namespace);
                    result.Add(res);
                }
                

            });
            return result.ToArray();
        }

        // Sidecar format: JSON object mapping compositeKey → [nsHash, keyHash]
        public static string HashSidecarPath(string csvPath)
            => Path.ChangeExtension(csvPath, ".locreshashes");

        public static void SaveHashSidecar(string csvPath, IEnumerable<LocresResult> results)
        {
            var path = HashSidecarPath(csvPath);
            var dict = new Dictionary<string, uint[]>();
            foreach (var r in results)
            {
                if (r.NsHash == 0 && r.KeyHash == 0) continue;
                // r.Key is already the full composite key (e.g. "Adler_SkillDes::adddes1")
                dict[r.Key] = new[] { r.NsHash, r.KeyHash };
            }
            if (dict.Count == 0) return;
            File.WriteAllText(path, JsonSerializer.Serialize(dict), Encoding.UTF8);
            Solicen.CLI.Console.WriteLine($"[DarkGray][Locres] Hash sidecar saved: {Path.GetFileName(path)} ({dict.Count} entries)");
        }

        public static Dictionary<string, (uint NsHash, uint KeyHash)> LoadHashSidecar(string csvPath)
        {
            var path = HashSidecarPath(csvPath);
            var result = new Dictionary<string, (uint, uint)>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;
            try
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, uint[]>>(File.ReadAllText(path));
                if (raw == null) return result;
                foreach (var (k, v) in raw)
                    if (v.Length >= 2)
                        result[k] = (v[0], v[1]);
                Solicen.CLI.Console.WriteLine($"[DarkGray][Locres] Hash sidecar loaded: {Path.GetFileName(path)} ({result.Count} entries)");
            }
            catch (Exception ex)
            {
                Solicen.CLI.Console.WriteLine($"[Yellow][Locres] Could not load hash sidecar: {ex.Message}");
            }
            return result;
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

        // journalPath: if set, completed batch pairs are appended to this file so a restart
        // can skip already-translated strings without rewriting the full CSV each batch.
        public static void ProcessTranslator(ref LocresResult[] locres,
            Action<List<(string Source, string Translation)>>? onBatchComplete = null,
            string? journalPath = null)
        {
            // Pre-load journal so already-translated sources are skipped
            if (journalPath != null && File.Exists(journalPath))
            {
                var cached = LoadJournal(journalPath);
                foreach (var entry in locres)
                    if (string.IsNullOrWhiteSpace(entry.Translation) && cached.TryGetValue(entry.Source, out var t))
                        entry.Translation = t;
            }

            var manager = new UberTranslator();
            var allValues = locres.GetUnique()
                .Where(x => !string.IsNullOrWhiteSpace(x.Source))
                .Where(x => string.IsNullOrWhiteSpace(x.Translation))
                .ToDictionary(x => x.Source, x => x.Translation);

            if (allValues.Count > 0)
            {
                var locresRef = locres;
                manager.TranslateLines(ref allValues, onBatchComplete: pairs =>
                {
                    // Apply to in-memory array
                    var map = pairs.ToDictionary(p => p.Source, p => p.Translation);
                    locresRef.ReplaceAll(map);
                    // Append to journal (fast, no full-file rewrite)
                    if (journalPath != null) AppendJournal(journalPath, pairs);
                    onBatchComplete?.Invoke(pairs);
                });
                locres.ReplaceAll(allValues);
            }
        }

        private static Dictionary<string, string> LoadJournal(string path)
        {
            var dict = new Dictionary<string, string>();
            foreach (var line in File.ReadLines(path))
            {
                var tab = line.IndexOf('\t');
                if (tab > 0) dict[line[..tab]] = line[(tab + 1)..];
            }
            return dict;
        }

        private static readonly object _journalLock = new();
        private static void AppendJournal(string path, List<(string Source, string Translation)> pairs)
        {
            lock (_journalLock)
            using (var sw = new StreamWriter(path, append: true, System.Text.Encoding.UTF8))
                foreach (var (src, tgt) in pairs)
                    sw.WriteLine($"{src}\t{tgt}");
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
        private static void DuplicatesFoundMessage()
        {
            var dublicatesListMsg = new StringBuilder();
            foreach (var d in DuplicatesCollection.OrderBy(x => x.Key).ToArray())
            {
                var lFormat = d.Namespace != string.Empty ?
                    $" {d.Namespace}::{d.Key}     | {d.Source}\t" : $" {d.Key}     | {d.Source}\t";
                if (lFormat == string.Empty) continue;
                dublicatesListMsg.AppendLine(lFormat);
            }
            CLI.Console.WriteLine(
                $"[DarkYellow][WRN][White] Found ({DuplicatesCollection.Count}) duplicate key(s).\n" +
                " [Red]─[White] These lines will not be in the final result.\n" + 
                " [Green]>[White] Tip: Use UAssetGUI or KissE to replace the strings directly in asset (uasset/uexp).\n" +
                " [Green]>[White] It's the only choice for handling duplicate keys without developer changes.");
            CLI.Console.Separator();
            CLI.Console.WriteLine(" Duplicate Key                        | SourceString", ConsoleColor.DarkGray);
            CLI.Console.Separator();
            CLI.Console.WriteLine($"{dublicatesListMsg}", ConsoleColor.DarkGray);
            CLI.Console.Separator();
            CLI.Console.WriteLine();
        }
        #endregion
    }
}
