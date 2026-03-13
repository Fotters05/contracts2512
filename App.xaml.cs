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
        /// Проверяет наличие обновлений через Squirrel
        /// </summary>
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                // Читаем настройки из .env
                var githubOwner = EnvConfigService.Get("GITHUB_OWNER") ?? "Pyanuk";
                var githubRepo = EnvConfigService.Get("GITHUB_REPO") ?? "contracts2512";
                var githubToken = EnvConfigService.Get("GITHUB_TOKEN");
                
                // URL для Squirrel (GitHub Releases) - правильный формат
                // Squirrel ищет файл RELEASES по этому URL
                var updateUrl = $"https://github.com/{githubOwner}/{githubRepo}/releases/download";
                
                var updateService = new AutoUpdateService(updateUrl);
                
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo.HasUpdate)
                {
                    // Показываем окно обновления в UI потоке
                    Dispatcher.Invoke(() =>
                    {
                        var updateWindow = new UpdateWindow(updateInfo, updateService);
                        updateWindow.ShowDialog();
                    });
                }
            }
            catch (Exception ex)
            {
                // Ошибки обновления не должны ломать приложение
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
            }
        }
    }
}
