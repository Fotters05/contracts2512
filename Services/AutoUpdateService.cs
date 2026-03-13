using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Octokit;

namespace Contract2512.Services
{
    /// <summary>
    /// Сервис автоматического обновления приложения через GitHub Releases
    /// </summary>
    public class AutoUpdateService
    {
        private readonly string _githubOwner;
        private readonly string _githubRepo;
        private readonly string _githubToken; // Токен для приватного репозитория
        private readonly string _currentVersion;
        private readonly HttpClient _httpClient;

        public AutoUpdateService(string githubOwner, string githubRepo, string githubToken = null)
        {
            _githubOwner = githubOwner;
            _githubRepo = githubRepo;
            _githubToken = githubToken;
            _currentVersion = GetCurrentVersion();
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Получает текущую версию приложения
        /// </summary>
        private string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        /// <summary>
        /// Проверяет наличие обновлений на GitHub
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("Contract2512"));
                
                // Если есть токен для приватного репозитория, используем его
                if (!string.IsNullOrEmpty(_githubToken))
                {
                    github.Credentials = new Credentials(_githubToken);
                }
                
                var releases = await github.Repository.Release.GetAll(_githubOwner, _githubRepo);

                if (releases.Count == 0)
                {
                    return new UpdateInfo { HasUpdate = false };
                }

                var latestRelease = releases[0];
                var latestVersion = latestRelease.TagName.TrimStart('v');

                if (IsNewerVersion(latestVersion, _currentVersion))
                {
                    // Ищем .exe файл в assets
                    string downloadUrl = null;
                    foreach (var asset in latestRelease.Assets)
                    {
                        if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.BrowserDownloadUrl;
                            break;
                        }
                    }

                    return new UpdateInfo
                    {
                        HasUpdate = true,
                        Version = latestVersion,
                        ReleaseNotes = latestRelease.Body ?? "Нет описания изменений",
                        DownloadUrl = downloadUrl,
                        PublishedAt = latestRelease.PublishedAt?.DateTime
                    };
                }

                return new UpdateInfo { HasUpdate = false };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
                return new UpdateInfo { HasUpdate = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Скачивает и устанавливает обновление
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<int> progress = null)
        {
            try
            {
                // Путь для временного файла
                var tempPath = Path.Combine(Path.GetTempPath(), "Contract2512_Update.exe");
                
                // Добавляем токен в заголовки для приватного репозитория
                if (!string.IsNullOrEmpty(_githubToken))
                {
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_githubToken}");
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Contract2512");
                }
                
                // Скачиваем файл
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;

                            if (totalBytes > 0)
                            {
                                var progressPercentage = (int)((downloadedBytes * 100) / totalBytes);
                                progress?.Report(progressPercentage);
                            }
                        }
                    }
                }

                // Создаем batch скрипт для замены файла и перезапуска
                var currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                var batchPath = Path.Combine(Path.GetTempPath(), "update.bat");

                var batchContent = $@"@echo off
timeout /t 2 /nobreak > nul
taskkill /F /IM Contract2512.exe > nul 2>&1
timeout /t 1 /nobreak > nul
move /Y ""{tempPath}"" ""{currentExePath}""
start """" ""{currentExePath}""
del ""%~f0""
";

                File.WriteAllText(batchPath, batchContent);

                // Запускаем batch скрипт
                var processInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process.Start(processInfo);

                // Закрываем приложение
                System.Windows.Application.Current.Shutdown();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка установки обновления: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Сравнивает версии (например: "1.2.3" > "1.2.2")
        /// </summary>
        private bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                var newParts = newVersion.Split('.');
                var currentParts = currentVersion.Split('.');

                for (int i = 0; i < Math.Min(newParts.Length, currentParts.Length); i++)
                {
                    if (int.TryParse(newParts[i], out int newPart) &&
                        int.TryParse(currentParts[i], out int currentPart))
                    {
                        if (newPart > currentPart) return true;
                        if (newPart < currentPart) return false;
                    }
                }

                return newParts.Length > currentParts.Length;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Информация об обновлении
    /// </summary>
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string Version { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string Error { get; set; }
    }
}
