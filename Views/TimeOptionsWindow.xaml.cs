using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    public partial class TimeOptionsWindow : FluentWindow
    {
        private int? _editingId;
        private bool _isLoadingSelection;

        public TimeOptionsWindow()
        {
            InitializeComponent();
            CategoryComboBox.SelectedIndex = 0;
            LoadOptions();
            ClearForm();
        }

        private void LoadOptions()
        {
            try
            {
                using var db = new AppDbContext();
                OptionsDataGrid.ItemsSource = db.TimeOptions
                    .AsNoTracking()
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.ContractCategory)
                    .ThenBy(o => o.SortOrder)
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
            var category = GetSelectedCategory();
            var optionKey = Normalize(OptionKeyTextBox.Text);
            var name = Normalize(NameTextBox.Text);
            var text = Normalize(TextTextBox.Text);

            if (string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(optionKey) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(text))
            {
                ShowWarning("Заполните тип договора, ключ, название и текст опции.");
                return;
            }

            if (!TryParseOptionalInt(SortOrderTextBox.Text, "Порядок", out var sortOrder) ||
                !TryParseOptionalInt(HoursTextBox.Text, "Часы/нед", out var hoursPerWeek) ||
                !TryParseOptionalInt(WeeksTextBox.Text, "Недель", out var weeksDuration))
            {
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var duplicateExists = db.TimeOptions.Any(o =>
                    o.Id != (_editingId ?? 0) &&
                    o.ContractCategory == category &&
                    o.OptionKey == optionKey);

                if (duplicateExists)
                {
                    ShowWarning("Для этого типа договора уже есть опция с таким ключом.");
                    return;
                }

                var now = DateTime.Now;
                TimeOption option;

                if (_editingId.HasValue)
                {
                    option = db.TimeOptions.FirstOrDefault(o => o.Id == _editingId.Value) ?? new TimeOption
                    {
                        CreatedAt = now
                    };

                    if (option.Id == 0)
                    {
                        db.TimeOptions.Add(option);
                    }
                }
                else
                {
                    option = new TimeOption
                    {
                        CreatedAt = now
                    };
                    db.TimeOptions.Add(option);
                }

                option.ContractCategory = category;
                option.OptionKey = optionKey;
                option.Name = name;
                option.Text = text;
                option.SortOrder = sortOrder ?? 0;
                option.HoursPerWeek = hoursPerWeek;
                option.WeeksDuration = weeksDuration;
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
            if (OptionsDataGrid.SelectedItem is not TimeOption selected)
            {
                ShowWarning("Выберите опцию для удаления.");
                return;
            }

            var result = MessageBox.Show(
                $"Удалить опцию \"{selected.Name}\"?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var option = db.TimeOptions.Find(selected.Id);
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

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadOptions();
        }

        private void OptionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSelection || OptionsDataGrid.SelectedItem is not TimeOption selected)
            {
                return;
            }

            _isLoadingSelection = true;
            _editingId = selected.Id;
            SelectCategory(selected.ContractCategory);
            OptionKeyTextBox.Text = selected.OptionKey;
            NameTextBox.Text = selected.Name;
            TextTextBox.Text = selected.Text;
            SortOrderTextBox.Text = selected.SortOrder.ToString();
            HoursTextBox.Text = selected.HoursPerWeek?.ToString() ?? string.Empty;
            WeeksTextBox.Text = selected.WeeksDuration?.ToString() ?? string.Empty;
            _isLoadingSelection = false;
        }

        private void ClearForm()
        {
            _editingId = null;
            SelectCategory(TimeOptionSeedData.PkCategory);
            OptionKeyTextBox.Text = "Option_Time";
            NameTextBox.Text = string.Empty;
            TextTextBox.Text = string.Empty;
            SortOrderTextBox.Text = "0";
            HoursTextBox.Text = string.Empty;
            WeeksTextBox.Text = string.Empty;
            OptionsDataGrid.SelectedItem = null;
        }

        private string GetSelectedCategory()
        {
            return CategoryComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag
                ? tag
                : TimeOptionSeedData.PkCategory;
        }

        private void SelectCategory(string category)
        {
            foreach (ComboBoxItem item in CategoryComboBox.Items)
            {
                if (string.Equals(item.Tag as string, category, StringComparison.OrdinalIgnoreCase))
                {
                    CategoryComboBox.SelectedItem = item;
                    return;
                }
            }

            CategoryComboBox.SelectedIndex = 0;
        }

        private static bool TryParseOptionalInt(string value, string fieldName, out int? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (int.TryParse(value.Trim(), out var parsed) && parsed >= 0)
            {
                result = parsed;
                return true;
            }

            ShowWarning($"Поле \"{fieldName}\" должно быть положительным числом.");
            return false;
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeRestoreButton.Content = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeRestoreButton.Content = "❐";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
