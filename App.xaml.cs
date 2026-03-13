using System.Configuration;
using System.Data;
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
        /// Проверяет наличие обновлений на GitHub
        /// </summary>
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                // Читаем настройки из .env
                var githubOwner = EnvConfigService.Get("GITHUB_OWNER") ?? "твой-username";
                var githubRepo = EnvConfigService.Get("GITHUB_REPO") ?? "contracts2512";
                var githubToken = EnvConfigService.Get("GITHUB_TOKEN"); // Токен для приватного репозитория
                
                var updateService = new AutoUpdateService(githubOwner, githubRepo, githubToken);
                
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo.HasUpdate && !string.IsNullOrEmpty(updateInfo.DownloadUrl))
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
                Console.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
            }
        }
    }
}
