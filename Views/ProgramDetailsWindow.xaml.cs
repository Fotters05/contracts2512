using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Contract2512.Models;
using Contract2512.Services;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui.Controls;

namespace Contract2512.Views
{
    public partial class ProgramDetailsWindow : FluentWindow
    {
        private readonly LearningProgram _program;
        private List<ProgramModule> _modules;

        public ProgramDetailsWindow(LearningProgram program)
        {
            InitializeComponent();
            _program = program;
            _modules = new List<ProgramModule>();
            
            LoadProgramData();
        }

        private void LoadProgramData()
        {
            // Загружаем основную информацию о программе
            ProgramNameText.Text = _program.Name;
            HoursText.Text = $"{_program.Hours} часов";
            LessonsText.Text = $"{_program.LessonsCount} занятий";
            PriceText.Text = $"{_program.Price:N0} ₽";

            // Загружаем модули из БД
            using (var db = new AppDbContext())
            {
                _modules = db.ProgramModules
                    .Where(pm => pm.ProgramId == _program.Id)
                    .OrderBy(pm => pm.ModuleNumber)
                    .ToList();
            }

            // Отображаем информацию
            if (_modules.Count > 0)
            {
                DescriptionText.Text = $"Программа содержит {_modules.Count} модулей";
            }
            else
            {
                DescriptionText.Text = "Модули не загружены. Импортируйте программу с сайта.";
            }

            DisplayModules();
        }

        private void DisplayModules()
        {
            ModulesPanel.Children.Clear();

            if (_modules.Count == 0)
            {
                NoModulesText.Visibility = Visibility.Visible;
                ModulesPanel.Children.Add(NoModulesText);
                return;
            }

            NoModulesText.Visibility = Visibility.Collapsed;

            foreach (var module in _modules.OrderBy(m => m.ModuleNumber))
            {
                var moduleCard = CreateModuleCard(module);
                ModulesPanel.Children.Add(moduleCard);
            }
        }

        private UIElement CreateModuleCard(ProgramModule module)
        {
            var card = new Card
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(15)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel();

            // Заголовок модуля
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var numberText = new System.Windows.Controls.TextBlock
            {
                Text = $"Модуль {module.ModuleNumber}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 10, 0)
            };
            titlePanel.Children.Add(numberText);

            if (module.Hours.HasValue)
            {
                var hoursText = new System.Windows.Controls.TextBlock
                {
                    Text = $"({module.Hours} ч.)",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                titlePanel.Children.Add(hoursText);
            }

            stackPanel.Children.Add(titlePanel);

            // Название модуля
            var nameText = new System.Windows.Controls.TextBlock
            {
                Text = module.ModuleName,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            };
            stackPanel.Children.Add(nameText);

            // Описание модуля (если есть)
            if (!string.IsNullOrWhiteSpace(module.Description))
            {
                var descText = new System.Windows.Controls.TextBlock
                {
                    Text = module.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 5, 0, 0),
                    FontSize = 12
                };
                stackPanel.Children.Add(descText);
            }

            Grid.SetColumn(stackPanel, 0);
            grid.Children.Add(stackPanel);

            // Кнопки управления
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

            var editButton = new Wpf.Ui.Controls.Button
            {
                Content = "",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Edit24 },
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                Margin = new Thickness(5, 0, 0, 0),
                Tag = module
            };
            editButton.Click += EditModuleButton_Click;
            buttonsPanel.Children.Add(editButton);

            var deleteButton = new Wpf.Ui.Controls.Button
            {
                Content = "",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Delete24 },
                Appearance = Wpf.Ui.Controls.ControlAppearance.Danger,
                Margin = new Thickness(5, 0, 0, 0),
                Tag = module
            };
            deleteButton.Click += DeleteModuleButton_Click;
            buttonsPanel.Children.Add(deleteButton);

            Grid.SetColumn(buttonsPanel, 1);
            grid.Children.Add(buttonsPanel);

