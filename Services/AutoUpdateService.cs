using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Contract2512.Services
{
    public class AutoUpdateService
    {
        private readonly string _repoUrl;
        private readonly string _accessToken;
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Contract2512",
            "update_log.txt"
        );

        public AutoUpdateService(string repoUrl, string accessToken = null)
        {
            _repoUrl = repoUrl;
            _accessToken = accessToken;
        }

        private static void Log(string message)
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir!);
                }
                
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(LogFilePath, $"[{timestamp}] {message}\n");
                Debug.WriteLine(message);
            }
            catch
            {
                // Игнорируем ошибки логирования
            }
        }

        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                Log($"🔍 Checking updates: {_repoUrl}");
                Log($"🔍 Access token: {(_accessToken != null ? "***" : "none")}");

                // КРИТИЧНО: Используем Location сборки, а не BaseDirectory
                // Squirrel создает stub exe в корне, а реальное приложение в app-X.X.X
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var dllPath = Path.Combine(assemblyDir, "SquirrelLib.dll");
                
                Log($"🔍 Assembly location: {assemblyLocation}");
                Log($"🔍 Assembly directory: {assemblyDir}");
                Log($"🔍 Looking for DLL at: {dllPath}");
                Log($"🔍 DLL exists: {File.Exists(dllPath)}");
                
                if (!File.Exists(dllPath))
                {
                    Log("❌ SquirrelLib.dll not found");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        Error = $"SquirrelLib.dll not found at: {dllPath}",
                        CurrentVersion = GetCurrentVersion()
                    };
                }

                // Загружаем Squirrel через рефлексию чтобы избежать проблем с WPF временными проектами
                Log($"🔄 Loading assembly from: {dllPath}");
                var squirrelAssembly = Assembly.LoadFrom(dllPath);
                Log($"✅ Assembly loaded: {squirrelAssembly.FullName}");
                
                // Используем GithubUpdateManager вместо UpdateManager
                var githubUpdateManagerType = squirrelAssembly.GetType("Squirrel.GithubUpdateManager");
                
                if (githubUpdateManagerType == null)
                {
                    Log("⚠️ GithubUpdateManager type not found");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        Error = "GithubUpdateManager type not found in SquirrelLib",
                        CurrentVersion = GetCurrentVersion()
                    };
                }

                // Создаём GithubUpdateManager через рефлексию
                // Constructor(String repoUrl, Boolean prerelease, String accessToken, String applicationIdOverride, String localAppDataDirectoryOverride, IFileDownloader urlDownloader)
                Log($"🔧 Creating GithubUpdateManager with repoUrl={_repoUrl}, prerelease=false, accessToken={(_accessToken != null ? "***" : "null")}");
                var mgr = Activator.CreateInstance(githubUpdateManagerType, _repoUrl, false, _accessToken, null, null, null);
                
                if (mgr == null)
                {
                    Log("⚠️ Failed to create GithubUpdateManager");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        Error = "Failed to create GithubUpdateManager",
                        CurrentVersion = GetCurrentVersion()
                    };
                }

                try
                {
                    // Вызываем CheckForUpdate через рефлексию
                    var checkMethod = githubUpdateManagerType.GetMethod("CheckForUpdate");
                    var updateTask = checkMethod?.Invoke(mgr, null) as Task<object>;
                    
                    if (updateTask == null)
                    {
                        Log("⚠️ CheckForUpdate method not found");
                        return new UpdateInfo
                        {
                            HasUpdate = false,
                            Error = "CheckForUpdate method not found",
                            CurrentVersion = GetCurrentVersion()
                        };
                    }

                    var update = await updateTask;

                    // Получаем ReleasesToApply через рефлексию
                    var releasesToApplyProp = update.GetType().GetProperty("ReleasesToApply");
                    var releasesToApply = releasesToApplyProp?.GetValue(update) as System.Collections.IList;

                    if (releasesToApply != null && releasesToApply.Count > 0)
                    {
                        // Получаем FutureReleaseEntry через рефлексию
                        var futureReleaseProp = update.GetType().GetProperty("FutureReleaseEntry");
                        var futureRelease = futureReleaseProp?.GetValue(update);
                        
                        if (futureRelease != null)
                        {
                            var versionProp = futureRelease.GetType().GetProperty("Version");
                            var version = versionProp?.GetValue(futureRelease);
                            var newVersion = version?.ToString() ?? "Unknown";

                            Log($"✅ Update found: {newVersion}");

                            return new UpdateInfo
                            {
                                HasUpdate = true,
                                Version = newVersion,
                                CurrentVersion = GetCurrentVersion(),
                                ReleaseNotes = "Доступна новая версия приложения"
                            };
                        }
                    }

                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        CurrentVersion = GetCurrentVersion()
                    };
                }
                finally
                {
                    // Освобождаем ресурсы
                    if (mgr is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Update error: {ex.Message}");

                return new UpdateInfo
                {
                    HasUpdate = false,
                    Error = ex.Message,
                    CurrentVersion = GetCurrentVersion()
                };
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(IProgress<int>? progress = null)
        {
            try
            {
                // КРИТИЧНО: Используем Location сборки, а не BaseDirectory
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var dllPath = Path.Combine(assemblyDir, "SquirrelLib.dll");
                
                var squirrelAssembly = Assembly.LoadFrom(dllPath);
                var githubUpdateManagerType = squirrelAssembly.GetType("Squirrel.GithubUpdateManager");
                
                if (githubUpdateManagerType == null)
                {
                    Log("⚠️ GithubUpdateManager type not found");
                    return false;
                }

                // Constructor(String repoUrl, Boolean prerelease, String accessToken, String applicationIdOverride, String localAppDataDirectoryOverride, IFileDownloader urlDownloader)
                Log($"🔧 Creating GithubUpdateManager for download with repoUrl={_repoUrl}, prerelease=false, accessToken={(_accessToken != null ? "***" : "null")}");
                var mgr = Activator.CreateInstance(githubUpdateManagerType, _repoUrl, false, _accessToken, null, null, null);
                
                if (mgr == null)
                {
                    Log("⚠️ Failed to create UpdateManager");
                    return false;
                }

                try
                {
                    // CheckForUpdate
                    var checkMethod = githubUpdateManagerType.GetMethod("CheckForUpdate");
                    var updateTask = checkMethod?.Invoke(mgr, null) as Task<object>;
                    
                    if (updateTask == null)
                        return false;

                    var update = await updateTask;

                    // Получаем ReleasesToApply
                    var releasesToApplyProp = update.GetType().GetProperty("ReleasesToApply");
                    var releasesToApply = releasesToApplyProp?.GetValue(update) as System.Collections.IList;

                    if (releasesToApply == null || releasesToApply.Count == 0)
                        return false;

                    // DownloadReleases
                    var downloadMethod = githubUpdateManagerType.GetMethod("DownloadReleases");
                    
                    if (progress != null)
                    {
                        Action<int> progressAction = p => progress.Report(p);
                        var downloadTask = downloadMethod?.Invoke(mgr, new object[] { releasesToApply, progressAction }) as Task;
                        
                        if (downloadTask != null)
                            await downloadTask;
                    }
                    else
                    {
                        var downloadTask = downloadMethod?.Invoke(mgr, new object[] { releasesToApply, null }) as Task;
                        
                        if (downloadTask != null)
                            await downloadTask;
                    }

                    // ApplyReleases
                    var applyMethod = githubUpdateManagerType.GetMethod("ApplyReleases");
                    var applyTask = applyMethod?.Invoke(mgr, new object[] { update }) as Task;
                    
                    if (applyTask != null)
                        await applyTask;

                    return true;
                }
                finally
                {
                    if (mgr is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Install error: {ex.Message}");
                return false;
            }
        }

        public static void RestartApp()
        {
            try
            {
                // КРИТИЧНО: Используем Location сборки, а не BaseDirectory
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var dllPath = Path.Combine(assemblyDir, "SquirrelLib.dll");
                
                var squirrelAssembly = Assembly.LoadFrom(dllPath);
                
                // UpdateManager.RestartApp() - это статический метод
                var updateManagerType = squirrelAssembly.GetType("Squirrel.UpdateManager");
                var restartMethod = updateManagerType?.GetMethod("RestartApp", BindingFlags.Public | BindingFlags.Static);
                restartMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log($"❌ Restart error: {ex.Message}");
            }
        }
    }

    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string Version { get; set; }
        public string CurrentVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string Error { get; set; }
    }
}