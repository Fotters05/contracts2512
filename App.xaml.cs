using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
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
        private int _isUpdateCheckRunning;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Обрабатываем события Squirrel через рефлексию
            HandleSquirrelEvents();
            
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

            // Проверяем обновления и показываем главное окно
            CheckForUpdatesInBackground();
            
            // Открываем главное окно сразу
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        /// <summary>
        /// Проверяет наличие обновлений в фоновом режиме
        /// </summary>
        private async void CheckForUpdatesInBackground()
        {
            // Небольшая задержка чтобы главное окно успело открыться
            await System.Threading.Tasks.Task.Delay(1000);
            
            await CheckForUpdatesAsync(showProgressWindow: false, showErrorsToUser: false);
        }

        /// <summary>
        /// Обрабатывает события Squirrel через Update.exe
        /// </summary>
        private void HandleSquirrelEvents()
        {
            try
            {
                // Проверяем, запущено ли приложение из Squirrel установки
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var parentDir = Directory.GetParent(appDir)?.FullName;
                if (parentDir == null) return;

                var updateExe = Path.Combine(parentDir, "Update.exe");
                if (!File.Exists(updateExe))
                {
                    Debug.WriteLine("⚠️ Update.exe не найден, пропускаем обработку Squirrel событий");
                    return;
                }

                Debug.WriteLine("✅ Приложение запущено из Squirrel установки");

                // Squirrel автоматически обрабатывает события через Update.exe
                // Нам не нужно вручную вызывать HandleEvents
                // Update.exe сам создает/удаляет ярлыки при установке/удалении
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка обработки Squirrel событий: {ex.Message}");
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
        private async System.Threading.Tasks.Task CheckForUpdatesAsync(bool showProgressWindow = true, bool showErrorsToUser = true)
        {
            if (Interlocked.Exchange(ref _isUpdateCheckRunning, 1) == 1)
            {
                System.Diagnostics.Debug.WriteLine("ℹ️ Проверка обновлений уже выполняется, повторный запуск пропущен");
                return;
            }

            try
            {
                // Читаем настройки GitHub из .env
                var githubOwner = EnvConfigService.Get("GITHUB_OWNER") ?? "Fotters05";
                var githubRepo = EnvConfigService.Get("GITHUB_REPO") ?? "contracts2512";
                var githubToken = EnvConfigService.Get("GITHUB_TOKEN");
                var updateUrl = await ResolveUpdateUrlAsync(githubOwner, githubRepo, githubToken);
                
                System.Diagnostics.Debug.WriteLine($"🔍 URL: {updateUrl.Replace(githubToken ?? "", "***")}");
                
                var updateService = new AutoUpdateService(updateUrl);

                if (showProgressWindow)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var checkWindow = new CheckUpdateWindow(updateService);
                        checkWindow.ShowDialog();
                    });
                    return;
                }

                var updateInfo = await updateService.CheckForUpdatesAsync();
                if (updateInfo.HasUpdate)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var updateWindow = new UpdateWindow(updateInfo, updateService);
                        updateWindow.ShowDialog();
                    });
                }
                else if (!string.IsNullOrWhiteSpace(updateInfo.Error))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Фоновая проверка обновлений завершилась ошибкой: {updateInfo.Error}");

                    if (showErrorsToUser)
                    {
                        MessageBox.Show(
                            $"Не удалось проверить обновления.\n{updateInfo.Error}",
                            "Ошибка проверки обновлений",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");

                if (showErrorsToUser)
                {
                    MessageBox.Show(
                        $"Не удалось проверить обновления.\n{ex.Message}",
                        "Ошибка проверки обновлений",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isUpdateCheckRunning, 0);
            }
        }

        private static async System.Threading.Tasks.Task<string> ResolveUpdateUrlAsync(string githubOwner, string githubRepo, string? githubToken)
        {
            var fallbackUrl = BuildDownloadUrl(githubOwner, githubRepo, "latest/download", githubToken);

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Contract2512-Updater");

                if (!string.IsNullOrWhiteSpace(githubToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
                    System.Diagnostics.Debug.WriteLine("🔍 Проверка обновлений (приватный или защищенный репозиторий)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("🔍 Проверка обновлений (публичный репозиторий)");
                }

                using var response = await httpClient.GetAsync($"https://api.github.com/repos/{githubOwner}/{githubRepo}/releases/latest");
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ GitHub API вернул {(int)response.StatusCode}, используем latest/download");
                    return fallbackUrl;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);

                if (document.RootElement.TryGetProperty("tag_name", out var tagNameElement))
                {
                    var tagName = tagNameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        return BuildDownloadUrl(githubOwner, githubRepo, $"download/{tagName}", githubToken);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Не удалось получить latest release через API: {ex.Message}");
            }

            return fallbackUrl;
        }

        private static string BuildDownloadUrl(string githubOwner, string githubRepo, string releasePath, string? githubToken)
        {
            if (!string.IsNullOrWhiteSpace(githubToken))
            {
                var encodedOwner = Uri.EscapeDataString(githubOwner);
                var encodedToken = Uri.EscapeDataString(githubToken);
                return $"https://{encodedOwner}:{encodedToken}@github.com/{githubOwner}/{githubRepo}/releases/{releasePath}";
            }

            return $"https://github.com/{githubOwner}/{githubRepo}/releases/{releasePath}";
        }
    }
}