            card.Content = grid;
            return card;
        }

        private async void LoadFromWebButton_Click(object sender, RoutedEventArgs e)
        {
            // МЕТОД ВРЕМЕННО ОТКЛЮЧЕН - требует рефакторинга после удаления ProgramContent
            System.Windows.MessageBox.Show(
                "Функция загрузки с сайта временно недоступна.\n" +
                "Используйте кнопку 'Загрузить программы с сайта' на главной странице для импорта программ.",
                "Информация",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            
            /* СТАРЫЙ КОД - ЗАКОММЕНТИРОВАН
            try
            {
                LoadFromWebButton.IsEnabled = false;
                LoadFromWebButton.Content = "Загрузка...";

                // Используем сохраненный URL программы
                string programUrl = _program.SourceUrl;
                
                if (string.IsNullOrWhiteSpace(programUrl))
                {
                    // Если URL не сохранен, пробуем сформировать его из названия
                    var slug = _program.Name.ToLower()
                        .Replace(" ", "-")
                        .Replace("«", "")
                        .Replace("»", "")
                        .Replace("\"", "")
                        .Replace(":", "")
                        .Replace(",", "");
                    programUrl = $"https://25-12.ru/courses/{slug}";
                    
                    Console.WriteLine($"⚠️ URL программы не сохранен, используем сгенерированный: {programUrl}");
                }
                else
                {
                    Console.WriteLine($"✓ Используем сохраненный URL: {programUrl}");
                }

                var parser = new WebParserService();
                var (content, modules) = await parser.ParseProgramFromUrl(programUrl);

                if (content != null)
                {
                    _programContent = content;
                    _programContent.ProgramId = _program.Id;
                    
                    DescriptionText.Text = content.Description ?? "Описание не найдено";
                    
                    if (!string.IsNullOrWhiteSpace(content.DocumentType))
                    {
                        DocumentPanel.Visibility = Visibility.Visible;
                        DocumentText.Text = content.DocumentType;
                    }

                    if (!string.IsNullOrWhiteSpace(content.ProfessionalStandard))
                    {
                        StandardPanel.Visibility = Visibility.Visible;
                        StandardText.Text = content.ProfessionalStandard;
                    }
                }

                if (modules.Count > 0)
                {
                    _modules = modules;
                    foreach (var module in _modules)
                    {
                        module.ProgramId = _program.Id;
                    }
                    DisplayModules();
                    
                    System.Windows.MessageBox.Show(
                        $"Загружено модулей: {modules.Count}",
                        "Успех",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Модули не найдены на странице. Попробуйте добавить их вручную или проверьте URL программы.\n\n" +
                        $"URL: {programUrl}",
                        "Внимание",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке данных: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Ошибка при загрузке данных:\n{ex.Message}\n\n" +
                    $"URL программы: {_program.SourceUrl ?? "не сохранен"}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadFromWebButton.IsEnabled = true;
                LoadFromWebButton.Content = "Загрузить с сайта";
            }
            */
        }

        private void AddModuleButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ModuleEditDialog(null, _modules.Count + 1);
            if (dialog.ShowDialog() == true && dialog.Module != null)
            {
                dialog.Module.ProgramId = _program.Id;
                _modules.Add(dialog.Module);
                DisplayModules();
            }
        }

        private void EditModuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button && button.Tag is ProgramModule module)
            {
                var dialog = new ModuleEditDialog(module, module.ModuleNumber);
                if (dialog.ShowDialog() == true)
                {
                    DisplayModules();
                }
            }
        }

        private void DeleteModuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button && button.Tag is ProgramModule module)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Удалить модуль {module.ModuleNumber}: {module.ModuleName}?",
                    "Подтверждение",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _modules.Remove(module);
                    
                    // Перенумеруем модули
                    int number = 1;
                    foreach (var m in _modules.OrderBy(m => m.ModuleNumber))
                    {
                        m.ModuleNumber = number++;
                    }
                    
                    DisplayModules();
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Удаляем старые модули
                    var oldModules = db.ProgramModules.Where(pm => pm.ProgramId == _program.Id).ToList();
                    db.ProgramModules.RemoveRange(oldModules);

                    // Добавляем новые модули
                    foreach (var module in _modules)
                    {
                        module.Id = 0; // Сбрасываем ID для создания новых записей
                        module.CreatedAt = DateTime.Now;
                        module.UpdatedAt = DateTime.Now;
                        db.ProgramModules.Add(module);
                    }

                    db.SaveChanges();
                }

                System.Windows.MessageBox.Show(
                    "Данные успешно сохранены!",
                    "Успех",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при сохранении: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Диалог для редактирования модуля
    /// </summary>
    public class ModuleEditDialog : Window
    {
        public ProgramModule? Module { get; private set; }

        private System.Windows.Controls.TextBox _numberTextBox;
        private System.Windows.Controls.TextBox _nameTextBox;
        private System.Windows.Controls.TextBox _hoursTextBox;
        private System.Windows.Controls.TextBox _descriptionTextBox;

        public ModuleEditDialog(ProgramModule? module, int defaultNumber)
        {
            Title = module == null ? "Добавить модуль" : "Редактировать модуль";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            Module = module ?? new ProgramModule { ModuleNumber = defaultNumber };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Номер модуля
            var numberLabel = new System.Windows.Controls.TextBlock { Text = "Номер модуля:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(numberLabel, 0);
            grid.Children.Add(numberLabel);

            _numberTextBox = new System.Windows.Controls.TextBox { Text = Module.ModuleNumber.ToString(), Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(_numberTextBox, 0);
            _numberTextBox.Margin = new Thickness(0, 25, 0, 15);
            grid.Children.Add(_numberTextBox);

            // Название модуля
            var nameLabel = new System.Windows.Controls.TextBlock { Text = "Название модуля:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(nameLabel, 1);
            grid.Children.Add(nameLabel);

            _nameTextBox = new System.Windows.Controls.TextBox { Text = Module.ModuleName, Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(_nameTextBox, 1);
            _nameTextBox.Margin = new Thickness(0, 25, 0, 15);
            grid.Children.Add(_nameTextBox);

            // Часы
            var hoursLabel = new System.Windows.Controls.TextBlock { Text = "Часы (необязательно):", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(hoursLabel, 2);
            grid.Children.Add(hoursLabel);

            _hoursTextBox = new System.Windows.Controls.TextBox { Text = Module.Hours?.ToString() ?? "", Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(_hoursTextBox, 2);
            _hoursTextBox.Margin = new Thickness(0, 25, 0, 15);
            grid.Children.Add(_hoursTextBox);

            // Описание
            var descLabel = new System.Windows.Controls.TextBlock { Text = "Описание (необязательно):", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(descLabel, 3);
            grid.Children.Add(descLabel);

            _descriptionTextBox = new System.Windows.Controls.TextBox 
            { 
                Text = Module.Description ?? "", 
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 25, 0, 15)
            };
            Grid.SetRow(_descriptionTextBox, 3);
            grid.Children.Add(_descriptionTextBox);

            // Кнопки
            var buttonsPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right 
            };
            Grid.SetRow(buttonsPanel, 4);

            var saveButton = new System.Windows.Controls.Button 
            { 
                Content = "Сохранить", 
                Width = 100, 
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            saveButton.Click += SaveButton_Click;
            buttonsPanel.Children.Add(saveButton);

            var cancelButton = new System.Windows.Controls.Button 
            { 
                Content = "Отмена", 
                Width = 100,
                IsCancel = true
            };
            cancelButton.Click += (s, e) => Close();
            buttonsPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonsPanel);

            Content = grid;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Введите название модуля!", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(_numberTextBox.Text, out int number) || number < 1)
            {
                System.Windows.MessageBox.Show("Введите корректный номер модуля!", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            Module!.ModuleNumber = number;
            Module.ModuleName = _nameTextBox.Text.Trim();
            Module.Description = string.IsNullOrWhiteSpace(_descriptionTextBox.Text) ? null : _descriptionTextBox.Text.Trim();
            
            if (int.TryParse(_hoursTextBox.Text, out int hours) && hours > 0)
            {
                Module.Hours = hours;
            }
            else
            {
                Module.Hours = null;
            }

            DialogResult = true;
            Close();
        }
    }
}
