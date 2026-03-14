using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Contract2512.Services
{
    public class AutoUpdateService
    {
        private readonly string _updateUrl;

        public AutoUpdateService(string updateUrl)
        {
            _updateUrl = updateUrl;
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
                Debug.WriteLine($"🔍 Checking updates: {_updateUrl}");

                // КРИТИЧНО: Используем Location сборки, а не BaseDirectory
                // Squirrel создает stub exe в корне, а реальное приложение в app-X.X.X
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var dllPath = Path.Combine(assemblyDir, "SquirrelLib.dll");
                
                Debug.WriteLine($"🔍 Assembly location: {assemblyLocation}");
                Debug.WriteLine($"🔍 Assembly directory: {assemblyDir}");
                Debug.WriteLine($"🔍 Looking for DLL at: {dllPath}");
                Debug.WriteLine($"🔍 DLL exists: {File.Exists(dllPath)}");
                
                if (!File.Exists(dllPath))
                {
                    Debug.WriteLine("❌ SquirrelLib.dll not found");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        Error = $"SquirrelLib.dll not found at: {dllPath}",
                        CurrentVersion = GetCurrentVersion()
                    };
                }

                // Загружаем Squirrel через рефлексию чтобы избежать проблем с WPF временными проектами
                Debug.WriteLine($"🔄 Loading assembly from: {dllPath}");
                var squirrelAssembly = Assembly.LoadFrom(dllPath);
                Debug.WriteLine($"✅ Assembly loaded: {squirrelAssembly.FullName}");
                
                // ДИАГНОСТИКА: Выводим все типы в сборке
                Debug.WriteLine("📋 Available types in SquirrelLib:");
                foreach (var type in squirrelAssembly.GetTypes().Take(20))
                {
                    Debug.WriteLine($"  - {type.FullName}");
                }
                
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                
                if (updateManagerType == null)
                {
                    Debug.WriteLine("⚠️ UpdateManager type not found");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        Error = "UpdateManager type not found in SquirrelLib",
                        CurrentVersion = GetCurrentVersion()
                    };
                }

                // Создаём UpdateManager через рефлексию
                var mgr = Activator.CreateInstance(updateManagerType, _updateUrl);
                
                if (mgr == null)
                {
                    Debug.WriteLine("⚠️ Failed to create UpdateManager");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        Error = "Failed to create UpdateManager",
                        CurrentVersion = GetCurrentVersion()
                    };
                }

                try
                {
                    // Вызываем CheckForUpdate через рефлексию
                    var checkMethod = updateManagerType.GetMethod("CheckForUpdate");
                    var updateTask = checkMethod?.Invoke(mgr, null) as Task<object>;
                    
                    if (updateTask == null)
                    {
                        Debug.WriteLine("⚠️ CheckForUpdate method not found");
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

                            Debug.WriteLine($"✅ Update found: {newVersion}");

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
                Debug.WriteLine($"❌ Update error: {ex.Message}");

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
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                
                if (updateManagerType == null)
                {
                    Debug.WriteLine("⚠️ UpdateManager type not found");
                    return false;
                }

                var mgr = Activator.CreateInstance(updateManagerType, _updateUrl);
                
                if (mgr == null)
                {
                    Debug.WriteLine("⚠️ Failed to create UpdateManager");
                    return false;
                }

                try
                {
                    // CheckForUpdate
                    var checkMethod = updateManagerType.GetMethod("CheckForUpdate");
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
                    var downloadMethod = updateManagerType.GetMethod("DownloadReleases");
                    
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
                    var applyMethod = updateManagerType.GetMethod("ApplyReleases");
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
                Debug.WriteLine($"❌ Install error: {ex.Message}");
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
                var updateManagerType = squirrelAssembly.GetType("Clowd.Squirrel.UpdateManager");
                var restartMethod = updateManagerType?.GetMethod("RestartApp", BindingFlags.Public | BindingFlags.Static);
                restartMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Restart error: {ex.Message}");
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