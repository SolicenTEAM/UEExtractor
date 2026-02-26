using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Solicen.GitHub.Updater
{
    public class GitProvider
    {
        private static string GitHubApiUrl = string.Empty;
        public static string UserName = "", RepoName = "";
        public static string FileNameSignature = "UEExtractor"; // Часть имени файла для поиска в релизе

        private static readonly HttpClient _httpClient = new()
        {
            DefaultRequestHeaders = { { "User-Agent", "GitHub-Updater" } }
        };

        public static async Task CheckForUpdatesAsync()
        {
            GitHubApiUrl = $"https://api.github.com/repos/{UserName}/{RepoName}/releases/latest";
            CLI.Console.WriteLine("[Cyan]Checking for updates...");

            try
            {
                var latestRelease = await _httpClient.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);
                if (latestRelease == null || string.IsNullOrEmpty(latestRelease.TagName))
                {
                    CLI.Console.WriteLine("[Yellow]Could not fetch latest release information from GitHub.");
                    return;
                }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var latestVersion = new Version(latestRelease.TagName.TrimStart('v', '.'));

                CLI.Console.WriteLine($"[Cyan]Current version: {currentVersion}");
                CLI.Console.WriteLine($"[Cyan]Latest version:  {latestVersion}");

                if (latestVersion > currentVersion)
                {
                    CLI.Console.WriteLine("[Green]A new version is available! Starting download...");
                    var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.Contains(FileNameSignature) && a.Name.EndsWith(".zip"));

                    if (asset == null)
                    {
                        CLI.Console.WriteLine("[Red]Could not find the release asset (zip archive) to download.");
                        return;
                    }

                    await DownloadAndApplyUpdate(asset);
                }
                else
                {
                    CLI.Console.WriteLine("[Green]You are using the latest version.");
                }
            }
            catch (Exception ex)
            {
                CLI.Console.WriteLine($"[Red]An error occurred while checking for updates: {ex.Message}");
            }
        }

        private static async Task DownloadAndApplyUpdate(GitHubAsset asset)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UEExtractor_Update");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var downloadedZipPath = Path.Combine(tempDir, asset.Name);

            CLI.Console.WriteLine($"[Cyan]Downloading {asset.Name}...");

            // Скачивание с прогрессом
            using (var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadedZipPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    if (totalBytes > 0)
                    {
                        var percentage = (int)((double)downloadedBytes / totalBytes * 100);
                        CLI.Console.Write($"\r[Cyan]Progress: {percentage}%");
                    }
                }
                CLI.Console.WriteLine("\n[Green]Download complete.");
            }

            CLI.Console.WriteLine("[Cyan]Extracting update...");
            ZipFile.ExtractToDirectory(downloadedZipPath, tempDir, true);
            File.Delete(downloadedZipPath);
            CLI.Console.WriteLine("[Green]Extraction complete.");

            // Логика замены файлов и перезапуска
            ApplyUpdate(tempDir);
        }

        private static void ApplyUpdate(string updateDir)
        {
            var appLocation = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe");
            var appDir = Path.GetDirectoryName(appLocation);
            var appExeName = Path.GetFileName(appLocation);
            var batchScriptPath = Path.Combine(Path.GetTempPath(), "update_ueextractor.bat");

            // Создаем .bat файл для замены файлов и перезапуска
            // Он дождется завершения текущего процесса, заменит файлы и запустит новую версию
            string scriptContent = $@"
                                    @echo off
                                    echo Waiting for application to close...
                                    timeout /t 2 /nobreak > nul
                                    echo Replacing files...
                                    move /y ""{updateDir}\\*"" ""{appDir}\\""
                                    echo Cleaning up...
                                    rmdir /s /q ""{updateDir}""
                                    echo Starting new version...
                                    start """" ""{appLocation}""
                                    del ""{batchScriptPath}""
                                    ";
            File.WriteAllText(batchScriptPath, scriptContent);

            CLI.Console.WriteLine("[Cyan]Update is ready. The application will now restart to apply changes.");

            // Запускаем .bat скрипт в новом процессе и выходим
            Process.Start(new ProcessStartInfo(batchScriptPath) { CreateNoWindow = true, UseShellExecute = false });
            Environment.Exit(0);
        }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
