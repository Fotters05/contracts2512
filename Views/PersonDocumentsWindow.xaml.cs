using System;
using System.Collections.Generic;
using System.IO;
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
    public partial class PersonDocumentsWindow : FluentWindow
    {
        private readonly Person _person;

        public PersonDocumentsWindow(Person person)
        {
            InitializeComponent();
            _person = person;
            UpdateTitle();
            LoadContracts();
            LoadOrders();
            LoadCards();
        }

        private void UpdateTitle()
        {
            TitleTextBlock.Text = $"Документы: {_person.FullName}";
            PersonInfoTextBlock.Text = $"Документы физического лица: {_person.FullName}";
        }

        private void LoadContracts()
        {
            try
            {
                using var db = new AppDbContext();
                var contracts = db.Contracts
                    .AsNoTracking()
                    .Include(c => c.ContractType)
                    .Include(c => c.Program)
                    .Include(c => c.Payer)
                    .Include(c => c.Listener)
                    .Where(c => !c.IsArchived && (c.PayerId == _person.Id || c.ListenerId == _person.Id))
                    .OrderByDescending(c => c.ContractDate)
                    .ToList();

                ContractsDataGrid.ItemsSource = contracts.Select(contract => new ContractWithRole
                {
                    Contract = contract,
                    Role = GetContractRole(contract)
                }).ToList();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке договоров: {ex.Message}");
            }
        }

        private void LoadOrders()
        {
            try
            {
                using var db = new AppDbContext();
                OrdersDataGrid.ItemsSource = db.OrderDocuments
                    .AsNoTracking()
                    .Include(o => o.Program)
                    .Include(o => o.Contract)
                    .Include(o => o.Listener)
                    .Where(o => o.ListenerId == _person.Id || (o.Contract != null && o.Contract.ListenerId == _person.Id))
                    .OrderByDescending(o => o.GeneratedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке приказов: {ex.Message}");
            }
        }

        private void LoadCards()
        {
            try
            {
                string cardsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Личные карточки");

                if (!Directory.Exists(cardsFolder))
                {
                    CardsDataGrid.ItemsSource = new List<CardFileInfo>();
                    return;
                }

                CardsDataGrid.ItemsSource = Directory.GetFiles(cardsFolder, $"*{_person.LastName}*.docx")
                    .Select(filePath => new CardFileInfo
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        ContractNumber = ExtractContractNumber(Path.GetFileName(filePath)),
                        CreatedDate = File.GetLastWriteTime(filePath)
                    })
                    .OrderByDescending(card => card.CreatedDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке личных карточек: {ex.Message}");
            }
        }

        private string GetContractRole(Contract contract)
        {
            if (contract.PayerId == _person.Id && contract.ListenerId == _person.Id)
                return "Заказчик, слушатель";
            if (contract.PayerId == _person.Id)
                return "Заказчик";
            if (contract.ListenerId == _person.Id)
                return "Слушатель";
            return string.Empty;
        }

        private static string ExtractContractNumber(string fileName)
        {
            try
            {
                var parts = fileName.Split('_');
                if (parts.Length >= 2)
                {
                    return parts[1];
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private void OpenContractButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedContract();
        }

        private void ContractsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedContract();
        }

        private void OpenSelectedContract()
        {
            if (ContractsDataGrid.SelectedItem is not ContractWithRole selectedItem)
            {
                ShowWarning("Выберите договор для просмотра!");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var contract = db.Contracts.FirstOrDefault(c => c.Id == selectedItem.Contract.Id);
                if (contract == null)
                {
                    ShowWarning("Договор не найден!");
                    return;
                }

                string documentPath = ContractWindow.GenerateContractDocumentForView(contract, db);
                new WordDocumentService().OpenDocument(documentPath);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при открытии договора: {ex.Message}");
            }
        }

        private void OpenOrderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedOrder();
        }

        private void OrdersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedOrder();
        }

        private void OpenSelectedOrder()
        {
            if (OrdersDataGrid.SelectedItem is not OrderDocument selectedOrder)
            {
                ShowWarning("Выберите приказ для открытия!");
                return;
            }

            OpenFile(selectedOrder.FilePath, "Файл приказа не найден.");
        }

        private void OpenCardButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedCard();
        }

        private void CardsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedCard();
        }

        private void OpenSelectedCard()
        {
            if (CardsDataGrid.SelectedItem is not CardFileInfo selectedCard)
            {
                ShowWarning("Выберите личную карточку для открытия!");
                return;
            }

            OpenFile(selectedCard.FilePath, "Файл личной карточки не найден.");
        }

        private static void OpenFile(string filePath, string missingMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    ShowWarning(missingMessage);
                    return;
                }

                new WordDocumentService().OpenDocument(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при открытии файла: {ex.Message}");
            }
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
