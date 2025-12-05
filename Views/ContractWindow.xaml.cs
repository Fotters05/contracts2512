using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
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
                // Загружаем типы договоров
                ContractTypeComboBox.ItemsSource = db.ContractTypes.ToList();

                // Загружаем программы обучения
                ProgramComboBox.ItemsSource = db.LearningPrograms.ToList();

                // Загружаем физических лиц
                var persons = db.Persons.ToList();
                PayerComboBox.ItemsSource = persons;
                ListenerComboBox.ItemsSource = persons;
            }

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

            // Автоматически генерируем номер договора, если он не заполнен
            if (string.IsNullOrWhiteSpace(ContractNumberTextBox.Text) && ContractTypeComboBox.SelectedItem is ContractType contractType)
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

            if (PayerComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите заказчика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ListenerComboBox.SelectedItem == null)
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

                    // Создаем договор в БД
                    var contract = new Contract
                    {
                        ContractNumber = ContractNumberTextBox.Text,
                        ContractDate = ContractDatePicker.SelectedDate.Value,
                        ContractTypeId = ((ContractType)ContractTypeComboBox.SelectedItem).Id,
                        ProgramId = ((LearningProgram)ProgramComboBox.SelectedItem).Id,
                        StartDate = StartDatePicker.SelectedDate,
                        EndDate = EndDatePicker.SelectedDate,
                        PayerId = ((Person)PayerComboBox.SelectedItem).Id,
                        ListenerId = ((Person)ListenerComboBox.SelectedItem).Id,
                        CreatedAt = DateTime.Now
                    };

                    db.Contracts.Add(contract);
                    db.SaveChanges();

                    // Создаем Word документ
                    CreateContractDocument(contract, db);

                    MessageBox.Show("Договор успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании договора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Формируем словарь замен
            var replacements = BuildReplacementsDictionary(contract, payer, listener, program, payerContacts, listenerContacts, payerPassport, listenerPassport);

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

            return outputPath;
        }

        private static Dictionary<string, string> BuildReplacementsDictionary(Contract contract, Person payer, Person listener, LearningProgram program, 
            Models.Contacts payerContacts, Models.Contacts listenerContacts, Passport payerPassport, Passport listenerPassport)
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
            replacements["{{Adress_Registracii}}"] = payerContacts?.RegistrationAddress ?? "";

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

            // Подписант - по умолчанию первый
            var signers = GetSignersStatic();
            if (signers.Any())
            {
                replacements["{{choice_signer}}"] = signers.First().Text;
            }
            else
            {
                replacements["{{choice_signer}}"] = "";
            }

            // Вариант оплаты - по умолчанию полная оплата
            replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
            replacements["{{option2}}"] = "";

            // Вариант учебной нагрузки - по умолчанию первая опция
            var studyOptions = GetStudyOptionsStatic();
            var defaultStudyOption = studyOptions.FirstOrDefault();
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
            replacements["{{Adress_Registracii}}"] = payerContacts?.RegistrationAddress ?? "";

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

            // Данные программы
            replacements["{{Program_name}}"] = program.Name;
            replacements["{{Date Start}}"] = contract.StartDate?.ToString("dd.MM.yyyy") ?? "";
            replacements["{{Date End}}"] = contract.EndDate?.ToString("dd.MM.yyyy") ?? "";
            
            // Дата договора (если нужен плейсхолдер для даты договора)
            // Используем текущую дату (сегодняшнюю)
            DateTime currentDate = DateTime.Today;
            
            replacements["{{ContractDate}}"] = currentDate.ToString("dd.MM.yyyy");
            replacements["{{Contract_Date}}"] = currentDate.ToString("dd.MM.yyyy");
            
            // Плейсхолдеры для даты: {{nm}} - день, {{mounts}} - месяц, {{yr}} - год (текущая дата)
            replacements["{{nm}}"] = currentDate.Day.ToString("D2");
            replacements["{{mounts}}"] = GetMonthName(currentDate.Month);
            replacements["{{yr}}"] = currentDate.Year.ToString();
            
            // Также добавляем номер договора
            replacements["{{number_dogovor}}"] = contract.ContractNumber;

            // Подписант - берем из выбранного в комбобоксе
            if (SignerComboBox.SelectedItem is Signer selectedSigner)
            {
                replacements["{{choice_signer}}"] = selectedSigner.Text;
            }
            else
            {
                replacements["{{choice_signer}}"] = "";
            }

            // Вариант оплаты - берем из выбранного в комбобоксе
            if (PaymentOptionComboBox.SelectedItem is PaymentOption selectedPaymentOption)
            {
                if (selectedPaymentOption.OptionKey == "option1")
                {
                    // Полная оплата - оставляем только option1, option2 удаляем
                    replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
                    replacements["{{option2}}"] = ""; // Удаляем option2
                }
                else if (selectedPaymentOption.OptionKey == "option2")
                {
                    // Оплата частями - оставляем только option2, option1 удаляем
                    replacements["{{option1}}"] = ""; // Удаляем option1
                    replacements["{{option2}}"] = "Оплата осуществляется в следующем порядке:";
                }
            }
            else
            {
                // По умолчанию - полная оплата
                replacements["{{option1}}"] = "Оплата осуществляется в следующем порядке: 100% предоплата до начала обучения.";
                replacements["{{option2}}"] = "";
            }

            // Вариант учебной нагрузки - берем из выбранного в комбобоксе
            if (StudyOptionComboBox.SelectedItem is StudyOption selectedStudyOption)
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

    }
}

