using System.Configuration;
using System.Data;
using System.Windows;
using Contract2512.Services;
using Contract2512.Views;
// using Clowd.Squirrel; // Закомментировано для избежания проблем с WPF временными проектами

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
            // await HandleSquirrelEventsAsync(); // Временно отключено из-за проблем с WPF компиляцией
            
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

        /*
        /// <summary>
        /// Обрабатывает события Squirrel (установка, обновление, удаление)
        /// </summary>
        private async System.Threading.Tasks.Task HandleSquirrelEventsAsync()
        {
            try
            {
                using (var mgr = new UpdateManager(""))
                {
                    // Обрабатываем аргументы командной строки от Squirrel
                    await SquirrelAwareApp.HandleEvents(
                        onInitialInstall: v => mgr.CreateShortcutForThisExe(),
                        onAppUpdate: v => mgr.CreateShortcutForThisExe(),
                        onAppUninstall: v => mgr.RemoveShortcutForThisExe()
                    );
                }
            }
            catch
            {
                // Игнорируем ошибки Squirrel при обычном запуске
            }
        }
        */

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
                
                // URL для Squirrel (GitHub Releases)
                // Для закрытых репозиториев добавляем токен в URL
                string updateUrl;
                if (!string.IsNullOrEmpty(githubToken))
                {
                    // Для закрытых репозиториев используем токен
                    updateUrl = $"https://{githubToken}@github.com/{githubOwner}/{githubRepo}/releases/download";
                    System.Diagnostics.Debug.WriteLine($"🔍 Проверка обновлений (private repo) по URL: https://***@github.com/{githubOwner}/{githubRepo}/releases/download");
                }
                else
                {
                    // Для публичных репозиториев токен не нужен
                    updateUrl = $"https://github.com/{githubOwner}/{githubRepo}/releases/download";
                    System.Diagnostics.Debug.WriteLine($"🔍 Проверка обновлений (public repo) по URL: {updateUrl}");
                }
                
                var updateService = new AutoUpdateService(updateUrl);
                
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
                    System.Diagnostics.Debug.WriteLine($"ℹ️ Обновлений нет. Текущая версия: {updateInfo.CurrentVersion}");
                    if (!string.IsNullOrEmpty(updateInfo.Error))
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {updateInfo.Error}");
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
