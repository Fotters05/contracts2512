using System;
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
    public partial class PersonWindow : FluentWindow
    {
        private Person _person;
        private bool _isEditMode;

        public PersonWindow(Person person = null)
        {
            InitializeComponent();
            _person = person;
            _isEditMode = person != null;
            LoadData();
        }

        private void LoadData()
        {
            using (var db = new AppDbContext())
            {
                // Загружаем список полов
                GenderComboBox.ItemsSource = db.Genders.ToList();
                GenderComboBox.SelectedIndex = 0;

                // Загружаем справочники образования
                BaseEducationComboBox.ItemsSource = db.BaseEducations.ToList();
                EducationLevelComboBox.ItemsSource = db.EducationLevels.ToList();

                // Добавляем обработчики для открытия ComboBox при клике на поле
                GenderComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
                BaseEducationComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
                EducationLevelComboBox.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;

                if (_isEditMode && _person != null)
                {
                    // Заполняем поля данными существующего лица
                    LastNameTextBox.Text = _person.LastName;
                    FirstNameTextBox.Text = _person.FirstName;
                    PatronymicTextBox.Text = _person.Patronymic ?? "";
                    DateOfBirthPicker.SelectedDate = _person.DateOfBirth;
                    GenderComboBox.SelectedValue = _person.GenderId;
                    PlaceOfBirthTextBox.Text = _person.PlaceOfBirth ?? "";
                    CitizenshipTextBox.Text = _person.Citizenship;
                    SnilsTextBox.Text = _person.Snils;
                    InnTextBox.Text = _person.Inn ?? "";
                    WorkplaceTextBox.Text = _person.Workplace ?? "";

                    // Загружаем паспорт
                    var passport = db.Passports.FirstOrDefault(p => p.PersonId == _person.Id);
                    if (passport != null)
                    {
                        PassportSeriesTextBox.Text = passport.Series;
                        PassportNumberTextBox.Text = passport.Number;
                        IssuanceDatePicker.SelectedDate = passport.IssuanceDate;
                        IssuedByTextBox.Text = passport.IssuedBy;
                        DivisionCodeTextBox.Text = passport.DivisionCode;
                        RegistrationDatePicker.SelectedDate = passport.RegistrationDate;
                        RegistrationAddressTextBox.Text = passport.RegistrationAddress ?? "";
                    }

                    // Загружаем контакты
                    if (_person.ContactsId.HasValue)
                    {
                        var contacts = db.Contacts.Find(_person.ContactsId.Value);
                        if (contacts != null)
                        {
                            PostalCodeTextBox.Text = contacts.PostalCode ?? "";
                            RegionTextBox.Text = contacts.Region ?? "";
                            CityTextBox.Text = contacts.City ?? "";
                            ResidenceAddressTextBox.Text = contacts.ResidenceAddress ?? "";
                            ContactPhoneTextBox.Text = contacts.ContactPhone;
                            EmailTextBox.Text = contacts.Email ?? "";
                            HomePhoneTextBox.Text = contacts.HomePhone ?? "";
                            WorkPhoneTextBox.Text = contacts.WorkPhone ?? "";
                        }
                    }

                    // Загружаем образование (берем первое, если есть несколько)
                    var education = db.Educations.FirstOrDefault(e => e.PersonId == _person.Id);
                    if (education != null)
                    {
                        EnrollmentDatePicker.SelectedDate = education.EnrollmentDate;
                        if (education.BaseEducationId.HasValue)
                        {
                            BaseEducationComboBox.SelectedValue = education.BaseEducationId.Value;
                        }
                        if (education.EducationLevelId.HasValue)
                        {
                            EducationLevelComboBox.SelectedValue = education.EducationLevelId.Value;
                        }
                        EducationSeriesTextBox.Text = education.Series ?? "";
                        EducationNumberTextBox.Text = education.Number;
                        EducationIssueDatePicker.SelectedDate = education.IssueDate;
                        EducationIssuedByTextBox.Text = education.IssuedBy;
                        EducationPlaceOfIssueTextBox.Text = education.PlaceOfIssue;
                        EducationCityTextBox.Text = education.City ?? "";
                        EducationSpecialtyTextBox.Text = education.Specialty ?? "";
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(LastNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(FirstNameTextBox.Text) ||
                DateOfBirthPicker.SelectedDate == null ||
                GenderComboBox.SelectedItem == null ||
                string.IsNullOrWhiteSpace(CitizenshipTextBox.Text) ||
                string.IsNullOrWhiteSpace(SnilsTextBox.Text) ||
                string.IsNullOrWhiteSpace(ContactPhoneTextBox.Text))
            {
                MessageBox.Show("Заполните все обязательные поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    if (_isEditMode && _person != null)
                    {
                        // Обновление существующего лица
                        var person = db.Persons.Find(_person.Id);
                        if (person != null)
                        {
                            UpdatePersonData(person, db);
                            db.SaveChanges();
                        }
                    }
                    else
                    {
                        // Создание нового лица
                        var person = new Person();
                        db.Persons.Add(person);
                        UpdatePersonData(person, db);
                        db.SaveChanges();
                    }
                    MessageBox.Show("Данные успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка при сохранении: {ex.Message}";
                
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nВнутренняя ошибка: {ex.InnerException.Message}";
                    
                    if (ex.InnerException.InnerException != null)
                    {
                        errorMessage += $"\n\nДетали: {ex.InnerException.InnerException.Message}";
                    }
                }
                
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePersonData(Person person, AppDbContext db)
        {
            // Устанавливаем CreatedAt только для новых записей
            if (person.Id == 0)
            {
                person.CreatedAt = DateTime.Now;
            }

            person.LastName = LastNameTextBox.Text;
            person.FirstName = FirstNameTextBox.Text;
            person.Patronymic = string.IsNullOrWhiteSpace(PatronymicTextBox.Text) ? null : PatronymicTextBox.Text;
            person.DateOfBirth = DateOfBirthPicker.SelectedDate.Value;
            person.GenderId = ((Gender)GenderComboBox.SelectedItem).Id;
            person.PlaceOfBirth = string.IsNullOrWhiteSpace(PlaceOfBirthTextBox.Text) ? null : PlaceOfBirthTextBox.Text;
            person.Citizenship = CitizenshipTextBox.Text;
            person.Snils = SnilsTextBox.Text;
            person.Inn = string.IsNullOrWhiteSpace(InnTextBox.Text) ? null : InnTextBox.Text;
            person.Workplace = string.IsNullOrWhiteSpace(WorkplaceTextBox.Text) ? null : WorkplaceTextBox.Text;
            person.UpdatedAt = DateTime.Now;

            // Обновляем или создаем контакты
            Contacts contacts;
            if (person.ContactsId.HasValue)
            {
                contacts = db.Contacts.Find(person.ContactsId.Value);
            }
            else
            {
                contacts = new Contacts();
                db.Contacts.Add(contacts);
            }

            contacts.PostalCode = string.IsNullOrWhiteSpace(PostalCodeTextBox.Text) ? null : PostalCodeTextBox.Text;
            contacts.Region = string.IsNullOrWhiteSpace(RegionTextBox.Text) ? null : RegionTextBox.Text;
            contacts.City = string.IsNullOrWhiteSpace(CityTextBox.Text) ? null : CityTextBox.Text;
            contacts.ResidenceAddress = string.IsNullOrWhiteSpace(ResidenceAddressTextBox.Text) ? null : ResidenceAddressTextBox.Text;
            contacts.ContactPhone = ContactPhoneTextBox.Text;
            contacts.Email = string.IsNullOrWhiteSpace(EmailTextBox.Text) ? null : EmailTextBox.Text;
            contacts.HomePhone = string.IsNullOrWhiteSpace(HomePhoneTextBox.Text) ? null : HomePhoneTextBox.Text;
            contacts.WorkPhone = string.IsNullOrWhiteSpace(WorkPhoneTextBox.Text) ? null : WorkPhoneTextBox.Text;
            contacts.CreatedAt = contacts.CreatedAt ?? DateTime.Now;

                    db.SaveChanges(); // Сохраняем контакты, чтобы получить ID
            person.ContactsId = contacts.Id;

            // Сохраняем изменения, чтобы получить ID для person (если это новый)
            db.SaveChanges();

            // Обновляем или создаем паспорт
            var passport = db.Passports.FirstOrDefault(p => p.PersonId == person.Id);
            if (passport == null)
            {
                passport = new Passport { PersonId = person.Id };
                db.Passports.Add(passport);
            }

            if (!string.IsNullOrWhiteSpace(PassportSeriesTextBox.Text) && 
                !string.IsNullOrWhiteSpace(PassportNumberTextBox.Text) &&
                IssuanceDatePicker.SelectedDate.HasValue &&
                RegistrationDatePicker.SelectedDate.HasValue)
            {
                passport.Series = PassportSeriesTextBox.Text;
                passport.Number = PassportNumberTextBox.Text;
                passport.IssuanceDate = IssuanceDatePicker.SelectedDate.Value;
                passport.IssuedBy = IssuedByTextBox.Text;
                passport.DivisionCode = DivisionCodeTextBox.Text;
                passport.RegistrationDate = RegistrationDatePicker.SelectedDate.Value;
                passport.RegistrationAddress = string.IsNullOrWhiteSpace(RegistrationAddressTextBox.Text) ? null : RegistrationAddressTextBox.Text;
                passport.CreatedAt = passport.CreatedAt ?? DateTime.Now;
            }
            else
            {
                // Сохраняем адрес регистрации даже если не все поля паспорта заполнены
                passport.RegistrationAddress = string.IsNullOrWhiteSpace(RegistrationAddressTextBox.Text) ? null : RegistrationAddressTextBox.Text;
            }

            // Обновляем или создаем образование
            var education = db.Educations.FirstOrDefault(e => e.PersonId == person.Id);
            if (education == null)
            {
                education = new Education { PersonId = person.Id };
                db.Educations.Add(education);
            }

            // Сохраняем образование только если заполнены обязательные поля
            if (EnrollmentDatePicker.SelectedDate.HasValue &&
                !string.IsNullOrWhiteSpace(EducationNumberTextBox.Text) &&
                EducationIssueDatePicker.SelectedDate.HasValue &&
                !string.IsNullOrWhiteSpace(EducationIssuedByTextBox.Text) &&
                !string.IsNullOrWhiteSpace(EducationPlaceOfIssueTextBox.Text))
            {
                education.EnrollmentDate = EnrollmentDatePicker.SelectedDate.Value;
                education.BaseEducationId = BaseEducationComboBox.SelectedValue != null 
                    ? (short?)BaseEducationComboBox.SelectedValue 
                    : null;
                education.EducationLevelId = EducationLevelComboBox.SelectedValue != null 
                    ? (short?)EducationLevelComboBox.SelectedValue 
                    : null;
                education.Series = string.IsNullOrWhiteSpace(EducationSeriesTextBox.Text) ? null : EducationSeriesTextBox.Text;
                education.Number = EducationNumberTextBox.Text;
                education.IssueDate = EducationIssueDatePicker.SelectedDate.Value;
                education.IssuedBy = EducationIssuedByTextBox.Text;
                education.PlaceOfIssue = EducationPlaceOfIssueTextBox.Text;
                education.City = string.IsNullOrWhiteSpace(EducationCityTextBox.Text) ? null : EducationCityTextBox.Text;
                education.Specialty = string.IsNullOrWhiteSpace(EducationSpecialtyTextBox.Text) ? null : EducationSpecialtyTextBox.Text;
                education.CreatedAt = education.CreatedAt ?? DateTime.Now;
            }
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

    }
}

