using System.Configuration;
using System.Data;
using System.IO;
using System.Reflection;
using System.Windows;
using Contract2512.Services;
using Contract2512.Views;

namespace Contract2512
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Обрабатываем события Squirrel (установка, обновление, удаление)
            await HandleSquirrelEventsAsync();
            
            // Проверяем и устанавливаем npm пакеты для парсера (если нужно)
            await CheckAndInstallNodePackagesAsync();
            
            // Проверяем наличие настроек подключения к БД
            if (!DbConnectionStringProvider.HasConnectionString())
            {
                // Если настроек нет, показываем окно настроек БД
                var settingsWindow = new DatabaseSettingsWindow();
                var result = settingsWindow.ShowDialog();
                
                // Если пользователь закрыл окно без сохранения настроек, закрываем приложение
                if (result != true)
                {
                    MessageBox.Show(
                        "Для работы приложения необходимо настроить подключение к базе данных.",
                        "Настройки не сохранены",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    Shutdown();
                    return;
                }
            }

            // Проверяем обновления (в фоновом режиме, не блокируем запуск)
            _ = CheckForUpdatesAsync();
        }

        /// <summary>
        /// Обрабатывает события Squirrel (установка, обновление, удаление) через рефлексию
        /// </summary>
        private async System.Threading.Tasks.Task HandleSquirrelEventsAsync()
        {
            try
            {
                // КРИТИЧНО: Используем Location сборки, а не BaseDirectory
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var dllPath = Path.Combine(assemblyDir, "SquirrelLib.dll");
                
                var squirrelAssembly = Assembly.LoadFrom(dllPath);
                var squirrelAwareAppType = squirrelAssembly.GetType("Squirrel.SquirrelAwareApp");
                
                if (squirrelAwareAppType == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ SquirrelAwareApp type not found");
                    return;
                }

                // Получаем метод HandleEvents
                var handleEventsMethod = squirrelAwareAppType.GetMethod("HandleEvents", BindingFlags.Public | BindingFlags.Static);
                
                if (handleEventsMethod == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ HandleEvents method not found");
                    return;
                }

                // Создаём делегаты для обработки событий
                var updateManagerType = squirrelAssembly.GetType("Squirrel.UpdateManager");
                
                Action<object> onInitialInstall = v => 
                {
                    System.Diagnostics.Debug.WriteLine($"📦 Первая установка версии {v}");
                    try
                    {
                        var mgr = Activator.CreateInstance(updateManagerType!, "");
                        if (mgr != null)
                        {
                            var createShortcutMethod = updateManagerType!.GetMethod("CreateShortcutForThisExe");
                            createShortcutMethod?.Invoke(mgr, null);
                            
                            if (mgr is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Ошибка создания ярлыка: {ex.Message}");
                    }
                };
                
                Action<object> onAppUpdate = v => 
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 Обновление до версии {v}");
                    try
                    {
                        var mgr = Activator.CreateInstance(updateManagerType!, "");
                        if (mgr != null)
                        {
                            var createShortcutMethod = updateManagerType!.GetMethod("CreateShortcutForThisExe");
                            createShortcutMethod?.Invoke(mgr, null);
                            
                            if (mgr is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Ошибка обновления ярлыка: {ex.Message}");
                    }
                };
                
                Action<object> onAppUninstall = v => 
                {
                    System.Diagnostics.Debug.WriteLine($"🗑️ Удаление версии {v}");
                    try
                    {
                        var mgr = Activator.CreateInstance(updateManagerType!, "");
                        if (mgr != null)
                        {
                            var removeShortcutMethod = updateManagerType!.GetMethod("RemoveShortcutForThisExe");
                            removeShortcutMethod?.Invoke(mgr, null);
                            
                            if (mgr is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Ошибка удаления ярлыка: {ex.Message}");
                    }
                };

                // Вызываем HandleEvents через рефлексию
                var task = handleEventsMethod.Invoke(null, new object?[] 
                { 
                    onInitialInstall, 
                    onAppUpdate, 
                    onAppUninstall, 
                    null, // onFirstRun
                    null  // arguments
                }) as System.Threading.Tasks.Task;

                if (task != null)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки Squirrel при обычном запуске
                System.Diagnostics.Debug.WriteLine($"ℹ️ Squirrel events: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверяет наличие node_modules и устанавливает npm пакеты если нужно
        /// </summary>
        private async System.Threading.Tasks.Task CheckAndInstallNodePackagesAsync()
        {
            try
            {
                var nodePackageService = new NodePackageService();
                
                // Проверяем наличие node_modules
                if (!nodePackageService.IsNodeModulesInstalled())
                {
                    System.Diagnostics.Debug.WriteLine("📦 node_modules not found, starting npm install...");
                    
                    // Показываем окно установки
                    var installWindow = new NpmInstallWindow();
                    installWindow.ShowDialog();
                    
                    if (!installWindow.InstallSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ npm install failed or was cancelled");
                        
                        var result = MessageBox.Show(
                            "Parser dependencies were not installed.\n\n" +
                            "The parser will not work without these dependencies.\n\n" +
                            "Do you want to continue anyway?",
                            "Warning",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning
                        );
                        
                        if (result == MessageBoxResult.No)
                        {
                            Shutdown();
                            return;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("✅ npm install completed successfully");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ node_modules already installed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error checking node packages: {ex.Message}");
                // Не блокируем запуск приложения из-за ошибок с npm
            }
        }

        /// <summary>
        /// Проверяет наличие обновлений через Squirrel
        /// </summary>
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                // Читаем настройки из .env с fallback значениями
                var githubOwner = EnvConfigService.Get("GITHUB_OWNER") ?? "Fotters05";
                var githubRepo = EnvConfigService.Get("GITHUB_REPO") ?? "contracts2512";
                var githubToken = EnvConfigService.Get("GITHUB_TOKEN");
                
                // URL для GithubUpdateManager (без /releases/download)
                var repoUrl = $"https://github.com/{githubOwner}/{githubRepo}";
                
                System.Diagnostics.Debug.WriteLine($"🔍 Проверка обновлений: {repoUrl}");
                System.Diagnostics.Debug.WriteLine($"🔍 Токен: {(string.IsNullOrEmpty(githubToken) ? "отсутствует" : "***")}");
                
                var updateService = new AutoUpdateService(repoUrl, githubToken);
                
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo.HasUpdate)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Найдено обновление: {updateInfo.Version}");
                    
                    // Показываем окно обновления в UI потоке
                    Dispatcher.Invoke(() =>
                    {
                        var updateWindow = new UpdateWindow(updateInfo, updateService);
                        updateWindow.ShowDialog();
                    });
                }
                else
                {
                    // Если есть ошибка - просто логируем, не показываем пользователю
                    if (!string.IsNullOrEmpty(updateInfo.Error))
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки обновлений: {updateInfo.Error}");
                        // НЕ показываем MessageBox с ошибкой - это техническая проблема
                    }
                    else
                    {
                        // Обновлений нет и нет ошибок - показываем информационное сообщение
                        System.Diagnostics.Debug.WriteLine($"ℹ️ Обновлений нет. Текущая версия: {updateInfo.CurrentVersion}");
                        
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"Обновлений нет.\n\nТекущая версия: {updateInfo.CurrentVersion}",
                                "Проверка обновлений",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Ошибки обновления не должны ломать приложение
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}