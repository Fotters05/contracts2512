using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace Contract2512.Services
{
    /// <summary>
    /// Сервис автоматического обновления приложения через Squirrel.Windows
    /// Использует рефлексию для избежания проблем с WPF временными проектами
    /// </summary>
    public class AutoUpdateService
    {
        private readonly string _updateUrl;
        private readonly string _currentVersion;
        private Assembly? _squirrelAssembly;

        public AutoUpdateService(string updateUrl)
        {
            _updateUrl = updateUrl;
            _currentVersion = GetCurrentVersion();
            LoadSquirrelAssembly();
        }

        /// <summary>
        /// Загружает сборку Clowd.Squirrel через рефлексию
        /// </summary>
        private void LoadSquirrelAssembly()
        {
            try
            {
                _squirrelAssembly = Assembly.Load("Clowd.Squirrel");
                Debug.WriteLine("✅ Clowd.Squirrel assembly loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to load Clowd.Squirrel assembly: {ex.Message}");
            }
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
            if (_squirrelAssembly == null)
            {
                return new UpdateInfo 
                { 
                    HasUpdate = false, 
                    Error = "Squirrel assembly not loaded", 
                    CurrentVersion = _currentVersion 
                };
            }

            try
            {
                Debug.WriteLine($"🔍 Проверка обновлений по URL: {_updateUrl}");
                
                // Создаём UpdateManager через рефлексию
                var updateManagerType = _squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                if (updateManagerType == null)
                {
                    throw new Exception("UpdateManager type not found");
                }

                var updateManager = Activator.CreateInstance(updateManagerType, _updateUrl);
                if (updateManager == null)
                {
                    throw new Exception("Failed to create UpdateManager instance");
                }

                try
                {
                    // Вызываем CheckForUpdate через рефлексию
                    var checkMethod = updateManagerType.GetMethod("CheckForUpdate");
                    if (checkMethod == null)
                    {
                        throw new Exception("CheckForUpdate method not found");
                    }

                    var checkTask = checkMethod.Invoke(updateManager, null) as Task;
                    if (checkTask == null)
                    {
                        throw new Exception("CheckForUpdate returned null");
                    }

                    await checkTask;
                    
                    // Получаем результат через Result property
                    var resultProperty = checkTask.GetType().GetProperty("Result");
                    var updateInfo = resultProperty?.GetValue(checkTask);

                    if (updateInfo != null)
                    {
                        // Получаем ReleasesToApply
                        var releasesToApplyProp = updateInfo.GetType().GetProperty("ReleasesToApply");
                        var releasesToApply = releasesToApplyProp?.GetValue(updateInfo) as System.Collections.IList;

                        if (releasesToApply != null && releasesToApply.Count > 0)
                        {
                            // Получаем FutureReleaseEntry
                            var futureReleaseProp = updateInfo.GetType().GetProperty("FutureReleaseEntry");
                            var futureRelease = futureReleaseProp?.GetValue(updateInfo);

                            if (futureRelease != null)
                            {
                                var versionProp = futureRelease.GetType().GetProperty("Version");
                                var version = versionProp?.GetValue(futureRelease);
                                
                                Debug.WriteLine($"✅ Найдено обновление: {version}");
                                
                                return new UpdateInfo
                                {
                                    HasUpdate = true,
                                    Version = version?.ToString() ?? "Unknown",
                                    ReleaseNotes = "Доступна новая версия приложения",
                                    CurrentVersion = _currentVersion
                                };
                            }
                        }
                    }

                    Debug.WriteLine($"ℹ️ Обновлений не найдено. Текущая версия: {_currentVersion}");
                    return new UpdateInfo { HasUpdate = false, CurrentVersion = _currentVersion };
                }
                finally
                {
                    // Dispose UpdateManager
                    if (updateManager is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new UpdateInfo { HasUpdate = false, Error = ex.Message, CurrentVersion = _currentVersion };
            }
        }

        /// <summary>
        /// Скачивает и устанавливает обновление через Squirrel
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(IProgress<int>? progress = null)
        {
            if (_squirrelAssembly == null)
            {
                Debug.WriteLine("❌ Squirrel assembly not loaded");
                return false;
            }

            try
            {
                Debug.WriteLine($"📥 Начало загрузки обновления...");
                
                // Создаём UpdateManager через рефлексию
                var updateManagerType = _squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                if (updateManagerType == null)
                {
                    throw new Exception("UpdateManager type not found");
                }

                var updateManager = Activator.CreateInstance(updateManagerType, _updateUrl);
                if (updateManager == null)
                {
                    throw new Exception("Failed to create UpdateManager instance");
                }

                try
                {
                    // Проверяем обновления
                    var checkMethod = updateManagerType.GetMethod("CheckForUpdate");
                    if (checkMethod == null)
                    {
                        throw new Exception("CheckForUpdate method not found");
                    }

                    var checkTask = checkMethod.Invoke(updateManager, null) as Task;
                    if (checkTask == null)
                    {
                        throw new Exception("CheckForUpdate returned null");
                    }

                    await checkTask;
                    
                    var resultProperty = checkTask.GetType().GetProperty("Result");
                    var updateInfo = resultProperty?.GetValue(checkTask);

                    if (updateInfo != null)
                    {
                        var releasesToApplyProp = updateInfo.GetType().GetProperty("ReleasesToApply");
                        var releasesToApply = releasesToApplyProp?.GetValue(updateInfo) as System.Collections.IList;

                        if (releasesToApply != null && releasesToApply.Count > 0)
                        {
                            Debug.WriteLine($"📦 Найдено релизов для применения: {releasesToApply.Count}");
                            
                            // Скачиваем релизы
                            var downloadMethod = updateManagerType.GetMethod("DownloadReleases");
                            if (downloadMethod != null)
                            {
                                Action<int>? progressAction = null;
                                if (progress != null)
                                {
                                    progressAction = p =>
                                    {
                                        progress.Report(p);
                                        Debug.WriteLine($"📥 Прогресс загрузки: {p}%");
                                    };
                                }

                                var downloadTask = downloadMethod.Invoke(updateManager, new object[] { releasesToApply, progressAction! }) as Task;
                                if (downloadTask != null)
                                {
                                    await downloadTask;
                                }
                            }
                            
                            Debug.WriteLine($"✅ Загрузка завершена, применение обновлений...");
                            
                            // Применяем обновления
                            var applyMethod = updateManagerType.GetMethod("ApplyReleases");
                            if (applyMethod != null)
                            {
                                var applyTask = applyMethod.Invoke(updateManager, new object[] { updateInfo }) as Task;
                                if (applyTask != null)
                                {
                                    await applyTask;
                                }
                            }
                            
                            Debug.WriteLine($"✅ Обновление успешно установлено!");
                            return true;
                        }
                    }

                    Debug.WriteLine($"ℹ️ Нет релизов для применения");
                    return false;
                }
                finally
                {
                    if (updateManager is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка установки обновления: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Перезапускает приложение после обновления
        /// </summary>
        public static void RestartApp()
        {
            try
            {
                Debug.WriteLine($"🔄 Перезапуск приложения...");
                
                var squirrelAssembly = Assembly.Load("Clowd.Squirrel");
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                var restartMethod = updateManagerType?.GetMethod("RestartApp", BindingFlags.Public | BindingFlags.Static);
                
                if (restartMethod != null)
                {
                    restartMethod.Invoke(null, null);
                }
                else
                {
                    Debug.WriteLine("❌ RestartApp method not found");
                }
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
