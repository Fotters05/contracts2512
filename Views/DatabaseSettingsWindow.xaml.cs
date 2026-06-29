using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Contract2512.Services;
using Microsoft.Win32;
using Npgsql;

namespace Contract2512.Views;

public partial class DatabaseSettingsWindow
{
    private readonly DatabaseBackupService _backupService = new();

    public string EnvPathText { get; }

    public DatabaseSettingsWindow()
    {
        InitializeComponent();
        DataContext = this;

        var envPath = EnvConfigService.GetEnvFilePath();
        EnvPathText = $"Файл настроек: {envPath}";

        ConnectionStringTextBox.Text = EnvConfigService.Get(EnvConfigService.ConnectionStringKey)
            ?? DbConnectionStringProvider.GetConnectionString();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var cs = (ConnectionStringTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cs))
        {
            SetStatus("Введите строку подключения.", isError: true);
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            SetStatus("Проверяю подключение...", isError: false);

            var ok = await TestConnectionAsync(cs);
            if (!ok)
                return;

            EnvConfigService.Set(EnvConfigService.ConnectionStringKey, cs);
            SetStatus("Подключение успешно. Настройки сохранены.", isError: false);

            DialogResult = true;
            Close();
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void ExportDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        var cs = (ConnectionStringTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cs))
        {
            SetStatus("Введите строку подключения перед экспортом.", isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Экспорт базы данных",
            Filter = "Резервная копия БД (*.json)|*.json|Все файлы (*.*)|*.*",
            FileName = $"contracts2512_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            AddExtension = true,
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            SetStatus("Экспортирую базу данных...", isError: false);
            await _backupService.ExportAsync(cs, dialog.FileName);
            SetStatus($"Экспорт завершен: {dialog.FileName}", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка экспорта БД: {UserErrorMessageService.ToRussian(ex)}", isError: true);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void ImportDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        var cs = (ConnectionStringTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cs))
        {
            SetStatus("Введите строку подключения перед импортом.", isError: true);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Импорт базы данных",
            Filter = "Резервная копия БД (*.json)|*.json|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            "Импорт заменит текущие данные в базе данными из резервной копии. Продолжить?",
            "Импорт базы данных",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            SetStatus("Импортирую базу данных...", isError: false);
            await _backupService.ImportAsync(cs, dialog.FileName);
            SetStatus("Импорт завершен. Данные восстановлены из резервной копии.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка импорта БД: {UserErrorMessageService.ToRussian(ex)}", isError: true);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await conn.CloseAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка подключения: {UserErrorMessageService.ToRussian(ex)}", isError: true);
            return false;
        }
    }

    private void SetStatus(string text, bool isError)
    {
        StatusTextBlock.Text = isError ? UserErrorMessageService.ToRussianText(text) : text;
        StatusTextBlock.Foreground = isError
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)) // #EF4444
            : (System.Windows.Media.Brush)FindResource("TextSecondary");
    }

    private void SetButtonsEnabled(bool isEnabled)
    {
        ConnectButton.IsEnabled = isEnabled;
        ExportDatabaseButton.IsEnabled = isEnabled;
        ImportDatabaseButton.IsEnabled = isEnabled;
    }
}
