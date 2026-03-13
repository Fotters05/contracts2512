using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Clowd.Squirrel;

namespace Contract2512.Services
{
    /// <summary>
    /// Сервис автоматического обновления приложения через Squirrel.Windows
    /// </summary>
    public class AutoUpdateService
    {
        private readonly string _updateUrl;
        private readonly string _currentVersion;

        public AutoUpdateService(string updateUrl)
        {
            _updateUrl = updateUrl;
            _currentVersion = GetCurrentVersion();
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
        /// Проверяет наличие обновлений через Squirrel
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                using (var updateManager = new UpdateManager(_updateUrl))
                {
                    var updateInfo = await updateManager.CheckForUpdate();

                    if (updateInfo?.ReleasesToApply?.Count > 0)
                    {
                        var latestRelease = updateInfo.FutureReleaseEntry;
                        return new UpdateInfo
                        {
                            HasUpdate = true,
                            Version = latestRelease.Version.ToString(),
                            ReleaseNotes = "Доступна новая версия приложения",
                            CurrentVersion = _currentVersion
                        };
                    }

                    return new UpdateInfo { HasUpdate = false, CurrentVersion = _currentVersion };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
                return new UpdateInfo { HasUpdate = false, Error = ex.Message, CurrentVersion = _currentVersion };
            }
        }

        /// <summary>
        /// Скачивает и устанавливает обновление через Squirrel
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(IProgress<int> progress = null)
        {
            try
            {
                using (var updateManager = new UpdateManager(_updateUrl))
                {
                    // Скачиваем обновление с прогрессом
                    var updateInfo = await updateManager.CheckForUpdate();
                    
                    if (updateInfo?.ReleasesToApply?.Count > 0)
                    {
                        await updateManager.DownloadReleases(updateInfo.ReleasesToApply, p => progress?.Report(p));
                        await updateManager.ApplyReleases(updateInfo);
                        
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка установки обновления: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Перезапускает приложение после обновления
        /// </summary>
        public static void RestartApp()
        {
            UpdateManager.RestartApp();
        }
    }

    /// <summary>
    /// Информация об обновлении
    /// </summary>
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string Version { get; set; }
        public string CurrentVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string Error { get; set; }
    }
}
