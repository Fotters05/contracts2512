using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Contract2512.Models;
using Contract2512.Services;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Contract2512.Views
{
    public partial class ContractWindow : FluentWindow
    {
        private Person _selectedPayer;
        private Person _selectedListener;
        private ContractType _selectedContractType;
        private LearningProgram _selectedProgram;

        // Полные списки для фильтрации
        private List<ContractType> _allContractTypes;
        private List<LearningProgram> _allPrograms;
        private List<Person> _allPersons;

        public ContractWindow(Person payer = null, Person listener = null)
        {
            InitializeComponent();
            LoadData();
            
            if (payer != null)
            {
                _selectedPayer = payer;
                PayerComboBox.SelectedItem = payer;
            }
            
            if (listener != null)
            {
                _selectedListener = listener;
                ListenerComboBox.SelectedItem = listener;
            }
        }

        private void LoadData()
        {
            using (var db = new AppDbContext())
            {
                // Загружаем типы договоров без отслеживания и сохраняем полный список
                _allContractTypes = db.ContractTypes.AsNoTracking().ToList();
                ContractTypeComboBox.ItemsSource = _allContractTypes;

                // Загружаем программы обучения без отслеживания и сохраняем полный список
                _allPrograms = db.LearningPrograms.AsNoTracking().ToList();
                ProgramComboBox.ItemsSource = _allPrograms;

                // Загружаем физических лиц без отслеживания и сохраняем полный список
                _allPersons = db.Persons.AsNoTracking().ToList();
                
                // Для слушателей всегда показываем только физических лиц
                var listenerViewModels = _allPersons.Select(p => new PersonViewModel
                {
                    Person = p,
                    DisplayName = p.FullName
                }).ToList();
                ListenerComboBox.ItemsSource = listenerViewModels;
                ListenerComboBox.DisplayMemberPath = "DisplayName";
                System.Windows.Controls.TextSearch.SetTextPath(ListenerComboBox, "DisplayName");
                
                // Для заказчиков изначально показываем физических лиц (обновится при выборе типа договора)
                var payerViewModels = _allPersons.Select(p => new PersonViewModel
                {
                    Person = p,
                    DisplayName = p.FullName
                }).ToList();
                PayerComboBox.ItemsSource = payerViewModels;
                PayerComboBox.DisplayMemberPath = "DisplayName";
                System.Windows.Controls.TextSearch.SetTextPath(PayerComboBox, "DisplayName");
            }

            // Добавляем обработчики для фильтрации при вводе текста
            ContractTypeComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler(ContractTypeComboBox_TextChanged));
            ProgramComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler(ProgramComboBox_TextChanged));
            PayerComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler(PayerComboBox_TextChanged));
            ListenerComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler(ListenerComboBox_TextChanged));

            // Добавляем обработчики для открытия ComboBox при клике на поле
            ContractTypeComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            ProgramComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            PayerComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            ListenerComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            SignerComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            PaymentOptionComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            StudyOptionComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            ItogDocumentComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TimeOptionComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            
            // Добавляем обработчики SelectionChanged для отладки
            PayerComboBox.SelectionChanged += PayerComboBox_SelectionChanged;
            ListenerComboBox.SelectionChanged += ListenerComboBox_SelectionChanged;

            // Загружаем подписантов из статического списка (хранятся в коде, не в БД)
            var signers = GetSigners();
            SignerComboBox.ItemsSource = signers;
            // Выбираем первого подписанта по умолчанию
            if (signers.Any())
            {
                SignerComboBox.SelectedIndex = 0;
            }

            // Загружаем варианты оплаты из статического списка (хранятся в коде, не в БД)
            var paymentOptions = GetPaymentOptions();
            PaymentOptionComboBox.ItemsSource = paymentOptions;
            // Выбираем первый вариант по умолчанию
            if (paymentOptions.Any())
            {
                PaymentOptionComboBox.SelectedIndex = 0;
            }

            // Загружаем варианты учебной нагрузки из статического списка (хранятся в коде, не в БД)
            var studyOptions = GetStudyOptions();
            StudyOptionComboBox.ItemsSource = studyOptions;
            // Выбираем первый вариант по умолчанию
            if (studyOptions.Any())
            {
                StudyOptionComboBox.SelectedIndex = 0;
            }

            ContractDatePicker.SelectedDate = DateTime.Today;
            
            // Устанавливаем дату по умолчанию для договора
            if (ContractDatePicker.SelectedDate == null)
            {
                ContractDatePicker.SelectedDate = DateTime.Today;
            }
        }

        private void ContractTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ContractTypeComboBox.SelectedItem is ContractType contractType)
            {
                _selectedContractType = contractType;
                
                // Автоматически генерируем номер договора
                GenerateContractNumber(contractType);
                
                // Показываем/скрываем поля в зависимости от типа договора
                UpdateFieldsVisibility(contractType);
                
                // Обновляем список заказчиков в зависимости от типа договора
                UpdatePayersList(contractType);
            }
        }

        private void UpdatePayersList(ContractType contractType)
        {
            using (var db = new AppDbContext())
            {
                // Проверяем, является ли договор для юридических лиц
                bool isLegalEntityContract = contractType.Name != null && 
                    (contractType.Name.Contains("юридических лиц") || 
                     contractType.Name.Contains("юридическим лицом") ||
                     contractType.Name.Contains("юр лиц") ||
                     contractType.Name.Contains("юр.лиц"));

                if (isLegalEntityContract)
                {
                    // Загружаем организации
                    var organizations = db.Organizations
                        .AsNoTracking()
                        .ToList();
                    
                    // Создаем список OrganizationViewModel для отображения названия организации
                    var organizationViewModels = organizations.Select(org => new OrganizationViewModel
                    {
                        Organization = org,
                        DisplayName = org.OrganizationName
                    }).ToList();
                    
                    PayerComboBox.ItemsSource = organizationViewModels;
                    PayerComboBox.DisplayMemberPath = "DisplayName";
                    System.Windows.Controls.TextSearch.SetTextPath(PayerComboBox, "DisplayName");
                }
                else
                {
                    // Загружаем всех физических лиц
                    var individualPersons = db.Persons
                        .AsNoTracking()
                        .ToList();
                    
                    // Создаем список PersonViewModel для отображения ФИО
                    var personViewModels = individualPersons.Select(p => new PersonViewModel
                    {
                        Person = p,
                        DisplayName = p.FullName
                    }).ToList();
                    
                    PayerComboBox.ItemsSource = personViewModels;
                    PayerComboBox.DisplayMemberPath = "DisplayName";
                    System.Windows.Controls.TextSearch.SetTextPath(PayerComboBox, "DisplayName");
                }
                
                // Сбрасываем выбор заказчика
                PayerComboBox.SelectedItem = null;
            }
        }
        
        // Вспомогательные классы для отображения в ComboBox
        private class PersonViewModel
        {
            public Person Person { get; set; }
            public string DisplayName { get; set; }
        }
        
        private class OrganizationViewModel
        {
            public Organization Organization { get; set; }
            public string DisplayName { get; set; }
        }

        private void UpdateFieldsVisibility(ContractType contractType)
        {
            // Проверяем, является ли тип договора "ПК" (повышение квалификации)
            bool isPK = contractType.Name != null && 
                       (contractType.Name.Contains("ПК") || 
                        contractType.Name.Contains("повышение квалификации") ||
                        contractType.Name.Contains("повышения квалификации"));

            // Проверяем, является ли тип договора "ПП" (профессиональная переподготовка)
            bool isPP = contractType.Name != null && 
                       (contractType.Name.Contains("ПП") || 
                        contractType.Name.Contains("профпереподготовк") ||
                        contractType.Name.Contains("профессиональной переподготовки") ||
                        contractType.Name.Contains("профессиональная переподготовка"));

            // Показываем/скрываем поля для опций 1.4 и 1.5
            if (isPK || isPP)
            {
                // Показываем поля для ПК или ПП
                ItogDocumentLabel.Visibility = Visibility.Visible;
                ItogDocumentComboBox.Visibility = Visibility.Visible;
                TimeOptionLabel.Visibility = Visibility.Visible;
                TimeOptionComboBox.Visibility = Visibility.Visible;
                
                // Скрываем обычное поле "Учебная нагрузка"
                StudyOptionComboBox.Visibility = Visibility.Collapsed;
                StudyOptionLabel.Visibility = Visibility.Collapsed;
                
                // Загружаем опции в зависимости от типа договора
                if (isPK)
                {
                    LoadPKOptions();
                }
                else if (isPP)
                {
                    LoadPPOptions();
                }
            }
            else
            {
                // Скрываем поля для ПК/ПП
                ItogDocumentLabel.Visibility = Visibility.Collapsed;
                ItogDocumentComboBox.Visibility = Visibility.Collapsed;
                TimeOptionLabel.Visibility = Visibility.Collapsed;
                TimeOptionComboBox.Visibility = Visibility.Collapsed;
                
                // Показываем обычное поле "Учебная нагрузка"
                StudyOptionComboBox.Visibility = Visibility.Visible;
                StudyOptionLabel.Visibility = Visibility.Visible;
            }
        }

        private void LoadPKOptions()
        {
            // Загружаем опции для итогового документа (1.4) для ПК
            var itogOptions = GetItogDocumentOptions();
            ItogDocumentComboBox.ItemsSource = itogOptions;
            if (itogOptions.Any())
            {
                ItogDocumentComboBox.SelectedIndex = 0;
            }
            
            // Загружаем опции для учебной нагрузки (1.5) для ПК
            var timeOptions = GetTimeOptions();
            TimeOptionComboBox.ItemsSource = timeOptions;
            if (timeOptions.Any())
            {
                TimeOptionComboBox.SelectedIndex = 0;
            }
        }

        private void LoadPPOptions()
        {
            // Загружаем опции для итогового документа (1.4) для ПП
            var itogOptions = GetPPItogDocumentOptions();
            ItogDocumentComboBox.ItemsSource = itogOptions;
            if (itogOptions.Any())
            {
                ItogDocumentComboBox.SelectedIndex = 0;
            }
            
            // Загружаем опции для учебной нагрузки (1.5) для ПП
            var timeOptions = GetPPTimeOptions();
            TimeOptionComboBox.ItemsSource = timeOptions;
            if (timeOptions.Any())
            {
                TimeOptionComboBox.SelectedIndex = 0;
            }
        }

        private void ContractDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Перегенерируем номер договора при изменении даты, если тип договора выбран
            if (ContractTypeComboBox.SelectedItem is ContractType contractType)
            {
                // Проверяем, что текущий номер был сгенерирован автоматически (имеет правильный формат)
                string currentNumber = ContractNumberTextBox.Text;
                if (!string.IsNullOrWhiteSpace(currentNumber))
                {
                    string key = ExtractKeyFromContractTypeName(contractType.Name);
                    if (!string.IsNullOrWhiteSpace(key) && currentNumber.StartsWith(key + "-"))
                    {
                        // Номер был сгенерирован автоматически, перегенерируем с новой датой
                        GenerateContractNumber(contractType);
                    }
                }
                else
                {
                    // Если номер пустой, генерируем новый
                    GenerateContractNumber(contractType);
                }
            }
        }

        private void GenerateContractNumber(ContractType contractType)
        {
            if (contractType == null || string.IsNullOrWhiteSpace(contractType.Name))
                return;

            // Извлекаем ключ из названия типа договора (например, "Договор ДОП физ лиц" -> "ДОП")
            string key = ExtractKeyFromContractTypeName(contractType.Name);
            
            if (string.IsNullOrWhiteSpace(key))
                return;

            // Используем дату из DatePicker или текущую дату
            DateTime contractDate = ContractDatePicker.SelectedDate ?? DateTime.Today;
            string dateStr = contractDate.ToString("dd.MM.yy");

            using (var db = new AppDbContext())
            {
                // Находим все договоры с таким же ключом (формат: "ДОП-01 04.12.25")
                var existingContracts = db.Contracts
                    .Where(c => c.ContractNumber.StartsWith(key + "-"))
                    .Select(c => c.ContractNumber)
                    .ToList();

                // Определяем следующий номер среди всех договоров с таким ключом (независимо от даты)
                int nextNumber = 1;
                if (existingContracts.Any())
                {
                    // Извлекаем номера из всех существующих договоров с таким ключом
                    // Формат: "ДОП-01 04.12.25" -> извлекаем "01"
                    // Ищем паттерн "КЛЮЧ-XX" (любая дата после)
                    string escapedKey = System.Text.RegularExpressions.Regex.Escape(key);
                    var numbers = existingContracts
                        .Select(cn => 
                        {
                            // Ищем паттерн "КЛЮЧ-XX" (любые цифры после дефиса)
                            var match = System.Text.RegularExpressions.Regex.Match(cn, $@"^{escapedKey}-(\d{{2}})\s+");
                            if (match.Success && match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int num))
                                return num;
                            return 0;
                        })
                        .Where(n => n > 0)
                        .ToList();

                    if (numbers.Any())
                    {
                        nextNumber = numbers.Max() + 1;
                    }
                }

                // Формируем номер договора в формате: "ДОП-01 04.12.25"
                ContractNumberTextBox.Text = $"{key}-{nextNumber:D2} {dateStr}";
            }
        }

        private string ExtractKeyFromContractTypeName(string contractTypeName)
        {
            if (string.IsNullOrWhiteSpace(contractTypeName))
                return null;

            // Ищем ключ после слова "Договор" (например, "Договор ДОП физ лиц" -> "ДОП")
            // Или просто ищем последовательность заглавных букв в начале или после пробела
            var parts = contractTypeName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                // Если часть состоит только из заглавных букв и цифр (2-4 символа), это ключ
                if (part.Length >= 2 && part.Length <= 4 && 
                    part.All(c => char.IsUpper(c) || char.IsDigit(c)) && 
                    part.Any(c => char.IsUpper(c)))
                {
                    return part;
                }
            }

            // Если не нашли, пробуем найти после слова "Договор"
            int index = contractTypeName.IndexOf("Договор", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                string after = contractTypeName.Substring(index + "Договор".Length).Trim();
                var words = after.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    string firstWord = words[0];
                    if (firstWord.Length >= 2 && firstWord.Length <= 4 &&
                        firstWord.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                        firstWord.Any(c => char.IsUpper(c)))
                    {
                        return firstWord;
                    }
                }
            }

            return null;
        }

        private void CreateContractButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (ContractTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите тип договора!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем тип договора один раз
            var contractType = (ContractType)ContractTypeComboBox.SelectedItem;

            // Автоматически генерируем номер договора, если он не заполнен
            if (string.IsNullOrWhiteSpace(ContractNumberTextBox.Text))
            {
                GenerateContractNumber(contractType);
            }

            if (string.IsNullOrWhiteSpace(ContractNumberTextBox.Text))
            {
                MessageBox.Show("Не удалось сгенерировать номер договора. Выберите тип договора или введите номер вручную!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ContractDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Выберите дату договора!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ProgramComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите программу обучения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем заказчика
            bool payerValid = false;
            
            if (PayerComboBox.SelectedItem != null)
            {
                if (PayerComboBox.SelectedItem is PersonViewModel payerVm && payerVm.Person != null)
                {
                    payerValid = true;
                }
                else if (PayerComboBox.SelectedItem is OrganizationViewModel orgVm && orgVm.Organization != null)
                {
                    payerValid = true;
                }
                else if (PayerComboBox.SelectedItem is Person)
                {
                    payerValid = true;
                }
            }
            
            if (!payerValid)
            {
                MessageBox.Show("Выберите заказчика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем слушателя
            bool listenerValid = false;
            if (ListenerComboBox.SelectedItem != null)
            {
                if (ListenerComboBox.SelectedItem is PersonViewModel listenerVm && listenerVm.Person != null)
                {
                    listenerValid = true;
                }
                else if (ListenerComboBox.SelectedItem is Person)
                {
                    listenerValid = true;
                }
            }
            
            if (!listenerValid)
            {
                MessageBox.Show("Выберите слушателя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    // Проверяем, существует ли уже договор с таким номером
                    if (db.Contracts.Any(c => c.ContractNumber == ContractNumberTextBox.Text))
                    {
                        MessageBox.Show("Договор с таким номером уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Сохраняем выбранные опции
                    string itogDocumentOptionKey = null;
                    string timeOptionKey = null;
                    string studyOptionKey = null;
                    int? signerId = null;
                    string paymentOptionKey = null;

                    // Определяем тип договора для установки значений по умолчанию
                    bool isPK = contractType != null && contractType.Name != null && 
                               (contractType.Name.Contains("ПК") || 
                                contractType.Name.Contains("повышение квалификации") ||
                                contractType.Name.Contains("повышения квалификации"));
                    bool isPP = contractType != null && contractType.Name != null && 
                               (contractType.Name.Contains("ПП") || 
                                contractType.Name.Contains("профпереподготовк") ||
                                contractType.Name.Contains("профессиональной переподготовки") ||
                                contractType.Name.Contains("профессиональная переподготовка"));

                    if (ItogDocumentComboBox.SelectedItem is ItogDocumentOption selectedItogOption)
                    {
                        itogDocumentOptionKey = selectedItogOption.OptionKey;
                    }
                    else if (isPK || isPP)
                    {
                        // Если опция не выбрана, используем первую по умолчанию
                        if (isPK)
                        {
                            var defaultOption = GetItogDocumentOptions().FirstOrDefault();
                            itogDocumentOptionKey = defaultOption?.OptionKey;
                        }
                        else if (isPP)
                        {
                            var defaultOption = GetPPItogDocumentOptions().FirstOrDefault();
                            itogDocumentOptionKey = defaultOption?.OptionKey;
                        }
                    }

                    if (TimeOptionComboBox.SelectedItem is TimeOption selectedTimeOption)
                    {
                        timeOptionKey = selectedTimeOption.OptionKey;
                    }
                    else if (isPK || isPP)
                    {
                        // Если опция не выбрана, используем первую по умолчанию
                        if (isPK)
                        {
                            var defaultOption = GetTimeOptions().FirstOrDefault();
                            timeOptionKey = defaultOption?.OptionKey;
                        }
                        else if (isPP)
                        {
                            var defaultOption = GetPPTimeOptions().FirstOrDefault();
                            timeOptionKey = defaultOption?.OptionKey;
                        }
                    }

                    if (StudyOptionComboBox.SelectedItem is StudyOption selectedStudyOption)
                    {
                        studyOptionKey = selectedStudyOption.OptionKey;
                    }
                    else if (!isPK && !isPP)
                    {
                        // Для других типов договоров используем первую опцию по умолчанию
                        var defaultOption = GetStudyOptions().FirstOrDefault();
                        studyOptionKey = defaultOption?.OptionKey;
                    }

                    if (SignerComboBox.SelectedItem is Signer selectedSigner)
                    {
                        signerId = selectedSigner.Id;
                    }
                    else
                    {
                        // По умолчанию - первый подписант
                        var signers = GetSigners();
                        if (signers.Any())
                        {
                            signerId = signers.First().Id;
                        }
                    }

                    if (PaymentOptionComboBox.SelectedItem is PaymentOption selectedPaymentOption)
                    {
                        paymentOptionKey = selectedPaymentOption.OptionKey;
                    }
                    else
                    {
                        // По умолчанию - полная оплата
                        paymentOptionKey = "option1";
                    }

                    // Создаем договор в БД
                    var contract = new Contract
                    {
                        ContractNumber = ContractNumberTextBox.Text,
                        ContractDate = ContractDatePicker.SelectedDate.Value,
                        ContractTypeId = ((ContractType)ContractTypeComboBox.SelectedItem).Id,
                        ProgramId = ((LearningProgram)ProgramComboBox.SelectedItem).Id,
                        StartDate = StartDatePicker.SelectedDate,
                        EndDate = EndDatePicker.SelectedDate,
                        PayerId = PayerComboBox.SelectedItem is PersonViewModel payerVm ? payerVm.Person.Id : 
                                  PayerComboBox.SelectedItem is OrganizationViewModel orgVm ? orgVm.Organization.Id : 
                                  ((Person)PayerComboBox.SelectedItem).Id,
                        ListenerId = ListenerComboBox.SelectedItem is PersonViewModel listenerVm ? listenerVm.Person.Id : ((Person)ListenerComboBox.SelectedItem).Id,
                        CreatedAt = DateTime.Now,
                        ItogDocumentOptionKey = itogDocumentOptionKey,
                        TimeOptionKey = timeOptionKey,
                        StudyOptionKey = studyOptionKey,
                        SignerId = signerId,
                        PaymentOptionKey = paymentOptionKey
                    };

                    // Добавляем договор в контекст
                    db.Contracts.Add(contract);
                    
                    // Сохраняем изменения
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                    {
                        // Логируем детали ошибки для отладки
                        string errorDetails = dbEx.Message;
                        if (dbEx.InnerException != null)
                        {
                            errorDetails += "\n\nВнутреннее исключение: " + dbEx.InnerException.Message;
                        }
                        throw new Exception(errorDetails, dbEx);
                    }

                    // Перезагружаем договор из БД, чтобы получить актуальные данные
                    // Используем AsNoTracking() чтобы избежать конфликтов отслеживания
                    var savedContract = db.Contracts
                        .AsNoTracking()
                        .Include(c => c.ContractType)
                        .Include(c => c.Program)
                        .Include(c => c.Payer)
                        .Include(c => c.Listener)
                        .FirstOrDefault(c => c.Id == contract.Id);

                    if (savedContract == null)
                    {
                        throw new InvalidOperationException("Не удалось загрузить сохраненный договор");
                    }

                    // Создаем Word документ
                    CreateContractDocument(savedContract, db);
                    
                    // Создаем личную карточку слушателя
                    CreateListenerCard(savedContract, db);

                    MessageBox.Show("Договор и личная карточка слушателя успешно созданы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка при создании договора: {ex.Message}";
                
                // Если есть внутреннее исключение, добавляем его детали
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nДетали: {ex.InnerException.Message}";
                    
                    // Если это исключение от EF Core, добавляем больше деталей
                    if (ex.InnerException.InnerException != null)
                    {
                        errorMessage += $"\n\nПодробности: {ex.InnerException.InnerException.Message}";
                    }
                }
                
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static string GenerateContractDocumentForView(Contract contract, AppDbContext db)
        {
            var contractType = db.ContractTypes.Find(contract.ContractTypeId);
            if (contractType == null || string.IsNullOrWhiteSpace(contractType.FilePath) || !File.Exists(contractType.FilePath))
            {
                throw new FileNotFoundException("Шаблон договора не найден!");
            }

            // Загружаем заказчика и слушателя
            var payer = db.Persons.FirstOrDefault(p => p.Id == contract.PayerId);
            var listener = db.Persons.FirstOrDefault(p => p.Id == contract.ListenerId);
            
            if (payer == null)
            {
                throw new InvalidOperationException("Заказчик не найден");
            }
            
            if (listener == null)
            {
                throw new InvalidOperationException("Слушатель не найден");
            }
            
            var program = db.LearningPrograms.Find(contract.ProgramId);
            if (program == null)
            {
                throw new InvalidOperationException("Программа не найдена");
            }

            // Загружаем контакты
            Models.Contacts payerContacts = null;
            if (payer.ContactsId.HasValue)
            {
                payerContacts = db.Contacts.Where(c => c.Id == payer.ContactsId.Value).FirstOrDefault();
                if (payerContacts == null)
                {
                    payerContacts = db.Contacts.Find(payer.ContactsId.Value);
                }
            }
            
            Models.Contacts listenerContacts = null;
            if (listener.ContactsId.HasValue)
            {
                listenerContacts = db.Contacts.Where(c => c.Id == listener.ContactsId.Value).FirstOrDefault();
                if (listenerContacts == null)
                {
                    listenerContacts = db.Contacts.Find(listener.ContactsId.Value);
                }
            }

            // Загружаем паспортные данные
            var payerPassport = db.Passports.FirstOrDefault(p => p.PersonId == payer.Id);
            var listenerPassport = db.Passports.FirstOrDefault(p => p.PersonId == listener.Id);

            // Загружаем данные организации (если заказчик - юридическое лицо)
            var payerOrganization = db.Organizations.FirstOrDefault(o => o.Id == contract.PayerId);

            // Загружаем образование слушателя для плейсхолдера {{DIplom_Sculatel}}
            var listenerEducation = db.Educations
                .Include(e => e.BaseEducation)
                .FirstOrDefault(e => e.PersonId == listener.Id);

            // Формируем словарь замен
            var replacements = BuildReplacementsDictionary(contract, contractType, payer, listener, program, payerContacts, listenerContacts, payerPassport, listenerPassport, payerOrganization, listenerEducation);

            // Создаем путь для сохранения договора
            string outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Договоры");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string outputFileName = $"Договор_{contract.ContractNumber}_{listener.LastName}_{DateTime.Now:yyyyMMdd}.docx";
            string outputPath = Path.Combine(outputDirectory, outputFileName);

            // Заменяем плейсхолдеры
            var wordService = new WordDocumentService();
            wordService.ReplacePlaceholders(contractType.FilePath, outputPath, replacements);

            // Создаем PDF версию договора
            try
            {
                string pdfFileName = Path.ChangeExtension(outputFileName, ".pdf");
                string pdfPath = Path.Combine(outputDirectory, pdfFileName);
                wordService.ConvertToPdf(outputPath, pdfPath);
            }
            catch (Exception)
            {
                // Если не удалось создать PDF, просто игнорируем ошибку
                // (не показываем сообщение, так как это метод для просмотра)
            }

            return outputPath;
        }

        private static Dictionary<string, string> BuildReplacementsDictionary(Contract contract, ContractType contractType, Person payer, Person listener, LearningProgram program, 
            Models.Contacts payerContacts, Models.Contacts listenerContacts, Passport payerPassport, Passport listenerPassport, Organization payerOrganization = null, Education listenerEducation = null)
        {
            var replacements = new Dictionary<string, string>();

            // Данные заказчика
            string payerPhone = payerContacts?.ContactPhone ?? "";
            string payerEmail = payerContacts?.Email ?? "";
            
            replacements["{{FIO_Zakazchik}}"] = $"{payer.LastName} {payer.FirstName} {payer.Patronymic}".Trim();
            replacements["{{lastname_zakaz}}"] = payer.LastName;
            replacements["{{Name_zakaz.}}"] = GetInitialLetterStatic(payer.FirstName);
            replacements["{{Firstname_zakaz. }}"] = GetInitialLetterStatic(payer.Patronymic);
            replacements["{{Firstname_zakaz.}}"] = GetInitialLetterStatic(payer.Patronymic);
            string payerInitials = GetInitialsStatic(payer.FirstName, payer.Patronymic);
            replacements["{{lastname_zakaz Name_zakaz. Firstname_zakaz. }}"] = $"{payer.LastName} {payerInitials}".Trim();
            replacements["{{lastname_zakaz Name_zakaz. Firstname_zakaz.}}"] = $"{payer.LastName} {payerInitials}".Trim();
            replacements["{{contact_phone_zakazchik}}"] = payerPhone;
            replacements["{{contact_phone_zakazchik}"] = payerPhone;
            replacements["{{EMAIL_zakazchik}}"] = payerEmail;
            replacements["{{EMAIL_zakazchik}"] = payerEmail;
            replacements["{{email_zakazchik}}"] = payerEmail;
            replacements["{{Email_zakazchik}}"] = payerEmail;
            
            if (payerPassport != null)
            {
                replacements["{{Seria Number, Kem_Vidan}}"] = $"{payerPassport.Series} {payerPassport.Number}, выдан {payerPassport.IssuedBy}";
            }
            else
            {
                replacements["{{Seria Number, Kem_Vidan}}"] = "";
            }
            replacements["{{Adress_Registracii}}"] = payerPassport?.RegistrationAddress ?? "";
            replacements["{{Snils}}"] = payer.Snils ?? "";

            // Данные организации заказчика (если заказчик - юридическое лицо)
            if (payerOrganization != null)
            {
                replacements["{{Organisation_Name}}"] = payerOrganization.OrganizationName ?? "";
                string shortOrgName = ExtractShortOrganizationName(payerOrganization.OrganizationName);
                replacements["{{Organisation_Zakachik}}"] = $"ООО \"{shortOrgName}\"";
                replacements["{{Name_Direktororganisation}}"] = payerOrganization.DirectorFio ?? "";
                
                // Разбираем ФИО директора на фамилию и инициалы
                string directorFio = payerOrganization.DirectorFio ?? "";
                string[] directorParts = directorFio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (directorParts.Length >= 1)
                {
                    replacements["{{lastname_zakaz_org }}"] = directorParts[0];
                    replacements["{{lastname_zakaz_org}}"] = directorParts[0];
                }
                else
                {
                    replacements["{{lastname_zakaz_org }}"] = "";
                    replacements["{{lastname_zakaz_org}}"] = "";
                }
                
                if (directorParts.Length >= 2)
                {
                    replacements["{{Name_zakaz_org.}}"] = GetInitialLetterStatic(directorParts[1]);
                }
                else
                {
                    replacements["{{Name_zakaz_org.}}"] = "";
                }
                
                if (directorParts.Length >= 3)
                {
                    replacements["{{Firstname_zakaz_org. }}"] = GetInitialLetterStatic(directorParts[2]);
                    replacements["{{Firstname_zakaz_org.}}"] = GetInitialLetterStatic(directorParts[2]);
                }
                else
                {
                    replacements["{{Firstname_zakaz_org. }}"] = "";
                    replacements["{{Firstname_zakaz_org.}}"] = "";
                }
                
                replacements["{{INN_Zakachik}}"] = payerOrganization.Inn ?? "";
                replacements["{{KPP_Zakachik}}"] = payerOrganization.Kpp ?? "";
                replacements["{{OGRN_Zakachik}}"] = payerOrganization.Ogrn ?? "";
                replacements["{{Addres_Zakachik}}"] = payerOrganization.LegalAddress ?? "";
                
                // Переопределяем телефон и email для организации
                if (!string.IsNullOrWhiteSpace(payerOrganization.Phone))
                {
                    replacements["{{contact_phone_zakazchik}}"] = payerOrganization.Phone;
                    replacements["{{contact_phone_zakazchik}"] = payerOrganization.Phone;
                }
                if (!string.IsNullOrWhiteSpace(payerOrganization.Email))
                {
                    replacements["{{EMAIL_zakazchik}}"] = payerOrganization.Email;
                    replacements["{{EMAIL_zakazchik}"] = payerOrganization.Email;
                    replacements["{{email_zakazchik}}"] = payerOrganization.Email;
                    replacements["{{Email_zakazchik}}"] = payerOrganization.Email;
                }
            }
            else
            {
                // Если заказчик - физическое лицо, заполняем пустыми значениями
                replacements["{{Organisation_Name}}"] = "";
                replacements["{{Organisation_Zakachik}}"] = "ООО";
                replacements["{{Name_Direktororganisation}}"] = "";
                replacements["{{lastname_zakaz_org }}"] = "";
                replacements["{{lastname_zakaz_org}}"] = "";
                replacements["{{Name_zakaz_org.}}"] = "";
                replacements["{{Firstname_zakaz_org. }}"] = "";
                replacements["{{Firstname_zakaz_org.}}"] = "";
                replacements["{{INN_Zakachik}}"] = "";
                replacements["{{KPP_Zakachik}}"] = "";
                replacements["{{OGRN_Zakachik}}"] = "";
                replacements["{{Addres_Zakachik}}"] = "";
            }

            // Данные слушателя
            string listenerPhone = listenerContacts?.ContactPhone ?? "";
            string listenerEmail = listenerContacts?.Email ?? "";
            
            replacements["{{FIO_Slushtel}}"] = $"{listener.LastName} {listener.FirstName} {listener.Patronymic}".Trim();
            replacements["{{FIO_Slushatel}}"] = $"{listener.LastName} {listener.FirstName} {listener.Patronymic}".Trim();
            replacements["{{lastname}}"] = listener.LastName;
            replacements["{{Name.}}"] = GetInitialLetterStatic(listener.FirstName);
            replacements["{{Firstname.}}"] = GetInitialLetterStatic(listener.Patronymic);
            replacements["{{Firstname. }}"] = GetInitialLetterStatic(listener.Patronymic);
            string listenerInitials = GetInitialsStatic(listener.FirstName, listener.Patronymic);
            replacements["{{lastname Name. Firstname.}}"] = $"{listener.LastName} {listenerInitials}".Trim();
            replacements["{{lastname Name. Firstname. }}"] = $"{listener.LastName} {listenerInitials}".Trim();
            replacements["{{birthday}}"] = listener.DateOfBirth.ToString("dd.MM.yyyy");
            replacements["{{contact_phone}}"] = listenerPhone;
            replacements["{{EMAIL}}"] = listenerEmail;
            replacements["{{EMAIL}"] = listenerEmail;
            replacements["{{email}}"] = listenerEmail;
            replacements["{{Email}}"] = listenerEmail;
            replacements["{{Snils_Slushatel}}"] = listener.Snils ?? "";
            replacements["{{SNILS_Slushatel}}"] = listener.Snils ?? "";
            replacements["{{snils_slushatel}}"] = listener.Snils ?? "";

            // Диплом слушателя
            if (listenerEducation != null && listenerEducation.BaseEducation != null)
            {
                replacements["{{DIplom_Sculatel}}"] = listenerEducation.BaseEducation.Name ?? "";
            }
            else
            {
                replacements["{{DIplom_Sculatel}}"] = "";
            }

            // Данные программы
            replacements["{{Program_name}}"] = program.Name;
            replacements["{{Date Start}}"] = contract.StartDate?.ToString("dd.MM.yyyy") ?? "";
            replacements["{{Date End}}"] = contract.EndDate?.ToString("dd.MM.yyyy") ?? "";
            
            // Дата договора
            DateTime currentDate = contract.ContractDate;
            replacements["{{ContractDate}}"] = currentDate.ToString("dd.MM.yyyy");
            replacements["{{Contract_Date}}"] = currentDate.ToString("dd.MM.yyyy");
            replacements["{{nm}}"] = currentDate.Day.ToString("D2");
            replacements["{{mounts}}"] = GetMonthNameStatic(currentDate.Month);
            replacements["{{yr}}"] = currentDate.Year.ToString();
            replacements["{{number_dogovor}}"] = contract.ContractNumber;

            // Проверяем тип договора для ПК/ПП
            bool isPK = contractType != null && contractType.Name != null && 
                       (contractType.Name.Contains("ПК") || 
                        contractType.Name.Contains("повышение квалификации") ||
                        contractType.Name.Contains("повышения квалификации"));
            bool isPP = contractType != null && contractType.Name != null && 
                       (contractType.Name.Contains("ПП") || 
                        contractType.Name.Contains("профпереподготовк") ||
                        contractType.Name.Contains("профессиональной переподготовки") ||
                        contractType.Name.Contains("профессиональная переподготовка"));

            // Подписант - берем из сохраненного в БД
            if (contract.SignerId.HasValue)
            {
                var signers = GetSignersStatic();
                var selectedSigner = signers.FirstOrDefault(s => s.Id == contract.SignerId.Value);
                if (selectedSigner != null)
                {
                    replacements["{{choice_signer}}"] = selectedSigner.Text;
                }
                else
                {
                    replacements["{{choice_signer}}"] = "";
                }
            }
            else
            {
                // По умолчанию - первый
                var signers = GetSignersStatic();
                if (signers.Any())
                {
                    replacements["{{choice_signer}}"] = signers.First().Text;
                }
                else
                {
                    replacements["{{choice_signer}}"] = "";
                }
            }

            // Вариант оплаты - берем из сохраненного в БД
            if (!string.IsNullOrEmpty(contract.PaymentOptionKey))
            {
                if (contract.PaymentOptionKey == "option1")
                {
                    // Полная оплата
                    replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
                    replacements["{{option2}}"] = "";
                }
                else if (contract.PaymentOptionKey == "option2")
                {
                    // Оплата частями - используем плейсхолдеры для Price и Date last price
                    replacements["{{option1}}"] = "";
                    replacements["{{option2}}"] = "Оплата осуществляется в следующем порядке: а) Аванс 50 % — {{Price}} рублей, предоплата до начала обучения.\n" +
                                                  "б) Оставшиеся 50 % — {{Price}} рублей. Срок: не позднее {{Date last price}} г.\n" +
                                                  "Просрочка второго платежа более 30 календарных дней даёт Исполнителю право приостановить доступ к LMS и/или расторгнуть договор с удержанием фактических расходов.";
                }
            }
            else
            {
                // По умолчанию - полная оплата
                replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
                replacements["{{option2}}"] = "";
            }

            if (isPK || isPP)
            {
                // Для типа договора ПК или ПП используем опции 1.4 и 1.5
                
                // Опция итогового документа (1.4) - берем из сохраненного в БД
                if (!string.IsNullOrEmpty(contract.ItogDocumentOptionKey))
                {
                    List<ItogDocumentOption> itogOptions = isPK ? GetItogDocumentOptionsStatic() : GetPPItogDocumentOptionsStatic();
                    var selectedItogOption = itogOptions.FirstOrDefault(o => o.OptionKey == contract.ItogDocumentOptionKey);
                    if (selectedItogOption != null)
                    {
                        replacements[$"{{{{{selectedItogOption.OptionKey}}}}}"] = selectedItogOption.Text;
                        for (int i = 1; i <= 2; i++)
                        {
                            string optionKey = $"Option_Itog{i}";
                            if (optionKey != selectedItogOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                    else
                    {
                        replacements["{{Option_Itog1}}"] = "";
                        replacements["{{Option_Itog2}}"] = "";
                    }
                }
                else
                {
                    // По умолчанию - первая опция
                    if (isPK)
                    {
                        replacements["{{Option_Itog1}}"] = GetItogDocumentOptionsStatic().FirstOrDefault()?.Text ?? "";
                    }
                    else if (isPP)
                    {
                        replacements["{{Option_Itog1}}"] = GetPPItogDocumentOptionsStatic().FirstOrDefault()?.Text ?? "";
                    }
                    replacements["{{Option_Itog2}}"] = "";
                }

                // Опция учебной нагрузки (1.5) - берем из сохраненного в БД
                if (!string.IsNullOrEmpty(contract.TimeOptionKey))
                {
                    List<TimeOption> timeOptions = isPK ? GetTimeOptionsStatic() : GetPPTimeOptionsStatic();
                    var selectedTimeOption = timeOptions.FirstOrDefault(o => o.OptionKey == contract.TimeOptionKey);
                    if (selectedTimeOption != null)
                    {
                        replacements[$"{{{{{selectedTimeOption.OptionKey}}}}}"] = selectedTimeOption.Text;
                        for (int i = 1; i <= 6; i++)
                        {
                            string optionKey = $"Option_Time{i}";
                            if (optionKey != selectedTimeOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                    else
                    {
                        for (int i = 1; i <= 6; i++)
                        {
                            replacements[$"{{{{Option_Time{i}}}}}"] = "";
                        }
                    }
                }
                else
                {
                    // По умолчанию - первая опция
                    TimeOption defaultTimeOption = null;
                    if (isPK)
                    {
                        defaultTimeOption = GetTimeOptionsStatic().FirstOrDefault();
                    }
                    else if (isPP)
                    {
                        defaultTimeOption = GetPPTimeOptionsStatic().FirstOrDefault();
                    }
                    
                    if (defaultTimeOption != null)
                    {
                        replacements[$"{{{{{defaultTimeOption.OptionKey}}}}}"] = defaultTimeOption.Text;
                        for (int i = 1; i <= 6; i++)
                        {
                            string optionKey = $"Option_Time{i}";
                            if (optionKey != defaultTimeOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                }

                // Скрываем старые опции учебной нагрузки
                for (int i = 1; i <= 5; i++)
                {
                    replacements[$"{{{{Option_study{i}}}}}"] = "";
                }
            }
            else
            {
                // Для других типов договоров используем старые опции учебной нагрузки
                
                // Скрываем опции ПК
                replacements["{{Option_Itog1}}"] = "";
                replacements["{{Option_Itog2}}"] = "";
                for (int i = 1; i <= 6; i++)
                {
                    replacements[$"{{{{Option_Time{i}}}}}"] = "";
                }

                // Вариант учебной нагрузки - берем из сохраненного в БД
                if (!string.IsNullOrEmpty(contract.StudyOptionKey))
                {
                    var studyOptions = GetStudyOptionsStatic();
                    var selectedStudyOption = studyOptions.FirstOrDefault(o => o.OptionKey == contract.StudyOptionKey);
                    if (selectedStudyOption != null)
                    {
                        replacements[$"{{{{{selectedStudyOption.OptionKey}}}}}"] = selectedStudyOption.Text;
                        for (int i = 1; i <= 5; i++)
                        {
                            string optionKey = $"Option_study{i}";
                            if (optionKey != selectedStudyOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                    else
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            replacements[$"{{{{Option_study{i}}}}}"] = "";
                        }
                    }
                }
                else
                {
                    // По умолчанию - первая опция
                    var defaultStudyOption = GetStudyOptionsStatic().FirstOrDefault();
                    if (defaultStudyOption != null)
                    {
                        replacements[$"{{{{{defaultStudyOption.OptionKey}}}}}"] = defaultStudyOption.Text;
                        for (int i = 1; i <= 5; i++)
                        {
                            string optionKey = $"Option_study{i}";
                            if (optionKey != defaultStudyOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                }
            }

            // Стоимость
            replacements["{{Price full}}"] = program.Price.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            replacements["{{Price}}"] = (program.Price / 2).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

            // Дата последнего платежа
            if (contract.EndDate.HasValue)
            {
                replacements["{{Date last price}}"] = contract.EndDate.Value.AddDays(30).ToString("dd.MM.yyyy");
            }
            else
            {
                replacements["{{Date last price}}"] = "";
            }

            // Часы программы
            replacements["{{Program_Time}}"] = program.Hours.ToString();

            // Формат программы (форма обучения)
            string formatProgram = "";
            if (!string.IsNullOrEmpty(program.Format))
            {
                if (program.Format.Contains("Индивидуально с преподавателем"))
                {
                    formatProgram = "заочная форма, с применением дистанционных образовательных технологий";
                }
                else if (program.Format.Contains("С преподавателем в группе"))
                {
                    formatProgram = "очная форма";
                }
                else
                {
                    // Если формат не распознан, используем исходное значение
                    formatProgram = program.Format;
                }
            }
            replacements["{{Format_Program}}"] = formatProgram;

            return replacements;
        }

        private static string GetInitialsStatic(string firstName, string patronymic)
        {
            string initials = "";
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                initials += firstName.Trim()[0].ToString().ToUpper() + ".";
            }
            if (!string.IsNullOrWhiteSpace(patronymic))
            {
                initials += patronymic.Trim()[0].ToString().ToUpper() + ".";
            }
            return initials;
        }

        private static string GetInitialLetterStatic(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }
            return name.Trim()[0].ToString().ToUpper() + ".";
        }

        private static string ExtractShortOrganizationName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "";
            }

            string result = fullName.Trim();

            // Удаляем префиксы организационно-правовых форм
            string[] prefixes = {
                "Общество с ограниченной ответственностью",
                "Акционерное общество",
                "Публичное акционерное общество",
                "Непубличное акционерное общество",
                "ООО",
                "АО",
                "ПАО",
                "ЗАО",
                "ОАО"
            };

            foreach (var prefix in prefixes)
            {
                if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Substring(prefix.Length).Trim();
                    break;
                }
            }

            // Удаляем кавычки (и обычные, и ёлочки)
            result = result.Trim('"', '«', '»', ' ');

            return result;
        }

        private static string GetMonthNameStatic(int month)
        {
            string[] months = { "", "января", "февраля", "марта", "апреля", "мая", "июня",
                              "июля", "августа", "сентября", "октября", "ноября", "декабря" };
            if (month >= 1 && month <= 12)
                return months[month];
            return month.ToString();
        }

        private static List<Signer> GetSignersStatic()
        {
            return new List<Signer>
            {
                new Signer
                {
                    Id = 1,
                    Name = "Шимбирева Елена",
                    Text = "в лице Генерального директора Шимбиревой Елены Анатольевны, действующей на основании Устава, с одной стороны"
                },
                new Signer
                {
                    Id = 2,
                    Name = "Шимбирев Андрей",
                    Text = "в лице Исполнительного директора Шимбирёва Андрея Андреевича, действующей на основании Доверенности №Д/01 от 01 сентября 2025 г., с одной стороны"
                },
                new Signer
                {
                    Id = 3,
                    Name = "Бойцова Екатерина",
                    Text = "в лице Руководителя направления дополнительного образования Бойцовой Екатерины Юрьевны, действующей на основании Доверенности №Д/02 от 01 октября 2025 г., с одной стороны"
                }
            };
        }

        private static List<StudyOption> GetStudyOptionsStatic()
        {
            return new List<StudyOption>
            {
                new StudyOption
                {
                    Id = 1,
                    Name = "Опция № 1: 1 час/нед, 20 недель",
                    OptionKey = "Option_study1",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 1 академический час в неделю; общая продолжительность освоения — 20 недель."
                },
                new StudyOption
                {
                    Id = 2,
                    Name = "Опция № 2: 2 часа/нед, 10 недель",
                    OptionKey = "Option_study2",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 2 академических часа в неделю; общая продолжительность освоения — 10 недель."
                },
                new StudyOption
                {
                    Id = 3,
                    Name = "Опция № 3: 4 часа/нед, 5 недель",
                    OptionKey = "Option_study3",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 4 академических часа в неделю; общая продолжительность освоения — 5 недель."
                },
                new StudyOption
                {
                    Id = 4,
                    Name = "Опция № 4: 8 часов/нед, 2,5 недели",
                    OptionKey = "Option_study4",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 8 академических часов в неделю; общая продолжительность освоения — 2,5 недели."
                },
                new StudyOption
                {
                    Id = 5,
                    Name = "Опция № 5: 10 часов/нед, 2 недели",
                    OptionKey = "Option_study5",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 10 академических часов в неделю; общая продолжительность освоения — 2 недели."
                }
            };
        }

        private static List<ItogDocumentOption> GetItogDocumentOptionsStatic()
        {
            // Опции для итогового документа (1.4) для типа договора ПК
            return new List<ItogDocumentOption>
            {
                new ItogDocumentOption
                {
                    Id = 1,
                    Name = "Опция № 1: Удостоверение вручается по окончании",
                    OptionKey = "Option_Itog1",
                    Text = "удостоверение о повышении квалификации вручается по окончании;"
                },
                new ItogDocumentOption
                {
                    Id = 2,
                    Name = "Опция № 2: Удостоверение выдаётся одновременно с дипломом",
                    OptionKey = "Option_Itog2",
                    Text = "удостоверение выдаётся одновременно с дипломом СПО/ВО (ч. 16 ст. 76 ФЗ-273). До этого момента удостоверение хранится у Исполнителя."
                }
            };
        }

        private static List<TimeOption> GetTimeOptionsStatic()
        {
            // Опции для учебной нагрузки (1.5) для типа договора ПК
            return new List<TimeOption>
            {
                new TimeOption
                {
                    Id = 1,
                    Name = "Опция № 1: 3 часа/нед, 21 неделя",
                    OptionKey = "Option_Time1",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 21 неделя."
                },
                new TimeOption
                {
                    Id = 2,
                    Name = "Опция № 2: 6 часов/нед, 11 недель",
                    OptionKey = "Option_Time2",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 11 недель."
                },
                new TimeOption
                {
                    Id = 3,
                    Name = "Опция № 3: 12 часов/нед, 6 недель",
                    OptionKey = "Option_Time3",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 6 недель."
                },
                new TimeOption
                {
                    Id = 4,
                    Name = "Опция № 4: 15 часов/нед, 5 недель",
                    OptionKey = "Option_Time4",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 5 недель."
                },
                new TimeOption
                {
                    Id = 5,
                    Name = "Опция № 5: 30 часов/нед, 3 недели",
                    OptionKey = "Option_Time5",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели."
                },
                new TimeOption
                {
                    Id = 6,
                    Name = "Опция № 6: 32 часа/нед, 3 недели",
                    OptionKey = "Option_Time6",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели."
                }
            };
        }

        private static List<ItogDocumentOption> GetPPItogDocumentOptionsStatic()
        {
            // Опции для итогового документа (1.4) для типа договора ПП
            return new List<ItogDocumentOption>
            {
                new ItogDocumentOption
                {
                    Id = 1,
                    Name = "Опция № 1: Диплом вручается по окончании",
                    OptionKey = "Option_Itog1",
                    Text = "диплом о профпереподготовке вручается по окончании;"
                },
                new ItogDocumentOption
                {
                    Id = 2,
                    Name = "Опция № 2: Диплом выдаётся одновременно с дипломом",
                    OptionKey = "Option_Itog2",
                    Text = "диплом выдаётся одновременно с дипломом СПО/ВО (ч. 16 ст. 76 ФЗ-273). До этого момента диплом хранится у Исполнителя."
                }
            };
        }

        private static List<TimeOption> GetPPTimeOptionsStatic()
        {
            // Опции для учебной нагрузки (1.5) для типа договора ПП
            return new List<TimeOption>
            {
                new TimeOption
                {
                    Id = 1,
                    Name = "Опция № 1: 3 часа/нед, 81 неделя",
                    OptionKey = "Option_Time1",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 81 неделя."
                },
                new TimeOption
                {
                    Id = 2,
                    Name = "Опция № 2: 6 часов/нед, 41 неделя",
                    OptionKey = "Option_Time2",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 41 неделя."
                },
                new TimeOption
                {
                    Id = 3,
                    Name = "Опция № 3: 12 часов/нед, 21 неделя",
                    OptionKey = "Option_Time3",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 21 неделя."
                },
                new TimeOption
                {
                    Id = 4,
                    Name = "Опция № 4: 15 часов/нед, 17 недель",
                    OptionKey = "Option_Time4",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 17 недель."
                },
                new TimeOption
                {
                    Id = 5,
                    Name = "Опция № 5: 30 часов/нед, 9 недель",
                    OptionKey = "Option_Time5",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 9 недель."
                },
                new TimeOption
                {
                    Id = 6,
                    Name = "Опция № 6: 32 часа/нед, 8 недель",
                    OptionKey = "Option_Time6",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 8 недель."
                }
            };
        }

        private void CreateContractDocument(Contract contract, AppDbContext db)
        {
            var contractType = db.ContractTypes.Find(contract.ContractTypeId);
            if (contractType == null || string.IsNullOrWhiteSpace(contractType.FilePath) || !File.Exists(contractType.FilePath))
            {
                throw new FileNotFoundException("Шаблон договора не найден!");
            }

            // Загружаем заказчика и слушателя с явной загрузкой ContactsId
            var payer = db.Persons.FirstOrDefault(p => p.Id == contract.PayerId);
            var listener = db.Persons.FirstOrDefault(p => p.Id == contract.ListenerId);
            
            if (payer == null)
            {
                throw new InvalidOperationException("Заказчик не найден");
            }
            
            if (listener == null)
            {
                throw new InvalidOperationException("Слушатель не найден");
            }
            
            var program = db.LearningPrograms.Find(contract.ProgramId);
            if (program == null)
            {
                throw new InvalidOperationException("Программа не найдена");
            }

            // Загружаем контакты заказчика напрямую из таблицы contacts
            // ВАЖНО: Загружаем контакты по ContactsId из таблицы person
            Models.Contacts payerContacts = null;
            if (payer.ContactsId.HasValue)
            {
                // Загружаем контакты из БД по ID - используем прямой запрос к таблице contacts
                payerContacts = db.Contacts.Where(c => c.Id == payer.ContactsId.Value).FirstOrDefault();
                // Если не нашли, пробуем через Find
                if (payerContacts == null)
                {
                    payerContacts = db.Contacts.Find(payer.ContactsId.Value);
                }
            }
            
            // Загружаем контакты слушателя напрямую из таблицы contacts
            // ВАЖНО: Загружаем контакты по ContactsId из таблицы person
            Models.Contacts listenerContacts = null;
            if (listener.ContactsId.HasValue)
            {
                // Загружаем контакты из БД по ID - используем прямой запрос к таблице contacts
                listenerContacts = db.Contacts.Where(c => c.Id == listener.ContactsId.Value).FirstOrDefault();
                // Если не нашли, пробуем через Find
                if (listenerContacts == null)
                {
                    listenerContacts = db.Contacts.Find(listener.ContactsId.Value);
                }
            }

            // Загружаем паспортные данные
            var payerPassport = db.Passports.FirstOrDefault(p => p.PersonId == payer.Id);
            var listenerPassport = db.Passports.FirstOrDefault(p => p.PersonId == listener.Id);

            // Загружаем данные организации (если заказчик - юридическое лицо)
            var payerOrganization = db.Organizations.FirstOrDefault(o => o.Id == contract.PayerId);

            // Загружаем образование слушателя для плейсхолдера {{DIplom_Sculatel}}
            var listenerEducation = db.Educations
                .Include(e => e.BaseEducation)
                .FirstOrDefault(e => e.PersonId == listener.Id);

            // Формируем словарь замен
            var replacements = new Dictionary<string, string>();

            // Данные заказчика - берем телефон и email из таблицы contacts (такая же логика как для телефона)
            string payerPhone = payerContacts?.ContactPhone ?? "";
            string payerEmail = payerContacts?.Email ?? "";
            
            replacements["{{FIO_Zakazchik}}"] = $"{payer.LastName} {payer.FirstName} {payer.Patronymic}".Trim();
            replacements["{{lastname_zakaz}}"] = payer.LastName;
            // Инициалы заказчика отдельно
            replacements["{{Name_zakaz.}}"] = GetInitialLetter(payer.FirstName);
            replacements["{{Firstname_zakaz. }}"] = GetInitialLetter(payer.Patronymic);
            replacements["{{Firstname_zakaz.}}"] = GetInitialLetter(payer.Patronymic);
            // Фамилия с инициалами заказчика (например, "Халиков Р.Н.")
            string payerInitials = GetInitials(payer.FirstName, payer.Patronymic);
            replacements["{{lastname_zakaz Name_zakaz. Firstname_zakaz. }}"] = $"{payer.LastName} {payerInitials}".Trim();
            replacements["{{lastname_zakaz Name_zakaz. Firstname_zakaz.}}"] = $"{payer.LastName} {payerInitials}".Trim();
            replacements["{{contact_phone_zakazchik}}"] = payerPhone;
            // Также обрабатываем вариант с пропущенной закрывающей скобкой (опечатка в исходном файле)
            replacements["{{contact_phone_zakazchik}"] = payerPhone;
            // Добавляем email заказчика в словарь замен - ТОЧНО ТАК ЖЕ КАК ТЕЛЕФОН
            replacements["{{EMAIL_zakazchik}}"] = payerEmail;
            
            // Также обрабатываем вариант с пропущенной закрывающей скобкой для email
            replacements["{{EMAIL_zakazchik}"] = payerEmail;
            
            // Дополнительные варианты написания плейсхолдеров для email
            replacements["{{email_zakazchik}}"] = payerEmail;
            replacements["{{Email_zakazchik}}"] = payerEmail;
            
            if (payerPassport != null)
            {
                replacements["{{Seria Number, Kem_Vidan}}"] = $"{payerPassport.Series} {payerPassport.Number}, выдан {payerPassport.IssuedBy}";
            }
            else
            {
                replacements["{{Seria Number, Kem_Vidan}}"] = "";
            }
            replacements["{{Adress_Registracii}}"] = payerPassport?.RegistrationAddress ?? "";
            replacements["{{Snils}}"] = payer.Snils ?? "";

            // Данные организации заказчика (если заказчик - юридическое лицо)
            if (payerOrganization != null)
            {
                replacements["{{Organisation_Name}}"] = payerOrganization.OrganizationName ?? "";
                string shortOrgName = ExtractShortOrganizationName(payerOrganization.OrganizationName);
                replacements["{{Organisation_Zakachik}}"] = $"ООО \"{shortOrgName}\"";
                replacements["{{Name_Direktororganisation}}"] = payerOrganization.DirectorFio ?? "";
                
                // Разбираем ФИО директора на фамилию и инициалы
                string directorFio = payerOrganization.DirectorFio ?? "";
                string[] directorParts = directorFio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (directorParts.Length >= 1)
                {
                    replacements["{{lastname_zakaz_org }}"] = directorParts[0];
                    replacements["{{lastname_zakaz_org}}"] = directorParts[0];
                }
                else
                {
                    replacements["{{lastname_zakaz_org }}"] = "";
                    replacements["{{lastname_zakaz_org}}"] = "";
                }
                
                if (directorParts.Length >= 2)
                {
                    replacements["{{Name_zakaz_org.}}"] = GetInitialLetter(directorParts[1]);
                }
                else
                {
                    replacements["{{Name_zakaz_org.}}"] = "";
                }
                
                if (directorParts.Length >= 3)
                {
                    replacements["{{Firstname_zakaz_org. }}"] = GetInitialLetter(directorParts[2]);
                    replacements["{{Firstname_zakaz_org.}}"] = GetInitialLetter(directorParts[2]);
                }
                else
                {
                    replacements["{{Firstname_zakaz_org. }}"] = "";
                    replacements["{{Firstname_zakaz_org.}}"] = "";
                }
                
                replacements["{{INN_Zakachik}}"] = payerOrganization.Inn ?? "";
                replacements["{{KPP_Zakachik}}"] = payerOrganization.Kpp ?? "";
                replacements["{{OGRN_Zakachik}}"] = payerOrganization.Ogrn ?? "";
                replacements["{{Addres_Zakachik}}"] = payerOrganization.LegalAddress ?? "";
                
                // Переопределяем телефон и email для организации
                if (!string.IsNullOrWhiteSpace(payerOrganization.Phone))
                {
                    replacements["{{contact_phone_zakazchik_org}}"] = payerOrganization.Phone;
                }
                if (!string.IsNullOrWhiteSpace(payerOrganization.Email))
                {
                    replacements["{{EMAIL_zakazchik_org}}"] = payerOrganization.Email;
                }
            }
            else
            {
                // Если заказчик - физическое лицо, заполняем пустыми значениями
                replacements["{{Organisation_Name}}"] = "";
                replacements["{{Organisation_Zakachik}}"] = "ООО";
                replacements["{{Name_Direktororganisation}}"] = "";
                replacements["{{lastname_zakaz_org }}"] = "";
                replacements["{{lastname_zakaz_org}}"] = "";
                replacements["{{Name_zakaz_org.}}"] = "";
                replacements["{{Firstname_zakaz_org. }}"] = "";
                replacements["{{Firstname_zakaz_org.}}"] = "";
                replacements["{{INN_Zakachik}}"] = "";
                replacements["{{KPP_Zakachik}}"] = "";
                replacements["{{OGRN_Zakachik}}"] = "";
                replacements["{{Addres_Zakachik}}"] = "";
            }

            // Данные слушателя - берем телефон и email из таблицы contacts (такая же логика как для телефона)
            string listenerPhone = listenerContacts?.ContactPhone ?? "";
            string listenerEmail = listenerContacts?.Email ?? "";
            
            replacements["{{FIO_Slushtel}}"] = $"{listener.LastName} {listener.FirstName} {listener.Patronymic}".Trim();
            replacements["{{FIO_Slushatel}}"] = $"{listener.LastName} {listener.FirstName} {listener.Patronymic}".Trim();
            replacements["{{lastname}}"] = listener.LastName;
            // Инициалы слушателя отдельно
            replacements["{{Name.}}"] = GetInitialLetter(listener.FirstName);
            replacements["{{Firstname.}}"] = GetInitialLetter(listener.Patronymic);
            replacements["{{Firstname. }}"] = GetInitialLetter(listener.Patronymic);
            // Фамилия с инициалами слушателя (например, "Халиков Р.Н.")
            string listenerInitials = GetInitials(listener.FirstName, listener.Patronymic);
            replacements["{{lastname Name. Firstname.}}"] = $"{listener.LastName} {listenerInitials}".Trim();
            replacements["{{lastname Name. Firstname. }}"] = $"{listener.LastName} {listenerInitials}".Trim();
            replacements["{{birthday}}"] = listener.DateOfBirth.ToString("dd.MM.yyyy");
            replacements["{{contact_phone}}"] = listenerPhone;
            // Добавляем email слушателя в словарь замен - ТОЧНО ТАК ЖЕ КАК ТЕЛЕФОН
            replacements["{{EMAIL}}"] = listenerEmail;
            
            // Также обрабатываем вариант с пропущенной закрывающей скобкой для email
            replacements["{{EMAIL}"] = listenerEmail;
            
            // Дополнительные варианты написания плейсхолдеров для email
            replacements["{{email}}"] = listenerEmail;
            replacements["{{Email}}"] = listenerEmail;
            
            // СНИЛС слушателя - все варианты написания метки
            replacements["{{Snils_Slushatel}}"] = listener.Snils ?? "";
            replacements["{{SNILS_Slushatel}}"] = listener.Snils ?? "";
            replacements["{{snils_slushatel}}"] = listener.Snils ?? "";

            // Диплом слушателя
            if (listenerEducation != null && listenerEducation.BaseEducation != null)
            {
                replacements["{{DIplom_Sculatel}}"] = listenerEducation.BaseEducation.Name ?? "";
            }
            else
            {
                replacements["{{DIplom_Sculatel}}"] = "";
            }

            // Данные программы
            replacements["{{Program_name}}"] = program.Name;
            replacements["{{Date Start}}"] = contract.StartDate?.ToString("dd.MM.yyyy") ?? "";
            replacements["{{Date End}}"] = contract.EndDate?.ToString("dd.MM.yyyy") ?? "";
            
            // Дата договора - используем дату из договора
            DateTime contractDate = contract.ContractDate;
            
            replacements["{{ContractDate}}"] = contractDate.ToString("dd.MM.yyyy");
            replacements["{{Contract_Date}}"] = contractDate.ToString("dd.MM.yyyy");
            
            // Плейсхолдеры для даты: {{nm}} - день, {{mounts}} - месяц, {{yr}} - год
            replacements["{{nm}}"] = contractDate.Day.ToString("D2");
            replacements["{{mounts}}"] = GetMonthName(contractDate.Month);
            replacements["{{yr}}"] = contractDate.Year.ToString();
            
            // Также добавляем номер договора
            replacements["{{number_dogovor}}"] = contract.ContractNumber;
            
            // Номер акта - автоинкремент начиная с 1
            // Считаем количество договоров, созданных до текущего (включая текущий)
            int aktNumber = db.Contracts
                .Where(c => c.CreatedAt <= contract.CreatedAt || (c.CreatedAt == null && c.Id <= contract.Id))
                .Count();
            replacements["{{number_akt}}"] = aktNumber.ToString();

            // Подписант - берем из сохраненного в БД
            if (contract.SignerId.HasValue)
            {
                var signers = GetSignersStatic();
                var selectedSigner = signers.FirstOrDefault(s => s.Id == contract.SignerId.Value);
                if (selectedSigner != null)
                {
                    replacements["{{choice_signer}}"] = selectedSigner.Text;
                }
                else
                {
                    replacements["{{choice_signer}}"] = "";
                }
            }
            else
            {
                replacements["{{choice_signer}}"] = "";
            }

            // Вариант оплаты - берем из сохраненного в БД
            if (!string.IsNullOrEmpty(contract.PaymentOptionKey))
            {
                if (contract.PaymentOptionKey == "option1")
                {
                    // Полная оплата
                    replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
                    replacements["{{option2}}"] = "";
                }
                else if (contract.PaymentOptionKey == "option2")
                {
                    // Оплата частями - используем плейсхолдеры для Price и Date last price
                    replacements["{{option1}}"] = "";
                    replacements["{{option2}}"] = "Оплата осуществляется в следующем порядке: а) Аванс 50 % — {{Price}} рублей, предоплата до начала обучения.\n" +
                                                  "б) Оставшиеся 50 % — {{Price}} рублей. Срок: не позднее {{Date last price}} г.\n" +
                                                  "Просрочка второго платежа более 30 календарных дней даёт Исполнителю право приостановить доступ к LMS и/или расторгнуть договор с удержанием фактических расходов.";
                }
            }
            else
            {
                // По умолчанию - полная оплата
                replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
                replacements["{{option2}}"] = "";
            }

            // Проверяем, является ли тип договора "ПК" (повышение квалификации)
            bool isPK = contractType.Name != null && 
                       (contractType.Name.Contains("ПК") || 
                        contractType.Name.Contains("повышение квалификации") ||
                        contractType.Name.Contains("повышения квалификации"));

            // Проверяем, является ли тип договора "ПП" (профессиональная переподготовка)
            bool isPP = contractType.Name != null && 
                       (contractType.Name.Contains("ПП") || 
                        contractType.Name.Contains("профпереподготовк") ||
                        contractType.Name.Contains("профессиональной переподготовки") ||
                        contractType.Name.Contains("профессиональная переподготовка"));

            if (isPK || isPP)
            {
                // Для типа договора ПК или ПП используем опции 1.4 и 1.5
                
                // Опция итогового документа (1.4) - берем из сохраненного в БД
                if (!string.IsNullOrEmpty(contract.ItogDocumentOptionKey))
                {
                    List<ItogDocumentOption> itogOptions = isPK ? GetItogDocumentOptions() : GetPPItogDocumentOptions();
                    var selectedItogOption = itogOptions.FirstOrDefault(o => o.OptionKey == contract.ItogDocumentOptionKey);
                    if (selectedItogOption != null)
                    {
                        replacements[$"{{{{{selectedItogOption.OptionKey}}}}}"] = selectedItogOption.Text;
                        // Остальные опции итогового документа - пустые
                        for (int i = 1; i <= 2; i++)
                        {
                            string optionKey = $"Option_Itog{i}";
                            if (optionKey != selectedItogOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                    else
                    {
                        // Если опция не найдена, очищаем все
                        replacements["{{Option_Itog1}}"] = "";
                        replacements["{{Option_Itog2}}"] = "";
                    }
                }
                else
                {
                    // По умолчанию - первая опция
                    if (isPK)
                    {
                        replacements["{{Option_Itog1}}"] = GetItogDocumentOptions().FirstOrDefault()?.Text ?? "";
                    }
                    else if (isPP)
                    {
                        replacements["{{Option_Itog1}}"] = GetPPItogDocumentOptions().FirstOrDefault()?.Text ?? "";
                    }
                    replacements["{{Option_Itog2}}"] = "";
                }

                // Опция учебной нагрузки (1.5) - берем из сохраненного в БД
                if (!string.IsNullOrEmpty(contract.TimeOptionKey))
                {
                    List<TimeOption> timeOptions = isPK ? GetTimeOptions() : GetPPTimeOptions();
                    var selectedTimeOption = timeOptions.FirstOrDefault(o => o.OptionKey == contract.TimeOptionKey);
                    if (selectedTimeOption != null)
                    {
                        replacements[$"{{{{{selectedTimeOption.OptionKey}}}}}"] = selectedTimeOption.Text;
                        // Все остальные опции времени заменяем на пустую строку
                        for (int i = 1; i <= 6; i++)
                        {
                            string optionKey = $"Option_Time{i}";
                            if (optionKey != selectedTimeOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                    else
                    {
                        // Если опция не найдена, очищаем все
                        for (int i = 1; i <= 6; i++)
                        {
                            replacements[$"{{{{Option_Time{i}}}}}"] = "";
                        }
                    }
                }
                else
                {
                    // По умолчанию - первая опция
                    TimeOption defaultTimeOption = null;
                    if (isPK)
                    {
                        defaultTimeOption = GetTimeOptions().FirstOrDefault();
                    }
                    else if (isPP)
                    {
                        defaultTimeOption = GetPPTimeOptions().FirstOrDefault();
                    }
                    
                    if (defaultTimeOption != null)
                    {
                        replacements[$"{{{{{defaultTimeOption.OptionKey}}}}}"] = defaultTimeOption.Text;
                        for (int i = 1; i <= 6; i++)
                        {
                            string optionKey = $"Option_Time{i}";
                            if (optionKey != defaultTimeOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                }

                // Скрываем старые опции учебной нагрузки
                for (int i = 1; i <= 5; i++)
                {
                    replacements[$"{{{{Option_study{i}}}}}"] = "";
                }
            }
            else
            {
                // Для других типов договоров используем старые опции учебной нагрузки
                
                // Скрываем опции ПК
                replacements["{{Option_Itog1}}"] = "";
                replacements["{{Option_Itog2}}"] = "";
                for (int i = 1; i <= 6; i++)
                {
                    replacements[$"{{{{Option_Time{i}}}}}"] = "";
                }

                // Вариант учебной нагрузки - берем из сохраненного в БД
                if (!string.IsNullOrEmpty(contract.StudyOptionKey))
                {
                    var studyOptions = GetStudyOptions();
                    var selectedStudyOption = studyOptions.FirstOrDefault(o => o.OptionKey == contract.StudyOptionKey);
                    if (selectedStudyOption != null)
                    {
                        // Заменяем выбранную опцию на текст, остальные на пустую строку
                        replacements[$"{{{{{selectedStudyOption.OptionKey}}}}}"] = selectedStudyOption.Text;
                        
                        // Все остальные опции заменяем на пустую строку
                        for (int i = 1; i <= 5; i++)
                        {
                            string optionKey = $"Option_study{i}";
                            if (optionKey != selectedStudyOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                    else
                    {
                        // Если опция не найдена, очищаем все
                        for (int i = 1; i <= 5; i++)
                        {
                            replacements[$"{{{{Option_study{i}}}}}"] = "";
                        }
                    }
                }
                else
                {
                    // По умолчанию - первая опция
                    var defaultStudyOption = GetStudyOptions().FirstOrDefault();
                    if (defaultStudyOption != null)
                    {
                        replacements[$"{{{{{defaultStudyOption.OptionKey}}}}}"] = defaultStudyOption.Text;
                        for (int i = 1; i <= 5; i++)
                        {
                            string optionKey = $"Option_study{i}";
                            if (optionKey != defaultStudyOption.OptionKey)
                            {
                                replacements[$"{{{{{optionKey}}}}}"] = "";
                            }
                        }
                    }
                }
            }

            // Стоимость - всегда берем из таблицы learning_program
            replacements["{{Price full}}"] = program.Price.ToString("N2", CultureInfo.InvariantCulture);
            replacements["{{Price}}"] = (program.Price / 2).ToString("N2", CultureInfo.InvariantCulture);

            // Дата последнего платежа (если есть дата окончания)
            if (contract.EndDate.HasValue)
            {
                replacements["{{Date last price}}"] = contract.EndDate.Value.AddDays(30).ToString("dd.MM.yyyy");
            }
            else
            {
                replacements["{{Date last price}}"] = "";
            }

            // Часы программы
            replacements["{{Program_Time}}"] = program.Hours.ToString();

            // Формат программы (форма обучения)
            string formatProgram = "";
            if (!string.IsNullOrEmpty(program.Format))
            {
                if (program.Format.Contains("Индивидуально с преподавателем"))
                {
                    formatProgram = "заочная форма, с применением дистанционных образовательных технологий";
                }
                else if (program.Format.Contains("С преподавателем в группе"))
                {
                    formatProgram = "очная форма";
                }
                else
                {
                    // Если формат не распознан, используем исходное значение
                    formatProgram = program.Format;
                }
            }
            replacements["{{Format_Program}}"] = formatProgram;

            // Создаем путь для сохранения договора
            string outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Договоры");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string outputFileName = $"Договор_{contract.ContractNumber}_{listener.LastName}_{DateTime.Now:yyyyMMdd}.docx";
            string outputPath = Path.Combine(outputDirectory, outputFileName);

            // Заменяем плейсхолдеры
            var wordService = new WordDocumentService();
            wordService.ReplacePlaceholders(contractType.FilePath, outputPath, replacements);

            // Создаем PDF версию договора
            try
            {
                string pdfFileName = Path.ChangeExtension(outputFileName, ".pdf");
                string pdfPath = Path.Combine(outputDirectory, pdfFileName);
                wordService.ConvertToPdf(outputPath, pdfPath);
            }
            catch (Exception)
            {
                // Если не удалось создать PDF (например, Word не установлен), просто пропускаем
                // Договор в формате Word уже создан, это главное
            }

            // Открываем созданный документ
            wordService.OpenDocument(outputPath);
        }

        private string GetMonthName(int month)
        {
            string[] months = { "", "января", "февраля", "марта", "апреля", "мая", "июня",
                              "июля", "августа", "сентября", "октября", "ноября", "декабря" };
            if (month >= 1 && month <= 12)
                return months[month];
            return month.ToString();
        }

        private string GetInitials(string firstName, string patronymic)
        {
            // Формируем инициалы: первая буква имени + точка, первая буква отчества + точка
            // Например: "Р.Н." для имени "Роман" и отчества "Николаевич"
            string initials = "";
            
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                initials += firstName.Trim()[0].ToString().ToUpper() + ".";
            }
            
            if (!string.IsNullOrWhiteSpace(patronymic))
            {
                initials += patronymic.Trim()[0].ToString().ToUpper() + ".";
            }
            
            return initials;
        }

        private string GetInitialLetter(string name)
        {
            // Возвращает первую заглавную букву с точкой (например, "Р." для "Роман")
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }
            
            return name.Trim()[0].ToString().ToUpper() + ".";
        }

        private List<Signer> GetSigners()
        {
            // Подписанты хранятся в коде, не в БД
            return new List<Signer>
            {
                new Signer
                {
                    Id = 1,
                    Name = "Шимбирева Елена",
                    Text = "в лице Генерального директора Шимбиревой Елены Анатольевны, действующей на основании Устава, с одной стороны"
                },
                new Signer
                {
                    Id = 2,
                    Name = "Шимбирев Андрей",
                    Text = "в лице Исполнительного директора Шимбирёва Андрея Андреевича, действующей на основании Доверенности №Д/01 от 01 сентября 2025 г., с одной стороны"
                },
                new Signer
                {
                    Id = 3,
                    Name = "Бойцова Екатерина",
                    Text = "в лице Руководителя направления дополнительного образования Бойцовой Екатерины Юрьевны, действующей на основании Доверенности №Д/02 от 01 октября 2025 г., с одной стороны"
                }
            };
        }

        private List<PaymentOption> GetPaymentOptions()
        {
            // Варианты оплаты хранятся в коде, не в БД
            return new List<PaymentOption>
            {
                new PaymentOption
                {
                    Id = 1,
                    Name = "Полная оплата",
                    OptionKey = "option1"
                },
                new PaymentOption
                {
                    Id = 2,
                    Name = "Оплата частями",
                    OptionKey = "option2"
                }
            };
        }

        private List<StudyOption> GetStudyOptions()
        {
            // Варианты учебной нагрузки хранятся в коде, не в БД
            return new List<StudyOption>
            {
                new StudyOption
                {
                    Id = 1,
                    Name = "Опция № 1: 1 час/нед, 20 недель",
                    OptionKey = "Option_study1",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 1 академический час в неделю; общая продолжительность освоения — 20 недель."
                },
                new StudyOption
                {
                    Id = 2,
                    Name = "Опция № 2: 2 часа/нед, 10 недель",
                    OptionKey = "Option_study2",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 2 академических часа в неделю; общая продолжительность освоения — 10 недель."
                },
                new StudyOption
                {
                    Id = 3,
                    Name = "Опция № 3: 4 часа/нед, 5 недель",
                    OptionKey = "Option_study3",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 4 академических часа в неделю; общая продолжительность освоения — 5 недель."
                },
                new StudyOption
                {
                    Id = 4,
                    Name = "Опция № 4: 8 часов/нед, 2,5 недели",
                    OptionKey = "Option_study4",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 8 академических часов в неделю; общая продолжительность освоения — 2,5 недели."
                },
                new StudyOption
                {
                    Id = 5,
                    Name = "Опция № 5: 10 часов/нед, 2 недели",
                    OptionKey = "Option_study5",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 10 академических часов в неделю; общая продолжительность освоения — 2 недели."
                }
            };
        }

        private List<ItogDocumentOption> GetItogDocumentOptions()
        {
            // Опции для итогового документа (1.4) для типа договора ПК
            return new List<ItogDocumentOption>
            {
                new ItogDocumentOption
                {
                    Id = 1,
                    Name = "Опция № 1: Удостоверение вручается по окончании",
                    OptionKey = "Option_Itog1",
                    Text = "удостоверение о повышении квалификации вручается по окончании;"
                },
                new ItogDocumentOption
                {
                    Id = 2,
                    Name = "Опция № 2: Удостоверение выдаётся одновременно с дипломом",
                    OptionKey = "Option_Itog2",
                    Text = "удостоверение выдаётся одновременно с дипломом СПО/ВО (ч. 16 ст. 76 ФЗ-273). До этого момента удостоверение хранится у Исполнителя."
                }
            };
        }

        private List<TimeOption> GetTimeOptions()
        {
            // Опции для учебной нагрузки (1.5) для типа договора ПК
            return new List<TimeOption>
            {
                new TimeOption
                {
                    Id = 1,
                    Name = "Опция № 1: 3 часа/нед, 21 неделя",
                    OptionKey = "Option_Time1",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 21 неделя."
                },
                new TimeOption
                {
                    Id = 2,
                    Name = "Опция № 2: 6 часов/нед, 11 недель",
                    OptionKey = "Option_Time2",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 11 недель."
                },
                new TimeOption
                {
                    Id = 3,
                    Name = "Опция № 3: 12 часов/нед, 6 недель",
                    OptionKey = "Option_Time3",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 6 недель."
                },
                new TimeOption
                {
                    Id = 4,
                    Name = "Опция № 4: 15 часов/нед, 5 недель",
                    OptionKey = "Option_Time4",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 5 недель."
                },
                new TimeOption
                {
                    Id = 5,
                    Name = "Опция № 5: 30 часов/нед, 3 недели",
                    OptionKey = "Option_Time5",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели."
                },
                new TimeOption
                {
                    Id = 6,
                    Name = "Опция № 6: 32 часа/нед, 3 недели",
                    OptionKey = "Option_Time6",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели."
                }
            };
        }

        private List<ItogDocumentOption> GetPPItogDocumentOptions()
        {
            // Опции для итогового документа (1.4) для типа договора ПП
            return new List<ItogDocumentOption>
            {
                new ItogDocumentOption
                {
                    Id = 1,
                    Name = "Опция № 1: Диплом вручается по окончании",
                    OptionKey = "Option_Itog1",
                    Text = "диплом о профпереподготовке вручается по окончании;"
                },
                new ItogDocumentOption
                {
                    Id = 2,
                    Name = "Опция № 2: Диплом выдаётся одновременно с дипломом",
                    OptionKey = "Option_Itog2",
                    Text = "диплом выдаётся одновременно с дипломом СПО/ВО (ч. 16 ст. 76 ФЗ-273). До этого момента диплом хранится у Исполнителя."
                }
            };
        }

        private List<TimeOption> GetPPTimeOptions()
        {
            // Опции для учебной нагрузки (1.5) для типа договора ПП
            return new List<TimeOption>
            {
                new TimeOption
                {
                    Id = 1,
                    Name = "Опция № 1: 3 часа/нед, 81 неделя",
                    OptionKey = "Option_Time1",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 81 неделя."
                },
                new TimeOption
                {
                    Id = 2,
                    Name = "Опция № 2: 6 часов/нед, 41 неделя",
                    OptionKey = "Option_Time2",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 41 неделя."
                },
                new TimeOption
                {
                    Id = 3,
                    Name = "Опция № 3: 12 часов/нед, 21 неделя",
                    OptionKey = "Option_Time3",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 21 неделя."
                },
                new TimeOption
                {
                    Id = 4,
                    Name = "Опция № 4: 15 часов/нед, 17 недель",
                    OptionKey = "Option_Time4",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 17 недель."
                },
                new TimeOption
                {
                    Id = 5,
                    Name = "Опция № 5: 30 часов/нед, 9 недель",
                    OptionKey = "Option_Time5",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 9 недель."
                },
                new TimeOption
                {
                    Id = 6,
                    Name = "Опция № 6: 32 часа/нед, 8 недель",
                    OptionKey = "Option_Time6",
                    Text = "Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 8 недель."
                }
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CreateListenerCard(Contract contract, AppDbContext db)
        {
            string templatePath = @"C:\Dogovora\Личная карточка.docx";
            
            if (!File.Exists(templatePath))
            {
                MessageBox.Show("Шаблон личной карточки не найден по пути: " + templatePath, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Загружаем данные слушателя с включением Gender
            var listener = db.Persons
                .Include(p => p.Gender)
                .FirstOrDefault(p => p.Id == contract.ListenerId);
            if (listener == null)
            {
                MessageBox.Show("Слушатель не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Если Gender не загружен через Include, загружаем отдельно
            if (listener.Gender == null && listener.GenderId > 0)
            {
                listener.Gender = db.Genders.Find(listener.GenderId);
            }

            var program = db.LearningPrograms.Find(contract.ProgramId);
            if (program == null)
            {
                MessageBox.Show("Программа не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var programView = db.ProgramViews.Find(program.ProgramViewId);
            
            // Загружаем контакты слушателя
            Models.Contacts listenerContacts = null;
            if (listener.ContactsId.HasValue)
            {
                listenerContacts = db.Contacts.Find(listener.ContactsId.Value);
            }

            // Загружаем паспорт слушателя
            var listenerPassport = db.Passports.FirstOrDefault(p => p.PersonId == listener.Id);

            // Загружаем образование слушателя
            var education = db.Educations.FirstOrDefault(e => e.PersonId == listener.Id);
            var baseEducation = education?.BaseEducationId.HasValue == true ? db.BaseEducations.Find(education.BaseEducationId.Value) : null;

            // Формируем словарь замен (используем точные плейсхолдеры из шаблона)
            var replacements = new Dictionary<string, string>();

            // Основная информация
            replacements["{{number_dogovor}}"] = contract.ContractNumber ?? "";
            replacements["{{Program_name}}"] = program.Name ?? "";
            replacements["{{Type_program}}"] = programView?.Name ?? "";
            replacements["{{time_program}}"] = $"{program.Hours} часов";
            
            // ФИО и личные данные (обратите внимание на пробелы в плейсхолдерах!)
            replacements["{{FIO_Slushtel}}"] = $"{listener.LastName} {listener.FirstName} {listener.Patronymic}".Trim();
            replacements["{{Brithday_Slushtel}}"] = listener.DateOfBirth.ToString("dd.MM.yyyy");
            replacements["{{Gorod_ Slushtel}}"] = listener.PlaceOfBirth ?? ""; // С пробелом!
            replacements["{{Graschadnsto_Slushtel}}"] = listener.Citizenship ?? "";
            replacements["{{Gender_ Slushtel}}"] = listener.Gender?.Name ?? ""; // С пробелом!

            // Паспортные данные
            if (listenerPassport != null)
            {
                replacements["{{Seria}}"] = listenerPassport.Series ?? "";
                replacements["{{Number}}"] = listenerPassport.Number ?? "";
                replacements["{{Kem_Vidan}}"] = listenerPassport.IssuedBy ?? "";
                replacements["{{Kogda_Vidan}}"] = listenerPassport.IssuanceDate.ToString("dd.MM.yyyy");
            }
            else
            {
                replacements["{{Seria}}"] = "";
                replacements["{{Number}}"] = "";
                replacements["{{Kem_Vidan}}"] = "";
                replacements["{{Kogda_Vidan}}"] = "";
            }

            // Адрес
            if (listenerContacts != null)
            {
                replacements["{{Index_Slushtel}}"] = listenerContacts.PostalCode ?? "";
                replacements["{{Oblast_Slushtel}}"] = listenerContacts.Region ?? "";
                replacements["{{Gorod_Slushtel}}"] = listenerContacts.City ?? "";
                replacements["{{Street_Slushtel}}"] = listenerContacts.ResidenceAddress ?? "";
                replacements["{{Email_Slushtel}}"] = listenerContacts.Email ?? "";
            }
            else
            {
                replacements["{{Index_Slushtel}}"] = "";
                replacements["{{Oblast_Slushtel}}"] = "";
                replacements["{{Gorod_Slushtel}}"] = "";
                replacements["{{Street_Slushtel}}"] = "";
                replacements["{{Email_Slushtel}}"] = "";
            }

            // СНИЛС и Телефон
            replacements["{{Snils_Slushtel}}"] = listener.Snils ?? "";
            replacements["{{Phone_Slushtel}}"] = listenerContacts?.ContactPhone ?? "";
            replacements["{{Telefon_Slushtel}}"] = listenerContacts?.ContactPhone ?? "";
            
            // Образование
            replacements["{{Obrasovanie_Slushtel}}"] = baseEducation?.Name ?? "";

            // Данные об образовании (диплом)
            if (education != null)
            {
                replacements["{{Seria_Diplom}}"] = education.Series ?? "";
                replacements["{{Nomer_Diplom}}"] = education.Number ?? "";
                replacements["{{Date_Diplom}}"] = education.IssueDate?.ToString("dd.MM.yyyy") ?? "";
                replacements["{{Sity_Diplom}}"] = education.City ?? "";
                replacements["{{Naimenovanie_Diplom}}"] = education.IssuedBy ?? "";
                replacements["{{Spesialnost}}"] = education.Specialty ?? "";
            }
            else
            {
                replacements["{{Seria_Diplom}}"] = "";
                replacements["{{Nomer_Diplom}}"] = "";
                replacements["{{Date_Diplom}}"] = "";
                replacements["{{Sity_Diplom}}"] = "";
                replacements["{{Naimenovanie_Diplom}}"] = "";
                replacements["{{Spesialnost}}"] = "";
            }

            // Место работы
            replacements["{{Mesto_Raboti}}"] = listener.Workplace ?? "";

            // Создаем документ
            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Личные карточки");
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string outputFileName = $"Личная карточка_{contract.ContractNumber}_{listener.LastName}.docx";
            string outputPath = Path.Combine(outputFolder, outputFileName);

            try
            {
                var wordService = new WordDocumentService();
                wordService.ReplacePlaceholders(templatePath, outputPath, replacements);
                
                // Создаем PDF версию личной карточки
                try
                {
                    string pdfFileName = Path.ChangeExtension(outputFileName, ".pdf");
                    string pdfPath = Path.Combine(outputFolder, pdfFileName);
                    wordService.ConvertToPdf(outputPath, pdfPath);
                }
                catch (Exception)
                {
                    // Если не удалось создать PDF, просто игнорируем ошибку
                }
                
                // Не показываем отдельное сообщение, так как оно будет показано после создания договора
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании личной карточки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Методы фильтрации для ComboBox с поиском
        private void ContractTypeComboBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_allContractTypes == null) return;
            
            var searchText = ContractTypeComboBox.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ContractTypeComboBox.ItemsSource = _allContractTypes;
                return;
            }
            

            var filtered = _allContractTypes.Where(ct => 
                ct.Name != null && ct.Name.ToLower().Contains(searchText)
            ).ToList();

            ContractTypeComboBox.ItemsSource = filtered;
            ContractTypeComboBox.IsDropDownOpen = true;
        }

        private void ProgramComboBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_allPrograms == null) return;
            
            var searchText = ProgramComboBox.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ProgramComboBox.ItemsSource = _allPrograms;
                return;
            }

            var filtered = _allPrograms.Where(p => 
                p.Name != null && p.Name.ToLower().Contains(searchText)
            ).ToList();

            ProgramComboBox.ItemsSource = filtered;
            ProgramComboBox.IsDropDownOpen = true;
        }

        private void PayerComboBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_allPersons == null) return;
            
            var searchText = PayerComboBox.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(searchText))
            {
                PayerComboBox.ItemsSource = _allPersons;
                return;
            }

            var filtered = _allPersons.Where(p => 
                (p.FullName != null && p.FullName.ToLower().Contains(searchText)) ||
                (p.Snils != null && p.Snils.ToLower().Contains(searchText))
            ).ToList();

            PayerComboBox.ItemsSource = filtered;
            PayerComboBox.IsDropDownOpen = true;
        }

        private void ListenerComboBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_allPersons == null) return;
            
            var searchText = ListenerComboBox.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ListenerComboBox.ItemsSource = _allPersons;
                return;
            }

            var filtered = _allPersons.Where(p => 
                (p.FullName != null && p.FullName.ToLower().Contains(searchText)) ||
                (p.Snils != null && p.Snils.ToLower().Contains(searchText))
            ).ToList();

            ListenerComboBox.ItemsSource = filtered;
            ListenerComboBox.IsDropDownOpen = true;
        }

        // Обработчик для открытия ComboBox при клике на любую часть поля
        private void ComboBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox)
            {
                // Если ComboBox закрыт, открываем его
                if (!comboBox.IsDropDownOpen)
                {
                    comboBox.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }
        }

        // Обработчик изменения выбора заказчика
        private void PayerComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Этот обработчик нужен для того, чтобы убедиться, что выбор правильно обрабатывается
            // Когда пользователь выбирает элемент из выпадающего списка
            if (PayerComboBox.SelectedItem != null)
            {
                // Закрываем выпадающий список после выбора
                PayerComboBox.IsDropDownOpen = false;
            }
        }

        // Обработчик изменения выбора слушателя
        private void ListenerComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Этот обработчик нужен для того, чтобы убедиться, что выбор правильно обрабатывается
            // Когда пользователь выбирает элемент из выпадающего списка
            if (ListenerComboBox.SelectedItem != null)
            {
                // Закрываем выпадающий список после выбора
                ListenerComboBox.IsDropDownOpen = false;
            }
        }

    }
}

