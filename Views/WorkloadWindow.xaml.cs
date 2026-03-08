using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Contract2512.Models;
using Contract2512.Services;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Contract2512.Views
{
    public partial class WorkloadWindow : FluentWindow
    {
        public WorkloadWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            LoadPrograms();
            LoadTeachers();
        }

        private void LoadPrograms()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var programs = db.LearningPrograms
                        .OrderBy(p => p.Name)
                        .ToList();
                    ProgramComboBox.ItemsSource = programs;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при загрузке программ: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadTeachers()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var teachers = db.Teachers
                        .OrderBy(t => t.FullName)
                        .ToList();
                    TeacherComboBox.ItemsSource = teachers;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при загрузке преподавателей: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ProgramComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProgramComboBox.SelectedItem is LearningProgram selectedProgram)
            {
                // Обновляем информацию о программе
                ProgramInfoTextBlock.Text = $"Название: {selectedProgram.Name}\n" +
                                           $"Формат: {selectedProgram.Format}\n" +
                                           $"Объем: {selectedProgram.Hours} академических часов\n" +
                                           $"Количество уроков: {selectedProgram.LessonsCount}\n" +
                                           $"Цена: {selectedProgram.Price:N2} ₽";

                // Активируем кнопку генерации
                GenerateButton.IsEnabled = true;
            }
            else
            {
                ProgramInfoTextBlock.Text = "Выберите программу обучения";
                GenerateButton.IsEnabled = false;
            }
        }

        private void AddTeacherButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TeacherDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        var teacher = new Teacher
                        {
                            FullName = dialog.TeacherName
                        };
                        db.Teachers.Add(teacher);
                        db.SaveChanges();

                        LoadTeachers();
                        TeacherComboBox.SelectedItem = teacher;

                        MessageBox.Show(
                            "Преподаватель успешно добавлен!",
                            "Успех",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при добавлении преподавателя: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramComboBox.SelectedItem is not LearningProgram selectedProgram)
            {
                MessageBox.Show(
                    "Выберите программу обучения!",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Загружаем модули программы
                List<ProgramModule> modules;
                using (var db = new AppDbContext())
                {
                    modules = db.ProgramModules
                        .Where(m => m.ProgramId == selectedProgram.Id)
                        .OrderBy(m => m.ModuleNumber)
                        .ToList();
                }

                if (modules.Count == 0)
                {
                    MessageBox.Show(
                        "У выбранной программы нет модулей!\nВозможно, данные не были загружены из парсера.",
                        "Внимание",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Путь к шаблону
                string templatePath = @"C:\Dogovora\Шаблон формирования учебной нагрузки.xlsx";
                
                if (!File.Exists(templatePath))
                {
                    MessageBox.Show(
                        $"Файл шаблона не найден:\n{templatePath}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Создаем папку для сохранения документов
                string documentsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Учебная нагрузка");
                Directory.CreateDirectory(documentsFolder);

                // Формируем имя файла
                string fileName = $"Учебная_нагрузка_{selectedProgram.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                // Убираем недопустимые символы из имени файла
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }
                string targetPath = Path.Combine(documentsFolder, fileName);

                // Подготавливаем замены
                var replacements = new Dictionary<string, string>
                {
                    { "{{Program_Name}}", selectedProgram.Name ?? "" },
                    { "{{Time}}", selectedProgram.Hours.ToString() },
                    { "{{Teacher}}", TeacherComboBox.SelectedItem is Teacher teacher ? teacher.FullName : "" },
                    { "{{Teatcher}}", TeacherComboBox.SelectedItem is Teacher teacherAlias ? teacherAlias.FullName : "" }
                };

                // Формируем документ
                var excelService = new ExcelDocumentService();
                excelService.GenerateWorkloadDocument(templatePath, targetPath, replacements, modules);

                // Открываем документ
                excelService.OpenDocument(targetPath);

                MessageBox.Show(
                    $"Документ успешно сформирован и сохранен:\n{targetPath}",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при формировании документа: {ex.Message}\n\nДетали: {ex.StackTrace}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
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

    // Диалог для добавления преподавателя
    public partial class TeacherDialog : FluentWindow
    {
        public string TeacherName { get; private set; } = "";

        public TeacherDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Добавить преподавателя";
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Width = 400;
            Height = 200;
            ExtendsContentIntoTitleBar = true;

            var grid = new System.Windows.Controls.Grid();
            grid.Background = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(30, 27, 75),
                System.Windows.Media.Color.FromRgb(15, 23, 42),
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1));

            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(32) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // TitleBar
            var titleBar = new System.Windows.Controls.Grid();
            titleBar.Background = System.Windows.Media.Brushes.Transparent;
            titleBar.MouseDown += (s, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); };
            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "Добавить преподавателя",
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            titleBar.Children.Add(titleText);
            System.Windows.Controls.Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            // Content
            var contentBorder = new System.Windows.Controls.Border
            {
                Margin = new Thickness(20),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)) { Opacity = 0.3 },
                CornerRadius = new CornerRadius(12),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1)
            };

            var contentGrid = new System.Windows.Controls.Grid();
            contentGrid.Margin = new Thickness(20);
            contentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var label = new System.Windows.Controls.TextBlock
            {
                Text = "ФИО преподавателя:",
                FontSize = 14,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            System.Windows.Controls.Grid.SetRow(label, 0);
            contentGrid.Children.Add(label);

            var textBox = new System.Windows.Controls.TextBox
            {
                FontSize = 14,
                Padding = new Thickness(8, 6, 8, 6),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)) { Opacity = 0.4 },
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
                BorderThickness = new Thickness(1)
            };
            System.Windows.Controls.Grid.SetRow(textBox, 1);
            contentGrid.Children.Add(textBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "Добавить",
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    MessageBox.Show("Введите ФИО преподавателя!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                TeacherName = textBox.Text.Trim();
                DialogResult = true;
                Close();
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Отмена",
                Padding = new Thickness(16, 10, 16, 10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 85, 99)) { Opacity = 0.3 },
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            cancelButton.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);
            contentGrid.Children.Add(buttonPanel);

            contentBorder.Child = contentGrid;
            System.Windows.Controls.Grid.SetRow(contentBorder, 1);
            grid.Children.Add(contentBorder);

            Content = grid;
        }
    }
}
