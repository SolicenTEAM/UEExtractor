using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Internationalization;
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
        Console.WriteLine($"UE Version: {UE}");
        Console.WriteLine($"Found {files.Length} files:");
        foreach (var file in files.Take(10))
            Console.WriteLine($"- {Path.GetFileName(file)}");
        if (files.Length > 10) Console.WriteLine("... and more");
        try
        {
            
            _provider = new DefaultFileProvider(gameDirectory, SearchOption.AllDirectories, new VersionContainer(UE));

            LoadCompression();
            LoadAesKey(gameDirectory);
            LoadUsmapFiles(gameDirectory);

            _provider.Initialize();
            _provider.LoadVirtualPaths();
            _provider.Mount();
            _provider.PostMount();

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

    private EGame LoadEngineVersion(string dir)
    {
        var engineFile = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories).FirstOrDefault(x => x.Contains("Engine\\Binaries\\Win64\\")); //CrashReportClient.exe
        if (UE_VER != string.Empty && EngineSpecified)
        {
            var version = $"GAME_{UE_VER}";
            return ParseVersion(version);
        }
        else if (engineFile != null)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(engineFile);
            var version = $"GAME_UE{versionInfo.FileMajorPart}_{versionInfo.ProductMinorPart}";

            if (version == "GAME_UE0_0")
            {
                engineFile = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(x => x.Contains("Binaries\\Win64\\") && !x.Contains("CrashReportClient.exe"));

                Console.WriteLine(engineFile);
                versionInfo = FileVersionInfo.GetVersionInfo(engineFile);
                version = $"GAME_UE{versionInfo.FileMajorPart}_{versionInfo.ProductMinorPart}";
            }

            Console.WriteLine($"UEFile: {engineFile}");
            Console.WriteLine($"UEVersion: {version}");
            return ParseVersion(version);
        }
        return EGame.GAME_UE5_LATEST;
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
            var keyString = File.ReadAllText(aesKeyPath);
            if (!string.IsNullOrEmpty(keyString))
            {
                if (keyString.Length == 66)
                {
                    var key = new FAesKey(keyString);
                    _provider.SubmitKey(new Guid(), key);
                    _isEncrypted = true;
                    Console.WriteLine("AES key loaded successfully");
                }
                else
                {
                    throw new FormatException("Invalid AES key format. Expected 32-character hex string");
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
            }), OodleHelper.OODLE_DLL_NAME).Wait();
        }
        OodleHelper.Initialize(OodleHelper.OODLE_DLL_NAME);
        ZlibHelper.Initialize(ZlibHelper.DLL_NAME);
    }

    private void LoadUsmapFiles(string gameDirectory)
    {
        var usmapFiles = Directory.GetFiles(gameDirectory, "*.usmap", SearchOption.AllDirectories);
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
            Console.WriteLine("Warning: No .usmap files found. Type information may be limited.");
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
        var assets = _provider.Files.Keys.Where(x => validExtensions
        .Any(ext => x.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !x.Contains("Engine/")) // Очищение: работаем только с файлами не из Engine папки.
            .ToList();

        //assets = assets.Where(x => x.Contains("/DT_DialogBoxes.uasset") == true).ToList();

        Console.WriteLine($"Found {assets.Count} assets to process");
        if (assets.Count == 0)
        {
            // Диагностика: какие вообще есть файлы
            Console.WriteLine("Available file extensions:");
            var extensions = _provider.Files.Keys
                .Select(Path.GetExtension)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .Take(20);
            foreach (var ext in extensions)
                Console.WriteLine($"- {ext}");

            throw new InvalidOperationException("No valid assets found. See available extensions above.");
        }

        int totalAssets = assets.Count; 
        int currentIndex = 1;

        Parallel.ForEach(assets, assetPath => 
        {
            try
            {
                Console.WriteLine($"[{currentIndex}/{totalAssets}] ..{assetPath}");
                using var stream = LoadAsset(assetPath);
                currentIndex++;
                processor(assetPath, stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{currentIndex}/{totalAssets}] Error processing {assetPath}: {ex.Message}");
                if (ex is System.Security.SecurityException)
                    Console.WriteLine(">> Possible encryption issue!");
            }
        });
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


    public void LoadDataTable(string assetPath, Action<List<(string Namespace, string Key, string SourceString)>> processor)
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
            //Console.WriteLine(obj.ToString());

            // Проверяем, похож ли объект на структуру FText
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
