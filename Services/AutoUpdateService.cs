using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
// using Clowd.Squirrel; // Закомментировано для избежания проблем с WPF временными проектами

namespace Contract2512.Services
{
    /// <summary>
    /// Сервис автоматического обновления приложения через Squirrel.Windows
    /// Использует рефлексию для загрузки Clowd.Squirrel в рантайме
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
        /// Проверяет наличие обновлений через Squirrel (используя рефлексию)
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                // Загружаем Clowd.Squirrel через рефлексию
                var squirrelAssembly = Assembly.Load("Clowd.Squirrel");
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                
                if (updateManagerType == null)
                {
                    return new UpdateInfo { HasUpdate = false, Error = "UpdateManager не найден", CurrentVersion = _currentVersion };
                }

                // Создаём UpdateManager через рефлексию
                using (dynamic updateManager = Activator.CreateInstance(updateManagerType, _updateUrl))
                {
                    // Вызываем CheckForUpdate через рефлексию
                    var checkMethod = updateManagerType.GetMethod("CheckForUpdate");
                    dynamic updateInfo = await (Task<dynamic>)checkMethod.Invoke(updateManager, null);

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
        /// Скачивает и устанавливает обновление через Squirrel (используя рефлексию)
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(IProgress<int> progress = null)
        {
            try
            {
                // Загружаем Clowd.Squirrel через рефлексию
                var squirrelAssembly = Assembly.Load("Clowd.Squirrel");
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                
                if (updateManagerType == null)
                {
                    return false;
                }

                // Создаём UpdateManager через рефлексию
                using (dynamic updateManager = Activator.CreateInstance(updateManagerType, _updateUrl))
                {
                    // Проверяем обновления
                    var checkMethod = updateManagerType.GetMethod("CheckForUpdate");
                    dynamic updateInfo = await (Task<dynamic>)checkMethod.Invoke(updateManager, null);
                    
                    if (updateInfo?.ReleasesToApply?.Count > 0)
                    {
                        // Скачиваем релизы
                        var downloadMethod = updateManagerType.GetMethod("DownloadReleases");
                        Action<int> progressCallback = p => progress?.Report(p);
                        await (Task)downloadMethod.Invoke(updateManager, new object[] { updateInfo.ReleasesToApply, progressCallback });
                        
                        // Применяем обновления
                        var applyMethod = updateManagerType.GetMethod("ApplyReleases");
                        await (Task)applyMethod.Invoke(updateManager, new object[] { updateInfo });
                        
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
        /// Перезапускает приложение после обновления (используя рефлексию)
        /// </summary>
        public static void RestartApp()
        {
            try
            {
                var squirrelAssembly = Assembly.Load("Clowd.Squirrel");
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                var restartMethod = updateManagerType?.GetMethod("RestartApp", BindingFlags.Public | BindingFlags.Static);
                restartMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка перезапуска: {ex.Message}");
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
        public string CurrentVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string Error { get; set; }
    }
}
