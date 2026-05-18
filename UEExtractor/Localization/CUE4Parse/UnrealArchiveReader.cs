using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Internationalization;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;

public class UnrealArchiveReader : IDisposable
{

    public string UE_VER = string.Empty;
    public string AES_KEY = string.Empty;

    private readonly DefaultFileProvider _provider;
    private bool _hasValidFiles;
    private bool _isEncrypted;
    private bool _isZenloader = false;
    public static bool EngineSpecified = false;

    public UnrealArchiveReader(string gameDirectory, string VER = "4_24", string AES = "")
    {
        UE_VER = VER;
        gameDirectory = Path.GetFullPath(gameDirectory);
        Console.WriteLine($"Loading from: {gameDirectory}");

        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {gameDirectory}");

        // Вывод списка файлов для диагностики
        string[] allowedExtensions = { ".utoc", ".pak" };
        var files = Directory.GetFiles(gameDirectory, "*.*", SearchOption.AllDirectories)
            .Where(file => allowedExtensions.Any(file.EndsWith))
            .Where(x => x.Contains("\\Content\\Paks")).ToArray();

        EGame UE = LoadEngineVersion(gameDirectory);
        Console.WriteLine($"UE::Version: {UE}");
        Console.WriteLine($"Found {files.Length} files:");
        foreach (var file in files.Take(10))
            Console.WriteLine($"- {Path.GetFileName(file)}");
        if (files.Length > 10) Console.WriteLine("... and more");
        try
        {
            
            _provider = new DefaultFileProvider(gameDirectory, SearchOption.AllDirectories, new VersionContainer(UE));

            LoadCompression();
            LoadUsmapFiles(gameDirectory);

            _provider.Initialize();

            if (Solicen.Localization.UE4.UnrealLocres.VerboseOutput)
            {
                Console.WriteLine($"UnloadedVfs after Initialize: {_provider.UnloadedVfs.Count}");
                Console.WriteLine($"RequiredKeys (encryption GUIDs needed): {_provider.RequiredKeys.Count}");
                foreach (var guid in _provider.RequiredKeys)
                    Console.WriteLine($"  GUID: {guid}");
                Console.WriteLine("Reader details (first 10):");
                foreach (var r in _provider.UnloadedVfs.Take(10))
                    Console.WriteLine($"  [{r.GetType().Name}] {System.IO.Path.GetFileName(r.Name)} | Encrypted={r.IsEncrypted} | HasDirIdx={r.HasDirectoryIndex} | GUID={r.EncryptionKeyGuid}");
            }

            LoadAesKey(gameDirectory);  // must be after Initialize, before Mount

            if (Solicen.Localization.UE4.UnrealLocres.VerboseOutput)
                Console.WriteLine($"UnloadedVfs after SubmitKey: {_provider.UnloadedVfs.Count}");

            int mounted = _provider.Mount();

            if (Solicen.Localization.UE4.UnrealLocres.VerboseOutput)
            {
                Console.WriteLine($"Mount() newly mounted: {mounted}");
                Console.WriteLine($"UnloadedVfs after Mount: {_provider.UnloadedVfs.Count}");
            }

            _provider.LoadVirtualPaths();

            Console.WriteLine($"Provider initialized. Found {_provider.Files.Count} virtual files.");
            ValidateFiles();

            if (_provider.Files.Count == 0)
            {
                // Дополнительная проверка - попытка найти конкретный .pak файл
                var pakFiles = Directory.GetFiles(gameDirectory, "*.pak", SearchOption.AllDirectories);
                if (pakFiles.Length > 0)
                {
                    throw new Exception($"Found {pakFiles.Length} .pak files but couldn't load them. " +
                        "Possible reasons:\n" +
                        "1. Archives are encrypted (need AES key)\n" +
                        "2. Wrong UE version specified\n" +
                        "3. Corrupted archive files");
                }
                else
                {
                    throw new Exception("No .pak files found in directory");
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize file provider: {ex.Message}", ex);
        }
    }

    // Maps normalized folder/exe names to game-specific EGame values.
    // These games require a specific EGame to handle custom pak formats, encryption, or offsets.
    // CUE4Parse automatically configures CustomEncryption for games that need it (e.g. MarvelRivals, DeadByDaylight).
    private static readonly Dictionary<string, EGame> _knownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Neverness To Everness — custom pak index offset (FPakInfo -1)
        ["NevernessToEverness"]    = EGame.GAME_NevernessToEverness,
        ["NevernessToeEverness"]   = EGame.GAME_NevernessToEverness,
        ["HT"]                     = EGame.GAME_NevernessToEverness, // internal name

        // Ash Echoes — custom file provider
        ["AshEchoes"]              = EGame.GAME_AshEchoes,

        // Wuthering Waves — partial encryption pak format
        ["WutheringWaves"]         = EGame.GAME_WutheringWaves,
        ["KuroGames"]              = EGame.GAME_WutheringWaves,

        // inZOI — custom FPakInfo offset
        ["InZOI"]                  = EGame.GAME_InZOI,
        ["inZOI"]                  = EGame.GAME_InZOI,

        // Marvel Rivals — custom encryption (MarvelAes, auto-set by CUE4Parse)
        ["MarvelRivals"]           = EGame.GAME_MarvelRivals,
        ["MarvelsSRivals"]         = EGame.GAME_MarvelRivals,

        // Dead by Daylight — custom encryption (DBDAes, auto-set by CUE4Parse)
        ["DeadByDaylight"]         = EGame.GAME_DeadByDaylight,
        ["DeadbyDaylight"]         = EGame.GAME_DeadByDaylight,

        // FragPunk — custom global IoStore handling
        ["FragPunk"]               = EGame.GAME_FragPunk,

        // Infinity Nikki — custom encryption (InfinityNikkiAes, auto-set by CUE4Parse)
        ["InfinityNikki"]          = EGame.GAME_InfinityNikki,

        // Snowbreak: Containment Zone
        ["Snowbreak"]              = EGame.GAME_Snowbreak,
        ["SnowbreakContainmentZone"] = EGame.GAME_Snowbreak,
    };

    private EGame ParseVersion(string UEVersion)
    {
        if (Enum.TryParse<EGame>(UEVersion, out EGame result))
        {
            return result;
        }
        else
        {
            return EGame.GAME_UE4_LATEST;
        }

    }

    private EGame DetectKnownGame(string dir, EGame fallback)
    {
        // 1. Check folder name (e.g. "Neverness To Everness" → "NevernessToEverness")
        var folderName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar))
                            ?.Replace(" ", "").Replace("-", "").Replace("_", "");
        if (folderName != null && _knownGames.TryGetValue(folderName, out var byFolder))
        {
            Console.WriteLine($"GameType: detected '{folderName}' → {byFolder}");
            return byFolder;
        }

