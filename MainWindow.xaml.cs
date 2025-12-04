using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using Contract2512.Models;
using Contract2512.Services;
using Contract2512.Views;

namespace Contract2512
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadData();
        }


        private void LoadData()
        {
            LoadPersons();
            LoadContracts();
            LoadPrograms();
        }

        private void LoadPersons()
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

        private void LoadContracts()
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

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new PersonWindow();
            if (window.ShowDialog() == true)
            {
                LoadPersons();
            }
        }

        private void EditPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new PersonWindow(selectedPerson);
                if (window.ShowDialog() == true)
                {
                    LoadPersons();
                }
            }
            else
            {
                MessageBox.Show("Выберите физическое лицо для редактирования!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeletePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить {selectedPerson.FullName}?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
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
                                MessageBox.Show("Физическое лицо успешно удалено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите физическое лицо для удаления!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateContractForPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new ContractWindow(selectedPerson, selectedPerson);
                if (window.ShowDialog() == true)
                {
                    LoadContracts();
                }
            }
            else
            {
                MessageBox.Show("Выберите физическое лицо!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateContractButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ContractWindow();
            if (window.ShowDialog() == true)
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
                            MessageBox.Show("Договор не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии договора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите договор для просмотра!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadPrograms()
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
                        Name = p.Name,
                        Format = p.Format,
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

        private string GetImagePath(string image)
        {
            if (string.IsNullOrEmpty(image))
                return null;

            // Проверяем, является ли это URL
            if (Uri.TryCreate(image, UriKind.Absolute, out Uri uri) &&
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
            if (window.ShowDialog() == true)
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
                        if (window.ShowDialog() == true)
                        {
                            LoadPrograms();
                            ClearSelection();
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите программу для редактирования!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProgram = GetSelectedProgram();
            if (selectedProgram != null)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить программу \"{selectedProgram.Name}\"?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
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
                                MessageBox.Show("Программа успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите программу для удаления!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private ProgramViewModel GetSelectedProgram()
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
                // Визуально выделяем выбранную карточку
                foreach (var item in ProgramsItemsControl.Items)
                {
                    // Можно добавить визуальное выделение, если нужно
                }

                // Двойной клик открывает редактирование
                if (e.ClickCount == 2)
                {
                    EditProgramButton_Click(sender, e);
                }
            }
        }
    }

    // Класс для отображения программ в карточках
    public class ProgramViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public long Id { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public int Hours { get; set; }
        public int LessonsCount { get; set; }
        public decimal Price { get; set; }
        public string ImagePath { get; set; }
        public string ProgramViewName { get; set; }

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

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
