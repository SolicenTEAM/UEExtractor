using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using OodleDotNet;
using static CUE4Parse.UE4.Objects.StructUtils.FInstancedPropertyBag;

public class UnrealArchiveReader : IDisposable
{
    public string UE_VER = string.Empty;
    public string AES_KEY = string.Empty;

    private readonly DefaultFileProvider _provider;
    private bool _hasValidFiles;
    private bool _isEncrypted;

    public UnrealArchiveReader(string gameDirectory, string UEVersion = "", string AES = "")
    {
        gameDirectory = Path.GetFullPath(gameDirectory);
        Console.WriteLine($"Loading from: {gameDirectory}");

        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {gameDirectory}");

        // Вывод списка файлов для диагностики
        var files = Directory.GetFiles(gameDirectory, "*.*", SearchOption.AllDirectories)
            .Where(x => x.EndsWith(".pak") || x.EndsWith(".ucas") || x.EndsWith(".utoc"))
            .Where(x => x.Contains("\\Content\\Paks")).ToArray();
        Console.WriteLine($"Found {files.Length} files:");
        foreach (var file in files.Take(10))
            Console.WriteLine($"- {Path.GetFileName(file)}");
        if (files.Length > 10) Console.WriteLine("... and more");

        try
        {
            EGame UE = LoadEngineVersion(gameDirectory);
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
        var engineFile = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).FirstOrDefault(x => x.Contains("Win64\\CrashReportClient.exe"));
        if (UE_VER != string.Empty)
        {
            var version = $"GAME_{UE_VER}";
            return ParseVersion(version);
        }
        else if (engineFile != null)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(engineFile);
            var version = $"GAME_UE{versionInfo.FileMajorPart}_{versionInfo.ProductMinorPart}";
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
        OodleHelper.DownloadOodleDll();
        ZlibHelper.DownloadDll();

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

    public void ProcessAllAssets(Action<string, Stream> processor)
    {
        if (!_hasValidFiles)
            throw new InvalidOperationException("No valid files available for processing");

        // Расширенный список расширений
        var validExtensions = new[] { ".uasset", ".uexp" };
        var assets = _provider.Files.Keys
            .Where(x => validExtensions.Any(ext => x.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

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

        // Очищение: работаем только с файлами не из Engine папки.
        var gameAssets = assets.Where(x => !x.Contains("Engine/")).ToList();
        int totalAssets = gameAssets.Count; int currentIndex = 1;
        
        foreach (var assetPath in gameAssets)
        {
            try
            {
                var byteArray = _provider.SaveAsset(assetPath);
                using var stream = new MemoryStream(byteArray);
                Console.WriteLine($"[{currentIndex}/{totalAssets}] ..{assetPath}");
                currentIndex++;
                processor(assetPath, stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {assetPath}: {ex.Message}");
                if (ex is System.Security.SecurityException)
                    Console.WriteLine(">> Possible encryption issue!");
            }
        }
    }

    public byte[]? GetFileBytes(string filePath)
    {
        if (_provider.Files.TryGetValue(filePath, out _))
        {
            try
            {
                return _provider.SaveAsset(filePath);
            }
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