using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using Contract2512.Models;
using Contract2512.Services;
using Contract2512.Views;
using Wpf.Ui.Controls;
using System.ComponentModel;

namespace Contract2512
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadData();
            SetupNavigation();
            SetupDataGridClips();
        }

        private void SetupDataGridClips()
        {
            // Устанавливаем Clip для DataGrid при загрузке
            if (PersonsDataGridBorder != null)
            {
                var rect = new System.Windows.Rect(0, 0, PersonsDataGridBorder.ActualWidth, PersonsDataGridBorder.ActualHeight);
                PersonsDataGridBorder.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
            
            if (ContractsDataGridBorder != null)
            {
                var rect = new System.Windows.Rect(0, 0, ContractsDataGridBorder.ActualWidth, ContractsDataGridBorder.ActualHeight);
                ContractsDataGridBorder.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void SetupNavigation()
        {
            // Устанавливаем начальное выделение для "Физические лица"
            BtnPersons.Tag = "Selected";
            UpdateMenuSelection(BtnPersons);
        }

        private void UpdateMenuSelection(System.Windows.Controls.Border selectedButton)
        {
            // Сбрасываем выделение всех кнопок
            BtnPersons.Tag = null;
            BtnContracts.Tag = null;
            BtnPrograms.Tag = null;
            
            // Устанавливаем выделение выбранной кнопки
            if (selectedButton != null)
            {
                selectedButton.Tag = "Selected";
            }
        }

        private void BtnPersons_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnPersons);
            PersonsPanel.Visibility = Visibility.Visible;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
        }

        private void BtnContracts_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnContracts);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Visible;
            ProgramsPanel.Visibility = Visibility.Collapsed;
        }

        private void BtnPrograms_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnPrograms);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Visible;
        }

        private void PersonsDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void ContractsDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
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
                MaximizeIcon.Text = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "❐";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void LoadData()
        {
            LoadPersons();
            LoadContracts();
            LoadPrograms();
        }

        private void LoadPersons()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var persons = db.Persons.ToList();
                    // Загружаем связанные данные
                    foreach (var person in persons)
                    {
                        if (person.GenderId > 0)
                        {
                            person.Gender = db.Genders.Find(person.GenderId);
                        }
                        if (person.ContactsId.HasValue)
                        {
                            person.Contacts = db.Contacts.Find(person.ContactsId.Value);
                        }
                    }
                    PersonsDataGrid.ItemsSource = persons;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при загрузке данных: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void LoadContracts()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var contracts = db.Contracts.ToList();
                    // Загружаем связанные данные
                    foreach (var contract in contracts)
                    {
                        contract.ContractType = db.ContractTypes.Find(contract.ContractTypeId);
                        contract.Program = db.LearningPrograms.Find(contract.ProgramId);
                        contract.Payer = db.Persons.Find(contract.PayerId);
                        contract.Listener = db.Persons.Find(contract.ListenerId);

                        // Загружаем контакты для заказчика и слушателя
                        if (contract.Payer != null && contract.Payer.ContactsId.HasValue)
                        {
                            contract.Payer.Contacts = db.Contacts.Find(contract.Payer.ContactsId.Value);
                        }
                        if (contract.Listener != null && contract.Listener.ContactsId.HasValue)
                        {
                            contract.Listener.Contacts = db.Contacts.Find(contract.Listener.ContactsId.Value);
                        }
                    }
                    ContractsDataGrid.ItemsSource = contracts;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при загрузке договоров: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new PersonWindow();
            ((Window)window).Owner = this;
            if (((Window)window).ShowDialog() == true)
            {
                LoadPersons();
            }
        }

        private void EditPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new PersonWindow(selectedPerson);
                ((Window)window).Owner = this;
                if (((Window)window).ShowDialog() == true)
                {
                    LoadPersons();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для редактирования!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void DeletePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить {selectedPerson.FullName}?",
                    "Подтверждение удаления",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            var person = db.Persons.Find(selectedPerson.Id);
                            if (person != null)
                            {
                                db.Persons.Remove(person);
                                db.SaveChanges();
                                LoadPersons();
                                System.Windows.MessageBox.Show(
                                    "Физическое лицо успешно удалено!",
                                    "Успех",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка при удалении: {ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для удаления!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void CreateContractForPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new ContractWindow(selectedPerson, selectedPerson);
                ((Window)window).Owner = this;
                if (((Window)window).ShowDialog() == true)
                {
                    LoadContracts();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void CreateContractButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ContractWindow();
            ((Window)window).Owner = this;
            if (((Window)window).ShowDialog() == true)
            {
                LoadContracts();
            }
        }

        private void ViewContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContractsDataGrid.SelectedItem is Contract selectedContract)
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        var contract = db.Contracts.FirstOrDefault(c => c.Id == selectedContract.Id);
                        if (contract != null)
                        {
                            // Формируем договор с замененными плейсхолдерами
                            string documentPath = ContractWindow.GenerateContractDocumentForView(contract, db);

                            // Открываем сформированный договор
                            var wordService = new WordDocumentService();
                            wordService.OpenDocument(documentPath);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(
                                "Договор не найден!",
                                "Ошибка",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Ошибка при открытии договора: {ex.Message}",
                        "Ошибка",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите договор для просмотра!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void LoadPrograms()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var programs = db.LearningPrograms.ToList();
                    var programViewModels = programs.Select(p =>
                    {
                        // Загружаем вид программы
                        var programView = db.ProgramViews.Find(p.ProgramViewId);

                        return new ProgramViewModel
                        {
                            Id = p.Id,
                            Name = p.Name ?? "",
                            Format = p.Format ?? "",
                            Hours = p.Hours,
                            LessonsCount = p.LessonsCount,
                            Price = p.Price,
                            ImagePath = GetImagePath(p.Image),
                            ProgramViewName = programView?.Name ?? ""
                        };
                    }).ToList();

                    ProgramsItemsControl.ItemsSource = programViewModels;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при загрузке программ: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private string? GetImagePath(string? image)
        {
            if (string.IsNullOrEmpty(image))
                return null;

            // Проверяем, является ли это URL
            if (Uri.TryCreate(image, UriKind.Absolute, out Uri? uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // Это URL - возвращаем как есть
                return image;
            }
            // Проверяем, существует ли локальный файл
            else if (System.IO.File.Exists(image))
            {
                return image;
            }

            return null;
        }

        private void AddProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ProgramWindow();
            ((Window)window).Owner = this;
            if (((Window)window).ShowDialog() == true)
            {
                LoadPrograms();
            }
        }

        private void EditProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProgram = GetSelectedProgram();
            if (selectedProgram != null)
            {
                using (var db = new AppDbContext())
                {
                    var program = db.LearningPrograms.Find(selectedProgram.Id);
                    if (program != null)
                    {
                        var window = new ProgramWindow(program);
                        ((Window)window).Owner = this;
                        if (((Window)window).ShowDialog() == true)
                        {
                            LoadPrograms();
                            ClearSelection();
                        }
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите программу для редактирования!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProgram = GetSelectedProgram();
            if (selectedProgram != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить программу \"{selectedProgram.Name}\"?",
                    "Подтверждение удаления",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            var program = db.LearningPrograms.Find(selectedProgram.Id);
                            if (program != null)
                            {
                                db.LearningPrograms.Remove(program);
                                db.SaveChanges();
                                LoadPrograms();
                                ClearSelection();
                                System.Windows.MessageBox.Show(
                                    "Программа успешно удалена!",
                                    "Успех",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка при удалении: {ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите программу для удаления!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private ProgramViewModel? GetSelectedProgram()
        {
            if (ProgramsItemsControl.ItemsSource != null)
            {
                foreach (ProgramViewModel program in ProgramsItemsControl.ItemsSource)
                {
                    if (program.IsSelected)
                    {
                        return program;
                    }
                }
            }
            return null;
        }

        private void ClearSelection()
        {
            if (ProgramsItemsControl.ItemsSource != null)
            {
                foreach (ProgramViewModel program in ProgramsItemsControl.ItemsSource)
                {
                    program.IsSelected = false;
                }
            }
        }

        private void ProgramCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Предотвращаем всплытие события на карточку
            e.Handled = true;

            // Снимаем выделение с других программ (только одна может быть выбрана)
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
            {
                if (ProgramsItemsControl.ItemsSource != null)
                {
                    foreach (ProgramViewModel program in ProgramsItemsControl.ItemsSource)
                    {
                        if (program != checkBox.Tag as ProgramViewModel)
                        {
                            program.IsSelected = false;
                        }
                    }
                }
            }
        }

        private void ProgramCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Игнорируем клик, если он был на CheckBox
            if (e.OriginalSource is System.Windows.Controls.CheckBox)
            {
                return;
            }

            if (sender is System.Windows.FrameworkElement element && element.Tag is ProgramViewModel program)
            {
                // Переключаем выделение при двойном клике
                if (e.ClickCount == 2)
                {
                    // Двойной клик - открываем редактирование
                    ClearSelection();
                    program.IsSelected = true;
                    EditProgramButton_Click(sender, e);
                }
                else
                {
                    // Одинарный клик - просто выделяем
                    ClearSelection();
                    program.IsSelected = true;
                }
            }
        }

        private void NavigationViewItem_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    // Класс для отображения программ в карточках
    public class ProgramViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Format { get; set; } = "";
        public int Hours { get; set; }
        public int LessonsCount { get; set; }
        public decimal Price { get; set; }
        public string? ImagePath { get; set; }
        public string ProgramViewName { get; set; } = "";

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
