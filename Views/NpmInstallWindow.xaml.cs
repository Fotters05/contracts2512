using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Contract2512.Services;

namespace Contract2512.Views
{
    public partial class NpmInstallWindow : Window
    {
        private readonly NodePackageService _nodePackageService;
        private bool _installSuccess = false;

        public bool InstallSuccess => _installSuccess;

        public NpmInstallWindow()
        {
            InitializeComponent();
            _nodePackageService = new NodePackageService();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InstallPackagesAsync();
        }

        private async Task InstallPackagesAsync()
        {
            try
            {
                StatusTextBlock.Text = "Проверяю установку Node.js...";
                var isNodeInstalled = await _nodePackageService.IsNodeJsInstalledAsync();

                if (!isNodeInstalled)
                {
                    StatusTextBlock.Text = "Node.js не найден.";
                    ProgressBar.IsIndeterminate = false;
                    
                    await Task.Delay(500);
                    
                    _nodePackageService.ShowNodeJsInstallDialog();
                    Close();
                    return;
                }

                StatusTextBlock.Text = "Устанавливаю npm-пакеты...";
                
                var progress = new Progress<string>(message =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogTextBlock.Text += message + Environment.NewLine;
                        
                        // Auto-scroll to bottom
                        if (LogTextBlock.Parent is ScrollViewer scrollViewer)
                        {
                            scrollViewer.ScrollToEnd();
                        }
                    });
                });

                var (success, output) = await _nodePackageService.InstallPackagesAsync(progress);

                if (success)
                {
                    _installSuccess = true;
                    StatusTextBlock.Text = "Установка успешно завершена.";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 100;
                    
                    await Task.Delay(1500);
                    Close();
                }
                else
                {
                    StatusTextBlock.Text = "Не удалось установить npm-пакеты.";
                    ProgressBar.IsIndeterminate = false;
                    
                    MessageBox.Show(
                        $"Не удалось установить npm-пакеты:\n\n{UserErrorMessageService.ToRussianText(output)}",
                        "Ошибка установки",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    
                    Close();
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Произошла ошибка.";
                ProgressBar.IsIndeterminate = false;
                
                MessageBox.Show(
                    $"Ошибка во время установки:\n\n{UserErrorMessageService.ToRussian(ex)}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                
                Close();
            }
        }
    }
}