        // 2. Check exe name in top-level directory
        foreach (var exe in Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(exe).Replace("-", "").Replace("_", "");
            if (_knownGames.TryGetValue(name, out var byExe))
            {
                Console.WriteLine($"GameType: detected '{name}.exe' → {byExe}");
                return byExe;
            }
        }

        return fallback;
    }

    private EGame LoadEngineVersion(string dir)
    {
        var engineFile = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories).FirstOrDefault(x => x.Contains("Engine\\Binaries\\Win64\\")); //CrashReportClient.exe
        var mainExecutable = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault(x => x.Contains(".exe"));

        if (UE_VER != string.Empty && EngineSpecified)
        {
            var version = $"GAME_{UE_VER}";
            return ParseVersion(version);
        }
        else if (engineFile != null || mainExecutable != null)
        {
            bool isEngineFile = mainExecutable != null ? false : true;
            var file = isEngineFile ? engineFile : mainExecutable;

            if (FileVersionInfo.GetVersionInfo(file).FileMajorPart < 4)
                file = engineFile;

            var versionInfo = FileVersionInfo.GetVersionInfo(file);
            var version = $"GAME_UE{versionInfo.FileMajorPart}_{versionInfo.ProductMinorPart}";

            if (version == "GAME_UE0_0")
            {
                engineFile = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(x => x.Contains("Binaries\\Win64\\") && !x.Contains("CrashReportClient.exe"));

                Console.WriteLine(engineFile);
                versionInfo = FileVersionInfo.GetVersionInfo(engineFile);
                version = $"GAME_UE{versionInfo.FileMajorPart}_{versionInfo.ProductMinorPart}";
            }

            Console.WriteLine($"UE::File: {engineFile}");
            Console.WriteLine($"UE::Version: {version}");
            return DetectKnownGame(dir, ParseVersion(version));
        }
        return DetectKnownGame(dir, EGame.GAME_UE5_LATEST);
    }

    private void LoadAesKey(string gameDirectory)
    {
        var aesKeyPath = Path.Combine(gameDirectory, "aes.txt");
        if (AES_KEY != string.Empty)
        {
            var key = new FAesKey(AES_KEY);
            _provider.SubmitKey(new Guid(), key);
            _isEncrypted = true;
            Console.WriteLine("AES key loaded successfully");
            return;
        }
        else if (File.Exists(aesKeyPath))
        {
            var keyString = File.ReadAllText(aesKeyPath).Trim();
            if (!string.IsNullOrEmpty(keyString))
            {
                // Normalise: ensure "0x" prefix then check length
                if (!keyString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    keyString = "0x" + keyString;
                if (keyString.Length == 66)
                {
                    var key = new FAesKey(keyString);
                    _provider.SubmitKey(new Guid(), key);
                    _isEncrypted = true;
                    Console.WriteLine($"AES key loaded: {keyString[..8]}...{keyString[^4..]}");
                }
                else
                {
                    throw new FormatException($"Invalid AES key length: {keyString.Length} chars (expected 66). Check aes.txt.");
                }
            }
        }
    }

    private void LoadCompression()
    {
        bool OodleDownloaded = OodleHelper.DownloadOodleDll();
        ZlibHelper.DownloadDll();

        // UPD: 05.01.2026 - Замечена проблема при загрузке Oodle DLL, решение ниже.
        if (!OodleDownloaded)
        {
            OodleHelper.DownloadOodleDllFromOodleUEAsync(
                new HttpClient(new SocketsHttpHandler
            {
                UseProxy = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All
            }), OodleHelper.OODLE_NAME_CURRENT).Wait();
        }
        OodleHelper.Initialize(OodleHelper.OODLE_NAME_CURRENT);
        ZlibHelper.Initialize(ZlibHelper.DLL_NAME);
    }

    static bool IsZenLoader(string path)
    {
        var zenLoaderFile = Directory.GetFiles(path, "*.utoc", SearchOption.AllDirectories);
        return zenLoaderFile.Length > 0 ? true : false;
    }

    private void LoadUsmapFiles(string gameDirectory)
    {
        var usmapFiles = Directory.GetFiles(gameDirectory, "*.usmap", SearchOption.AllDirectories);
        _isZenloader = IsZenLoader(gameDirectory);

        if (usmapFiles.Length > 0)
        {
            foreach (var usmapPath in usmapFiles)
            {
                try
                {
                    var mappings = new FileUsmapTypeMappingsProvider(usmapPath);
                    _provider.MappingsContainer = mappings;
                    Console.WriteLine($"Loaded usmap file: {Path.GetFileName(usmapPath)}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load usmap file {usmapPath}: {ex.Message}");
                }
            }
        }
        else
        {
            if (_isZenloader)
            {
                Console.WriteLine("Danger: ZenLoader was provided, but the mapping file was not found..");
            }
            else
            {
                Console.WriteLine("Warning: No .usmap files found. Type information may be limited.");
            }


        }
    }

    private void ValidateFiles()
    {
        _hasValidFiles = _provider.Files.Count > 0;

        if (!_hasValidFiles)
        {
            if (_isEncrypted)
                throw new InvalidOperationException("No valid files found. Archives may be encrypted with a different AES key.");
            else
                throw new InvalidOperationException("No valid game files found in the specified directory.");
        }
    }

    public void LoadBlueprint(string path, Action<string, Dictionary<string, string>> processor)
    {
        if (path.EndsWith(".uasset"))
        {
            var packageId = Path.ChangeExtension(path, null);
            try
            {
                var package = _provider.LoadPackage(packageId);

                var exports = package.GetExports();
                foreach (var export in exports)
                {
                    Console.WriteLine($" - {export.ExportType} || {export.Name} ");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading blueprint asset {path}: {ex.Message}");
            }
        }
    }



    public void ProcessAllAssets(
        Action<string, Stream> processor,
        IEnumerable<string>? searchStrings = null,
        bool deepParse = true)
    {
        if (!_hasValidFiles)
            throw new InvalidOperationException("No valid files available for processing");

        // Расширенный список расширений
        // UPD: Исключаем uexp, так как uasset и так ссылается на него при загрузке.
        var validExtensions = new[] { ".uasset", ".uexp", ".umap" };
        var filterPath = Solicen.Localization.UE4.UnrealLocres.FilterPath;
        var assets = _provider.Files.Keys.Where(x => validExtensions
            .Any(ext => x.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !x.Contains("Engine/"))
            .Where(x => string.IsNullOrEmpty(filterPath) || x.Contains(filterPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrEmpty(filterPath))
            Console.WriteLine($"Path filter active: \"{filterPath}\"");
        Console.WriteLine($"Found {assets.Count} assets to process");
        if (assets.Count == 0)
        {
            var allKeys = _provider.Files.Keys.ToList();
            var extensions = allKeys
                .Select(Path.GetExtension)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .Take(20)
                .ToList();

            Console.WriteLine("Available file extensions:");
            foreach (var ext in extensions)
                Console.WriteLine($"- {ext}");

            Console.WriteLine("\nSample virtual paths (first 10):");
            foreach (var path in allKeys.Take(10))
                Console.WriteLine($"  {path}");

            bool onlyBinIni = extensions.All(e => e == ".bin" || e == ".ini");
            if (onlyBinIni)
            {
                Console.WriteLine(
                    "\n[HINT] Only .bin/.ini files are visible. This usually means the IoStore (.ucas) " +
                    "containers could not be mounted, likely due to a wrong UE version flag.\n" +
                    "Try removing the -v flag to auto-detect the version, or use the correct version " +
                    "(e.g. -v=UE5_6 for a UE 5.6 game).");
            }

            // When a path filter is active the folder may contain only .locres files (e.g.
            // HT/Content/Localization). Don't abort — ProcessLocresFiles will handle them.
            if (!string.IsNullOrEmpty(filterPath))
            {
                Console.WriteLine("No .uasset/.uexp files under the specified path. Will try .locres files.");
                return;
            }

            throw new InvalidOperationException("No valid assets found. See available extensions above.");
        }

        int totalAssets = assets.Count;
        int processed = 0;
        int errors = 0;
        bool verbose = Solicen.Localization.UE4.UnrealLocres.VerboseOutput;

        void PrintProgress()
        {
            int done = Volatile.Read(ref processed);
            int pct = totalAssets > 0 ? (int)((long)done * 100 / totalAssets) : 100;
            int barWidth = 30;
            int filled = barWidth * pct / 100;
            var bar = new string('█', filled) + new string('░', barWidth - filled);
            Console.Write($"\r  [{bar}] {pct,3}%  ({done}/{totalAssets})   ");
        }

        Parallel.ForEach(assets, assetPath =>
        {
            try
            {
                using var stream = LoadAsset(assetPath);
                processor(assetPath, stream);
                Interlocked.Increment(ref processed);
                if (!verbose) PrintProgress();
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref processed);
                Interlocked.Increment(ref errors);
                if (verbose)
                {
                    Console.WriteLine($"[{processed}/{totalAssets}] Error processing {assetPath}: {ex.Message}");
                    if (ex is System.Security.SecurityException)
                        Console.WriteLine(">> Possible encryption issue!");
                }
                else
                {
                    PrintProgress();
                }
            }
        });

        Console.WriteLine(); // newline after progress bar
        if (errors > 0)
            Console.WriteLine($"  Completed with {errors} error(s).");
    }

    // Reads .locres files directly from the virtual filesystem and calls processor with
    // (namespace, key, localizedString) triples. Handles game-specific encryption automatically
    // (e.g. NTE's encrypted locres via FNTEFTextLocalizationResource inside CUE4Parse).
    public void ProcessLocresFiles(
        Action<string, string, string> processor,
        string? pathFilter = null)
    {
        if (!_hasValidFiles)
            throw new InvalidOperationException("No valid files available for processing");

        var locresFiles = _provider.Files.Keys
            .Where(x => x.EndsWith(".locres", StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrEmpty(pathFilter) || x.Contains(pathFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrEmpty(pathFilter))
            Console.WriteLine($"Path filter active: \"{pathFilter}\"");
        Console.WriteLine($"Found {locresFiles.Count} .locres files to process");

        if (locresFiles.Count == 0) return;

        int processed = 0;
        int errors = 0;
        bool verbose = Solicen.Localization.UE4.UnrealLocres.VerboseOutput;
        int totalFiles = locresFiles.Count;

        void PrintProgress()
        {
            int done = Volatile.Read(ref processed);
            int pct = totalFiles > 0 ? (int)((long)done * 100 / totalFiles) : 100;
            int barWidth = 30;
            int filled = barWidth * pct / 100;
            var bar = new string('█', filled) + new string('░', barWidth - filled);
            Console.Write($"\r  [{bar}] {pct,3}%  ({done}/{totalFiles})   ");
        }

        foreach (var locresPath in locresFiles)
        {
            try
            {
                if (verbose) Console.WriteLine($"Reading: {locresPath}");
                using var ar = _provider.CreateReader(locresPath);
                var locres = new FTextLocalizationResource(ar);
                foreach (var (nsKey, entries) in locres.Entries)
                {
                    foreach (var (textKey, entry) in entries)
                    {
                        if (!string.IsNullOrEmpty(entry.LocalizedString))
                            processor(nsKey.Str, textKey.Str, entry.LocalizedString);
                    }
                }
                Interlocked.Increment(ref processed);
                if (!verbose) PrintProgress();
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref processed);
                Interlocked.Increment(ref errors);
                if (verbose)
                    Console.WriteLine($"Error reading {locresPath}: {ex.Message}");
                else
                    PrintProgress();
            }
        }

        Console.WriteLine();
        if (errors > 0)
            Console.WriteLine($"  Completed with {errors} error(s).");
    }


    public void LoadStringTable(string path, Action<string, Dictionary<string, string>> processor)
    {
        if (path.EndsWith(".uasset"))
        {
            var packageid = Path.ChangeExtension(path, null);
            if (_provider.TryLoadPackageObject<UStringTable>(packageid, out var st))
            {
                Dictionary<string, string> keys = new Dictionary<string, string>();
                foreach (var entry in st.StringTable.KeysToEntries)
                {
                    keys.Add(entry.Key, entry.Value);
                }

                processor(st.StringTable.TableNamespace, keys);
            }
        }
    }


    public void GetLocalizedStrings(string assetPath, Action<List<(string Namespace, string Key, string SourceString)>> processor)
    {
        var results = new List<(string Namespace, string Key, string SourceString)>();
        var asset = _provider.LoadPackage(assetPath);
        // Сериализуем все экспорты пакета в JSON
        var exports = asset.GetExports();
        var json = JsonConvert.SerializeObject(exports);
        try
        {
            var jToken = JToken.Parse(json);
            ProcessJsonToken(jToken, results);
        }
        catch (Newtonsoft.Json.JsonReaderException ex)
        {
            Console.WriteLine($"Error parsing JSON for asset {assetPath}: {ex.Message}");
        }
        json = string.Empty;
        processor(results);
    }

    private void ProcessJsonToken(JToken token, List<(string Namespace, string Key, string SourceString)> results)
    {
        if (token is JObject obj)
        {
            // Проверяем, похож ли объект на структуру FText (TextProperty)
            // {"Namespace": "", "Key": "...", "SourceString": "..."}
            if (obj.TryGetValue("SourceString", out var sourceStringToken) &&
                obj.TryGetValue("Key", out var keyToken) &&
                obj.TryGetValue("Namespace", out var nameToken))
                {
                    var key = keyToken.ToString();
                    var sourceString = sourceStringToken.ToString();
                    var Namespace = nameToken.ToString();

                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(sourceString))
                    {
                        results.Add((Namespace, key, sourceString));
                    }
                }

            // Рекурсивный обход всех свойств объекта
            foreach (var property in obj.Properties())
            {
                ProcessJsonToken(property.Value, results);
            }
        }
        else if (token is JArray array)
        {
            // Рекурсивный обход всех элементов массива
            foreach (var item in array)
            {
                ProcessJsonToken(item, results);
            }
        }
    }

    public MemoryStream LoadAsset(string assetPath)
    {
        var byteArray = _provider.SaveAsset(assetPath);
        return new MemoryStream(byteArray);
    }

    public byte[]? GetFileBytes(string filePath)
    {
        if (_provider.Files.TryGetValue(filePath, out _))
        {
            try { return _provider.SaveAsset(filePath); }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
                return null;
            }
        } 
        return null;
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
