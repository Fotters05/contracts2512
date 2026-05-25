using System;
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
    public partial class OrderRegistryWindow : FluentWindow
    {
        private long? _editingId;
        private bool _isLoadingSelection;

        public OrderRegistryWindow()
        {
            InitializeComponent();
            OrderDatePicker.SelectedDate = DateTime.Today;
            LoadEntries();
        }

        private void LoadEntries()
        {
            try
            {
                using var db = new AppDbContext();
                RegistryDataGrid.ItemsSource = db.OrderRegistryEntries
                    .AsNoTracking()
                    .OrderByDescending(e => e.OrderDate)
                    .ThenByDescending(e => e.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке реестра приказов: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var orderNumber = Normalize(OrderNumberTextBox.Text);
            var orderDate = OrderDatePicker.SelectedDate;
            var orderSubject = Normalize(OrderSubjectTextBox.Text);
            var listenerName = Normalize(ListenerNameTextBox.Text);
            var programName = Normalize(ProgramNameTextBox.Text);

            if (string.IsNullOrWhiteSpace(orderNumber) ||
                orderDate == null ||
                string.IsNullOrWhiteSpace(orderSubject) ||
                string.IsNullOrWhiteSpace(listenerName) ||
                string.IsNullOrWhiteSpace(programName))
            {
                ShowWarning("Заполните все поля реестра.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var now = DateTime.Now;
                OrderRegistryEntry entry;

                if (_editingId.HasValue)
                {
                    entry = db.OrderRegistryEntries.FirstOrDefault(e => e.Id == _editingId.Value) ?? new OrderRegistryEntry
                    {
                        CreatedAt = now
                    };

                    if (entry.Id == 0)
                    {
                        db.OrderRegistryEntries.Add(entry);
                    }
                }
                else
                {
                    entry = new OrderRegistryEntry
                    {
                        CreatedAt = now
                    };
                    db.OrderRegistryEntries.Add(entry);
                }

                entry.OrderNumber = orderNumber;
                entry.OrderDate = orderDate.Value.Date;
                entry.OrderSubject = orderSubject;
                entry.ListenerName = listenerName;
                entry.ProgramName = programName;
                entry.UpdatedAt = now;

                db.SaveChanges();
                ClearForm();
                LoadEntries();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при сохранении записи: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (RegistryDataGrid.SelectedItem is not OrderRegistryEntry selected)
            {
                ShowWarning("Выберите запись для удаления.");
                return;
            }

            var result = MessageBox.Show(
                $"Удалить приказ №{selected.OrderNumber} из реестра?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                using var db = new AppDbContext();
                var entry = db.OrderRegistryEntries.Find(selected.Id);
                if (entry != null)
                {
                    db.OrderRegistryEntries.Remove(entry);
                    db.SaveChanges();
                }

                ClearForm();
                LoadEntries();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при удалении записи: {ex.Message}");
            }
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadEntries();
        }

        private void RegistryDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingSelection || RegistryDataGrid.SelectedItem is not OrderRegistryEntry selected)
                return;

            _isLoadingSelection = true;
            _editingId = selected.Id;
            OrderNumberTextBox.Text = selected.OrderNumber;
            OrderDatePicker.SelectedDate = selected.OrderDate;
            OrderSubjectTextBox.Text = selected.OrderSubject;
            ListenerNameTextBox.Text = selected.ListenerName;
            ProgramNameTextBox.Text = selected.ProgramName;
            _isLoadingSelection = false;
        }

        private void ClearForm()
        {
            _editingId = null;
            OrderNumberTextBox.Text = string.Empty;
            OrderDatePicker.SelectedDate = DateTime.Today;
            OrderSubjectTextBox.Text = string.Empty;
            ListenerNameTextBox.Text = string.Empty;
            ProgramNameTextBox.Text = string.Empty;
            RegistryDataGrid.SelectedItem = null;
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
