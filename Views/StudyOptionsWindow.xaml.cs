using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Contract2512.Models;
using Contract2512.Services;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Contract2512.Views
{
    public partial class StudyOptionsWindow : FluentWindow
    {
        private int? _editingId;
        private bool _isLoadingSelection;

        public StudyOptionsWindow()
        {
            InitializeComponent();
            LoadOptions();
            ClearForm();
        }

        private void LoadOptions()
        {
            try
            {
                using var db = new AppDbContext();
                OptionsDataGrid.ItemsSource = db.StudyOptions
                    .AsNoTracking()
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.SortOrder)
                    .ThenBy(o => o.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке опций: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var optionKey = Normalize(OptionKeyTextBox.Text);
            var name = Normalize(NameTextBox.Text);
            var text = Normalize(TextTextBox.Text);

            if (string.IsNullOrWhiteSpace(optionKey) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(text))
            {
                ShowWarning("Заполните ключ, название и текст опции.");
                return;
            }

            if (!TryParseInt(SortOrderTextBox.Text, "Порядок", out var sortOrder) ||
                !TryParseNullableInt(HoursTextBox.Text, "Часы/нед", out var hours) ||
                !TryParseNullableDecimal(WeeksTextBox.Text, "Недель", out var weeks))
            {
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var duplicateExists = db.StudyOptions.Any(o => o.Id != (_editingId ?? 0) && o.OptionKey == optionKey);
                if (duplicateExists)
                {
                    ShowWarning("Опция с таким ключом уже существует.");
                    return;
                }

                var now = DateTime.Now;
                StudyOption option;

                if (_editingId.HasValue)
                {
                    option = db.StudyOptions.FirstOrDefault(o => o.Id == _editingId.Value) ?? new StudyOption { CreatedAt = now };
                    if (option.Id == 0)
                    {
                        db.StudyOptions.Add(option);
                    }
                }
                else
                {
                    option = new StudyOption { CreatedAt = now };
                    db.StudyOptions.Add(option);
                }

                option.OptionKey = optionKey;
                option.Name = name;
                option.Text = text;
                option.SortOrder = sortOrder;
                option.HoursPerWeek = hours;
                option.WeeksDuration = weeks;
                option.IsActive = true;
                option.UpdatedAt = now;

                db.SaveChanges();
                LoadOptions();
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при сохранении опции: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (OptionsDataGrid.SelectedItem is not StudyOption selected)
            {
                ShowWarning("Выберите опцию для удаления.");
                return;
            }

            if (MessageBox.Show($"Удалить опцию \"{selected.Name}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var option = db.StudyOptions.Find(selected.Id);
                if (option != null)
                {
                    option.IsActive = false;
                    option.UpdatedAt = DateTime.Now;
                    db.SaveChanges();
                }

                LoadOptions();
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при удалении опции: {ex.Message}");
            }
        }

        private void NewButton_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadOptions();

        private void OptionsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingSelection || OptionsDataGrid.SelectedItem is not StudyOption selected)
                return;

            _isLoadingSelection = true;
            _editingId = selected.Id;
            OptionKeyTextBox.Text = selected.OptionKey;
            NameTextBox.Text = selected.Name;
            TextTextBox.Text = selected.Text;
            SortOrderTextBox.Text = selected.SortOrder.ToString(CultureInfo.InvariantCulture);
            HoursTextBox.Text = selected.HoursPerWeek?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            WeeksTextBox.Text = selected.WeeksDuration?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _isLoadingSelection = false;
        }

        private void ClearForm()
        {
            _editingId = null;
            OptionKeyTextBox.Text = "Option_study";
            NameTextBox.Text = string.Empty;
            TextTextBox.Text = string.Empty;
            SortOrderTextBox.Text = "0";
            HoursTextBox.Text = string.Empty;
            WeeksTextBox.Text = string.Empty;
            OptionsDataGrid.SelectedItem = null;
        }

        private static bool TryParseInt(string value, string fieldName, out int result)
        {
            if (int.TryParse(value.Trim(), out result) && result >= 0)
                return true;

            ShowWarning($"Поле \"{fieldName}\" должно быть положительным числом.");
            return false;
        }

        private static bool TryParseNullableInt(string value, string fieldName, out int? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (int.TryParse(value.Trim(), out var parsed) && parsed >= 0)
            {
                result = parsed;
                return true;
            }

            ShowWarning($"Поле \"{fieldName}\" должно быть положительным числом.");
            return false;
        }

        private static bool TryParseNullableDecimal(string value, string fieldName, out decimal? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var normalized = value.Trim().Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                result = parsed;
                return true;
            }

            ShowWarning($"Поле \"{fieldName}\" должно быть положительным числом.");
            return false;
        }

        private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            MaximizeRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private static void ShowWarning(string message) => MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);

        private static void ShowError(string message) => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
