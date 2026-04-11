using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Contract2512.Models;
using Contract2512.Services;
using Microsoft.EntityFrameworkCore;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace Contract2512.Views
{
    public partial class WorkloadWindow : FluentWindow
    {
        private List<Contract> _availableContracts = new();
        private List<Contract> _selectedContracts = new();
        private Dictionary<long, Dictionary<string, List<TimeSlot>>> _contractSchedules = new();

        public WorkloadWindow()
        {
            InitializeComponent();
            LoadData();
            UpdateSelectedListenersSummary();
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
                using var db = new AppDbContext();
                ProgramComboBox.ItemsSource = db.LearningPrograms
                    .AsNoTracking()
                    .OrderBy(p => p.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке программ: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTeachers()
        {
            try
            {
                using var db = new AppDbContext();
                TeacherComboBox.ItemsSource = db.Teachers
                    .AsNoTracking()
                    .OrderBy(t => t.FullName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке преподавателей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProgramComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProgramComboBox.SelectedItem is not LearningProgram selectedProgram)
            {
                ProgramInfoTextBlock.Text = "Выберите программу обучения";
                GroupNamePanel.Visibility = Visibility.Collapsed;
                _availableContracts.Clear();
                _selectedContracts.Clear();
                UpdateSelectedListenersSummary();
                GenerateButton.IsEnabled = false;
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var programType = db.ProgramViews
                    .Where(pv => pv.Id == selectedProgram.ProgramViewId)
                    .Select(pv => pv.Name)
                    .FirstOrDefault() ?? "Не указан";

                var selectedProgramCategory = DetectProgramCategoryFromProgramView(programType);

                ProgramInfoTextBlock.Text =
                    $"Название: {selectedProgram.Name}\n" +
                    $"Тип программы: {programType}\n" +
                    $"Формат: {selectedProgram.Format}\n" +
                    $"Объем: {selectedProgram.Hours} академических часов\n" +
                    $"Количество уроков: {selectedProgram.LessonsCount}\n" +
                    $"Цена: {selectedProgram.Price:N2} ₽";

                _availableContracts = db.Contracts
                    .AsNoTracking()
                    .Include(c => c.Listener)
                    .Include(c => c.ContractType)
                    .Where(c => c.ProgramId == selectedProgram.Id)
                    .OrderByDescending(c => c.ContractDate)
                    .ToList()
                    .Where(c => selectedProgramCategory == null || IsContractCompatibleWithProgramCategory(c, selectedProgramCategory.Value))
                    .Where(c => c.Listener != null)
                    .GroupBy(c => c.ListenerId)
                    .Select(group => group.First())
                    .OrderBy(c => c.Listener!.LastName)
                    .ThenBy(c => c.Listener!.FirstName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке договоров слушателей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _availableContracts.Clear();
            }

            GroupNamePanel.Visibility = IsGroupProgram(selectedProgram) ? Visibility.Visible : Visibility.Collapsed;
            if (!IsGroupProgram(selectedProgram))
            {
                GroupNameTextBox.Text = string.Empty;
            }

            _selectedContracts.Clear();
            UpdateSelectedListenersSummary();
            GenerateButton.IsEnabled = true;
        }

        private void AddTeacherButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TeacherDialog { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var teacher = new Teacher { FullName = dialog.TeacherName };
                db.Teachers.Add(teacher);
                db.SaveChanges();
                LoadTeachers();
                TeacherComboBox.SelectedItem = teacher;
                MessageBox.Show("Преподаватель успешно добавлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении преподавателя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectListenersButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramComboBox.SelectedItem is not LearningProgram selectedProgram)
            {
                MessageBox.Show("Сначала выберите программу обучения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_availableContracts.Count == 0)
            {
                MessageBox.Show("Для выбранной программы не найдено договоров слушателей.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new ListenerSelectionDialog(_availableContracts, _selectedContracts, IsGroupProgram(selectedProgram))
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedContracts = dialog.SelectedContracts;
                UpdateSelectedListenersSummary();
                
                // Включаем кнопку настройки расписания если выбраны слушатели
                ConfigureScheduleButton.IsEnabled = _selectedContracts.Count > 0;
            }
        }

        private void ConfigureScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramComboBox.SelectedItem is not LearningProgram selectedProgram)
            {
                MessageBox.Show("Сначала выберите программу обучения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedContracts.Count == 0)
            {
                MessageBox.Show("Сначала выберите слушателей.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isGroupMode = IsGroupProgram(selectedProgram);
            
            // Для индивидуального режима настраиваем расписание для каждого слушателя отдельно
            if (!isGroupMode)
            {
                foreach (var contract in _selectedContracts)
                {
                    // Извлекаем информацию о часах в неделю и продолжительности из договора
                    var (hoursPerWeek, weeksDuration) = ExtractWorkloadInfo(contract);
                    
                    var dialog = new WorkloadScheduleDialog(contract, isGroupMode, hoursPerWeek, weeksDuration)
                    {
                        Owner = this
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        _contractSchedules[contract.Id] = dialog.Schedule;
                    }
                    else
                    {
                        // Если пользователь отменил настройку для одного из слушателей
                        return;
                    }
                }
            }
            else
            {
                // Для группового режима одно расписание для всех
                var firstContract = _selectedContracts.First();
                var (hoursPerWeek, weeksDuration) = ExtractWorkloadInfo(firstContract);
                
                var dialog = new WorkloadScheduleDialog(firstContract, isGroupMode, hoursPerWeek, weeksDuration)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    // Применяем одно расписание ко всем слушателям в группе
                    foreach (var contract in _selectedContracts)
                    {
                        _contractSchedules[contract.Id] = dialog.Schedule;
                    }
                }
                else
                {
                    return;
                }
            }
            
            UpdateScheduleInfo();
        }

        private (int hoursPerWeek, int weeksDuration) ExtractWorkloadInfo(Contract contract)
        {
            // Извлекаем информацию из TimeOptionKey или StudyOptionKey
            int hoursPerWeek = 2; // По умолчанию
            int weeksDuration = 10; // По умолчанию
            
            try
            {
                using var db = new AppDbContext();
                var fullContract = db.Contracts
                    .AsNoTracking()
                    .Include(c => c.ContractType)
                    .FirstOrDefault(c => c.Id == contract.Id);
                
                if (fullContract != null)
                {
                    bool isDopContract = IsDopContractType(fullContract.ContractType?.Name);

                    if (isDopContract && !string.IsNullOrEmpty(fullContract.StudyOptionKey))
                    {
                        var studyOption = GetStudyOptionByKey(fullContract.StudyOptionKey);
                        if (studyOption != null)
                        {
                            hoursPerWeek = studyOption.HoursPerWeek;
                            weeksDuration = studyOption.WeeksDuration;
                        }
                    }
                    else if (!string.IsNullOrEmpty(fullContract.TimeOptionKey))
                    {
                        var timeOption = GetTimeOptionByKey(fullContract.TimeOptionKey, fullContract.ContractType?.Name);
                        if (timeOption != null)
                        {
                            hoursPerWeek = timeOption.HoursPerWeek;
                            weeksDuration = timeOption.WeeksDuration;
                        }
                    }
                    else if (!string.IsNullOrEmpty(fullContract.StudyOptionKey))
                    {
                        var studyOption = GetStudyOptionByKey(fullContract.StudyOptionKey);
                        if (studyOption != null)
                        {
                            hoursPerWeek = studyOption.HoursPerWeek;
                            weeksDuration = studyOption.WeeksDuration;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка извлечения информации о нагрузке: {ex.Message}");
            }
            
            return (hoursPerWeek, weeksDuration);
        }

        private static bool IsDopContractType(string? contractTypeName)
        {
            return !string.IsNullOrWhiteSpace(contractTypeName) &&
                   (contractTypeName.Contains("ДОП", StringComparison.OrdinalIgnoreCase) ||
                    contractTypeName.Contains("дополнительное образование", StringComparison.OrdinalIgnoreCase));
        }

        private TimeOptionInfo? GetTimeOptionByKey(string optionKey, string? contractTypeName)
        {
            // Определяем тип договора и возвращаем соответствующую опцию
            var options = new Dictionary<string, TimeOptionInfo>
            {
                // ПК опции
                { "Option_Time1", new TimeOptionInfo { HoursPerWeek = 3, WeeksDuration = 21 } },
                { "Option_Time2", new TimeOptionInfo { HoursPerWeek = 6, WeeksDuration = 11 } },
                { "Option_Time3", new TimeOptionInfo { HoursPerWeek = 12, WeeksDuration = 6 } },
                { "Option_Time4", new TimeOptionInfo { HoursPerWeek = 15, WeeksDuration = 5 } },
                { "Option_Time5", new TimeOptionInfo { HoursPerWeek = 30, WeeksDuration = 3 } },
                { "Option_Time6", new TimeOptionInfo { HoursPerWeek = 32, WeeksDuration = 3 } },
            };
            
            // Для ПП опции отличаются
            if (contractTypeName != null && contractTypeName.Contains("ПП"))
            {
                options["Option_Time3"] = new TimeOptionInfo { HoursPerWeek = 12, WeeksDuration = 21 };
                options["Option_Time4"] = new TimeOptionInfo { HoursPerWeek = 15, WeeksDuration = 17 };
                options["Option_Time5"] = new TimeOptionInfo { HoursPerWeek = 30, WeeksDuration = 9 };
                options["Option_Time6"] = new TimeOptionInfo { HoursPerWeek = 32, WeeksDuration = 8 };
            }
            
            return options.TryGetValue(optionKey, out var option) ? option : null;
        }

        private StudyOptionInfo? GetStudyOptionByKey(string optionKey)
        {
            var options = new Dictionary<string, StudyOptionInfo>
            {
                { "Option_study1", new StudyOptionInfo { HoursPerWeek = 1, WeeksDuration = 20 } },
                { "Option_study2", new StudyOptionInfo { HoursPerWeek = 2, WeeksDuration = 10 } },
                { "Option_study3", new StudyOptionInfo { HoursPerWeek = 4, WeeksDuration = 5 } },
                { "Option_study4", new StudyOptionInfo { HoursPerWeek = 8, WeeksDuration = 2 } }, // 2.5 недели округляем до 2
                { "Option_study5", new StudyOptionInfo { HoursPerWeek = 10, WeeksDuration = 2 } },
            };
            
            return options.TryGetValue(optionKey, out var option) ? option : null;
        }

        private void UpdateScheduleInfo()
        {
            if (_contractSchedules.Count == 0)
            {
                ScheduleInfoTextBlock.Text = "Расписание не настроено";
                return;
            }
            
            // Показываем краткую информацию о расписании
            var firstSchedule = _contractSchedules.Values.First();
            var days = string.Join(", ", firstSchedule.Keys);
            var totalHours = firstSchedule.Values.Sum(slots => slots.Count);
            
            ScheduleInfoTextBlock.Text = $"Настроено: {days} ({totalHours} ч/нед)";
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramComboBox.SelectedItem is not LearningProgram selectedProgram)
            {
                MessageBox.Show("Выберите программу обучения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isGroupMode = IsGroupProgram(selectedProgram);
            if (_selectedContracts.Count == 0)
            {
                MessageBox.Show(isGroupMode ? "Для группового режима выберите слушателей." : "Выберите слушателя по договору.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!isGroupMode && _selectedContracts.Count > 1)
            {
                MessageBox.Show("В индивидуальном режиме можно выбрать только одного слушателя.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var groupName = GroupNameTextBox.Text?.Trim() ?? string.Empty;
            if (isGroupMode && string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show("Укажите название группы для группового режима.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                List<ProgramModule> modules;
                string programType;
                HashSet<DateTime> holidayDates;

                using (var lookupDb = new AppDbContext())
                {
                    programType = lookupDb.ProgramViews
                        .Where(pv => pv.Id == selectedProgram.ProgramViewId)
                        .Select(pv => pv.Name)
                        .FirstOrDefault() ?? string.Empty;

                    modules = lookupDb.ProgramModules
                        .Where(m => m.ProgramId == selectedProgram.Id)
                        .OrderBy(m => m.ModuleNumber)
                        .ToList();

                    holidayDates = lookupDb.HolidayCalendarDays
                        .AsNoTracking()
                        .Select(h => h.HolidayDate.Date)
                        .ToHashSet();
                }

                if (modules.Count == 0)
                {
                    MessageBox.Show("У выбранной программы нет модулей.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var templatePath = @"C:\Dogovora\Шаблон формирования учебной нагрузки.xlsx";
                if (!File.Exists(templatePath))
                {
                    MessageBox.Show($"Файл шаблона не найден:\n{templatePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var documentsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Учебная нагрузка");
                Directory.CreateDirectory(documentsFolder);

                using var db = new AppDbContext();
                EnsureWorkloadTablesCreated(db);

                var now = DateTime.Now;
                var batch = new WorkloadBatch
                {
                    ProgramId = selectedProgram.Id,
                    TeacherId = (TeacherComboBox.SelectedItem as Teacher)?.Id,
                    GroupName = isGroupMode ? groupName : null,
                    IsGroup = isGroupMode,
                    CreatedAt = now
                };

                db.WorkloadBatches.Add(batch);
                db.SaveChanges();

                var generatedFiles = new List<string>();
                var pdfExportFailures = new List<string>();
                var excelService = new ExcelDocumentService();

                foreach (var contract in _selectedContracts)
                {
                    if (contract.Listener == null)
                    {
                        continue;
                    }

                    var targetPath = Path.Combine(documentsFolder, BuildFileName(selectedProgram.Name, contract.Listener.FullName, now, generatedFiles.Count));
                    var replacements = BuildReplacements(selectedProgram, programType, contract, isGroupMode ? groupName : string.Empty);
                    
                    // Получаем расписание для этого договора
                    Dictionary<string, List<TimeSlot>>? contractSchedule = null;
                    if (_contractSchedules.TryGetValue(contract.Id, out var schedule))
                    {
                        contractSchedule = schedule;
                        System.Diagnostics.Debug.WriteLine($"Расписание найдено для договора {contract.Id}: {schedule.Count} дней");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Расписание НЕ найдено для договора {contract.Id}");
                    }
                    
                    var scheduledLessons = BuildScheduleEntries(
                        modules,
                        contractSchedule,
                        contract.StartDate ?? contract.ContractDate,
                        holidayDates,
                        now);

                    excelService.GenerateWorkloadDocument(templatePath, targetPath, replacements, modules, scheduledLessons);

                    try
                    {
                        excelService.ConvertToPdf(targetPath);
                    }
                    catch (Exception ex)
                    {
                        pdfExportFailures.Add($"{Path.GetFileName(targetPath)}: {ex.Message}");
                    }

                    SaveWorkloadDocumentToDatabase(db, batch, selectedProgram, contract, programType, targetPath, scheduledLessons, isGroupMode, groupName, now);
                    generatedFiles.Add(targetPath);
                }

                if (generatedFiles.Count == 0)
                {
                    MessageBox.Show("Не удалось сформировать ни одного документа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                excelService.OpenDocument(generatedFiles[0]);
                MessageBox.Show(
                    generatedFiles.Count == 1
                        ? $"Документ успешно сформирован:\n{generatedFiles[0]}"
                        : $"Сформировано документов: {generatedFiles.Count}\nПапка: {documentsFolder}",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (pdfExportFailures.Count > 0)
                {
                    MessageBox.Show(
                        "Excel-файлы созданы, но часть PDF-копий не удалось сформировать.\n\n" +
                        string.Join("\n", pdfExportFailures.Take(5)),
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                var errorMessage = $"Ошибка при формировании документов:\n\n{ex.Message}";
                
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nВнутренняя ошибка: {ex.InnerException.Message}";
                }
                
                errorMessage += $"\n\nТип ошибки: {ex.GetType().Name}";
                errorMessage += $"\n\nСтек вызовов:\n{ex.StackTrace}";
                
                MessageBox.Show(errorMessage, "Детальная информация об ошибке", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, string> BuildReplacements(LearningProgram selectedProgram, string programType, Contract contract, string groupName)
        {
            var today = DateTime.Today;

            return new Dictionary<string, string>
            {
                { "{{Program_Name}}", selectedProgram.Name ?? string.Empty },
                { "{{Program_Type}}", programType },
                { "{{Time}}", selectedProgram.Hours.ToString() },
                { "{{Teacher}}", TeacherComboBox.SelectedItem is Teacher teacher ? teacher.FullName : string.Empty },
                { "{{Teatcher}}", TeacherComboBox.SelectedItem is Teacher teacherAlias ? teacherAlias.FullName : string.Empty },
                { "{{FIO_Slushatel}}", contract.Listener?.FullName ?? string.Empty },
                { "{{Group_Name}}", groupName },
                { "{{Date Start}}", contract.StartDate?.ToString("dd.MM.yyyy") ?? string.Empty },
                { "{{Date End}}", contract.EndDate?.ToString("dd.MM.yyyy") ?? string.Empty },
                { "{{Chislo}}", today.Day.ToString() },
                { "{{mounth}}", GetMonthNameInGenitive(today.Month) },
                { "{{year}}", today.Year.ToString() },
                { "{{month}}", GetMonthNameInGenitive(today.Month) }
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateSelectedListenersSummary()
        {
            if (_selectedContracts.Count == 0)
            {
                SelectedListenersTextBlock.Text = "Слушатели не выбраны";
                return;
            }

            SelectedListenersTextBlock.Text = string.Join(
                Environment.NewLine,
                _selectedContracts.Where(c => c.Listener != null).Select(c =>
                {
                    string start = c.StartDate?.ToString("dd.MM.yyyy") ?? "без даты";
                    string end = c.EndDate?.ToString("dd.MM.yyyy") ?? "без даты";
                    return $"{c.Listener!.FullName} ({start} - {end})";
                }));
        }

        private static bool IsGroupProgram(LearningProgram program)
        {
            return !string.IsNullOrWhiteSpace(program.Format) &&
                   program.Format.Contains("С преподавателем в группе", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsContractCompatibleWithProgramCategory(Contract contract, ProgramCategory category)
        {
            var contractCategory = DetectProgramCategoryFromContractType(contract.ContractType?.Name);
            return contractCategory != null && contractCategory.Value == category;
        }

        private static ProgramCategory? DetectProgramCategoryFromContractType(string? contractTypeName)
        {
            if (string.IsNullOrWhiteSpace(contractTypeName))
            {
                return null;
            }

            if (contractTypeName.Contains("ДОП", StringComparison.OrdinalIgnoreCase) ||
                contractTypeName.Contains("дополнительное образование", StringComparison.OrdinalIgnoreCase))
            {
                return ProgramCategory.Dop;
            }

            if (contractTypeName.Contains("ПП", StringComparison.OrdinalIgnoreCase) ||
                contractTypeName.Contains("профпереподготовк", StringComparison.OrdinalIgnoreCase) ||
                contractTypeName.Contains("профессиональной переподготовки", StringComparison.OrdinalIgnoreCase) ||
                contractTypeName.Contains("профессиональная переподготовка", StringComparison.OrdinalIgnoreCase))
            {
                return ProgramCategory.Pp;
            }

            if (contractTypeName.Contains("ПК", StringComparison.OrdinalIgnoreCase) ||
                contractTypeName.Contains("повышение квалификации", StringComparison.OrdinalIgnoreCase) ||
                contractTypeName.Contains("повышения квалификации", StringComparison.OrdinalIgnoreCase))
            {
                return ProgramCategory.Pk;
            }

            return null;
        }

        private static ProgramCategory? DetectProgramCategoryFromProgramView(string? programViewName)
        {
            if (string.IsNullOrWhiteSpace(programViewName))
            {
                return null;
            }

            if (string.Equals(programViewName, "ДОП", StringComparison.OrdinalIgnoreCase))
            {
                return ProgramCategory.Dop;
            }

            if (string.Equals(programViewName, "ПП", StringComparison.OrdinalIgnoreCase))
            {
                return ProgramCategory.Pp;
            }

            if (string.Equals(programViewName, "ПК", StringComparison.OrdinalIgnoreCase))
            {
                return ProgramCategory.Pk;
            }

            return null;
        }

        private static string BuildFileName(string programName, string listenerName, DateTime timestamp, int index)
        {
            string fileName = $"Учебная_нагрузка_{programName}_{listenerName}_{timestamp:yyyyMMdd_HHmmss}_{index + 1}.xlsx";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }

        private static void SaveWorkloadDocumentToDatabase(AppDbContext db, WorkloadBatch batch, LearningProgram selectedProgram, Contract contract, string programType, string targetPath, IReadOnlyList<WorkloadScheduleEntry> scheduledLessons, bool isGroupMode, string groupName, DateTime timestamp)
        {
            var workloadDocument = new WorkloadDocument
            {
                BatchId = batch.Id,
                ProgramId = selectedProgram.Id,
                ContractId = contract.Id,
                ListenerId = contract.ListenerId,
                TeacherId = batch.TeacherId,
                ProgramType = string.IsNullOrWhiteSpace(programType) ? null : programType.Trim(),
                GroupName = isGroupMode ? groupName : null,
                IsGroup = isGroupMode,
                FileName = Path.GetFileName(targetPath),
                FilePath = targetPath,
                GeneratedAt = timestamp,
                CreatedAt = timestamp
            };

            db.WorkloadDocuments.Add(workloadDocument);
            db.SaveChanges();

            var scheduleEntries = scheduledLessons
                .Select(lesson => new WorkloadScheduleEntry
                {
                    WorkloadDocumentId = workloadDocument.Id,
                    LessonNumber = lesson.LessonNumber,
                    ModuleNumber = lesson.ModuleNumber,
                    ModuleName = lesson.ModuleName,
                    Topic = lesson.Topic,
                    LessonDate = lesson.LessonDate,
                    DayOfWeek = lesson.DayOfWeek,
                    StartTime = lesson.StartTime,
                    EndTime = lesson.EndTime,
                    Hours = lesson.Hours,
                    CreatedAt = lesson.CreatedAt
                })
                .ToList();

            if (scheduleEntries.Count > 0)
            {
                db.WorkloadScheduleEntries.AddRange(scheduleEntries);
                db.SaveChanges();
            }
        }

        private static void EnsureWorkloadTablesCreated(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS public.workload_batch
                (
                    id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    program_id BIGINT NOT NULL,
                    teacher_id BIGINT NULL,
                    group_name VARCHAR(255) NULL,
                    is_group BOOLEAN NOT NULL DEFAULT FALSE,
                    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    CONSTRAINT fk_workload_batch_program FOREIGN KEY (program_id) REFERENCES public.learning_program(id) ON DELETE RESTRICT,
                    CONSTRAINT fk_workload_batch_teacher FOREIGN KEY (teacher_id) REFERENCES public.teacher(id) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS ix_workload_batch_program_id ON public.workload_batch(program_id);
                CREATE INDEX IF NOT EXISTS ix_workload_batch_teacher_id ON public.workload_batch(teacher_id);

                CREATE TABLE IF NOT EXISTS public.workload_document
                (
                    id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    program_id BIGINT NOT NULL,
                    batch_id BIGINT NULL,
                    contract_id BIGINT NULL,
                    listener_id BIGINT NULL,
                    teacher_id BIGINT NULL,
                    program_type VARCHAR(255) NULL,
                    group_name VARCHAR(255) NULL,
                    is_group BOOLEAN NOT NULL DEFAULT FALSE,
                    file_name VARCHAR(500) NOT NULL,
                    file_path VARCHAR(1000) NOT NULL,
                    generated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );

                ALTER TABLE public.workload_document ADD COLUMN IF NOT EXISTS batch_id BIGINT NULL;
                ALTER TABLE public.workload_document ADD COLUMN IF NOT EXISTS contract_id BIGINT NULL;
                ALTER TABLE public.workload_document ADD COLUMN IF NOT EXISTS listener_id BIGINT NULL;
                ALTER TABLE public.workload_document ADD COLUMN IF NOT EXISTS group_name VARCHAR(255) NULL;
                ALTER TABLE public.workload_document ADD COLUMN IF NOT EXISTS is_group BOOLEAN NOT NULL DEFAULT FALSE;

                CREATE INDEX IF NOT EXISTS ix_workload_document_program_id ON public.workload_document(program_id);
                CREATE INDEX IF NOT EXISTS ix_workload_document_teacher_id ON public.workload_document(teacher_id);
                CREATE INDEX IF NOT EXISTS ix_workload_document_batch_id ON public.workload_document(batch_id);
                CREATE INDEX IF NOT EXISTS ix_workload_document_contract_id ON public.workload_document(contract_id);
                CREATE INDEX IF NOT EXISTS ix_workload_document_listener_id ON public.workload_document(listener_id);

                CREATE TABLE IF NOT EXISTS public.workload_schedule_entry
                (
                    id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    workload_document_id BIGINT NOT NULL,
                    lesson_number INTEGER NOT NULL,
                    module_number INTEGER NULL,
                    module_name VARCHAR(500) NULL,
                    topic TEXT NOT NULL,
                    lesson_date DATE NULL,
                    day_of_week VARCHAR(50) NULL,
                    start_time TIME WITHOUT TIME ZONE NULL,
                    end_time TIME WITHOUT TIME ZONE NULL,
                    hours INTEGER NULL,
                    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ix_workload_schedule_entry_document_id ON public.workload_schedule_entry(workload_document_id);

                CREATE TABLE IF NOT EXISTS public.holiday_calendar_day
                (
                    id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    holiday_date DATE NOT NULL,
                    holiday_name VARCHAR(255) NULL,
                    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_holiday_calendar_day_holiday_date
                    ON public.holiday_calendar_day(holiday_date);
                """);
        }

        private static List<WorkloadScheduleEntry> BuildScheduleEntries(
            List<ProgramModule> modules,
            Dictionary<string, List<TimeSlot>>? schedule,
            DateTime? periodStartDate,
            ISet<DateTime> holidayDates,
            DateTime createdAt)
        {
            var result = new List<WorkloadScheduleEntry>();
            int lessonNumber = 1;

            foreach (var module in modules.OrderBy(m => m.ModuleNumber))
            {
                foreach (var topic in ExtractTopics(module.Description))
                {
                    result.Add(new WorkloadScheduleEntry
                    {
                        WorkloadDocumentId = 0,
                        LessonNumber = lessonNumber++,
                        ModuleNumber = module.ModuleNumber,
                        ModuleName = string.IsNullOrWhiteSpace(module.ModuleName) ? null : module.ModuleName.Trim(),
                        Topic = topic,
                        LessonDate = null,
                        DayOfWeek = null,
                        StartTime = null,
                        EndTime = null,
                        Hours = 1,
                        CreatedAt = createdAt
                    });
                }
            }

            ApplyScheduleToLessons(result, schedule, periodStartDate, holidayDates);
            return result;
        }

        private static void ApplyScheduleToLessons(
            List<WorkloadScheduleEntry> lessons,
            Dictionary<string, List<TimeSlot>>? schedule,
            DateTime? periodStartDate,
            ISet<DateTime> holidayDates)
        {
            if (lessons.Count == 0 || schedule == null || schedule.Count == 0 || !periodStartDate.HasValue)
            {
                return;
            }

            var scheduleSlots = FlattenSchedule(schedule);
            if (scheduleSlots.Count == 0)
            {
                return;
            }

            var startDate = periodStartDate.Value.Date;
            var lessonDates = BuildLessonDates(scheduleSlots, startDate, holidayDates, lessons.Count);

            for (int i = 0; i < lessons.Count && i < lessonDates.Count; i++)
            {
                var occurrence = lessonDates[i];
                lessons[i].LessonDate = occurrence.Date;
                lessons[i].DayOfWeek = occurrence.DayName;
                lessons[i].StartTime = occurrence.TimeSlot.StartTime;
                lessons[i].EndTime = occurrence.TimeSlot.EndTime;
            }
        }

        private static List<ScheduleSlotDefinition> FlattenSchedule(Dictionary<string, List<TimeSlot>> schedule)
        {
            var orderedDays = new[]
            {
                ("Понедельник", DayOfWeek.Monday),
                ("Вторник", DayOfWeek.Tuesday),
                ("Среда", DayOfWeek.Wednesday),
                ("Четверг", DayOfWeek.Thursday),
                ("Пятница", DayOfWeek.Friday)
            };

            var result = new List<ScheduleSlotDefinition>();
            foreach (var (dayName, dayOfWeek) in orderedDays)
            {
                if (!schedule.TryGetValue(dayName, out var timeSlots))
                {
                    continue;
                }

                foreach (var timeSlot in timeSlots.OrderBy(slot => slot.StartTime))
                {
                    result.Add(new ScheduleSlotDefinition(dayName, dayOfWeek, timeSlot));
                }
            }

            AppendScheduleSlots(result, schedule, "РЎСѓР±Р±РѕС‚Р°", DayOfWeek.Saturday);
            AppendScheduleSlots(result, schedule, "Р’РѕСЃРєСЂРµСЃРµРЅСЊРµ", DayOfWeek.Sunday);

            return result;
        }

        private static void AppendScheduleSlots(
            List<ScheduleSlotDefinition> result,
            Dictionary<string, List<TimeSlot>> schedule,
            string dayName,
            DayOfWeek dayOfWeek)
        {
            if (!schedule.TryGetValue(dayName, out var timeSlots))
            {
                return;
            }

            foreach (var timeSlot in timeSlots.OrderBy(slot => slot.StartTime))
            {
                result.Add(new ScheduleSlotDefinition(dayName, dayOfWeek, timeSlot));
            }
        }

        private static List<LessonOccurrence> BuildLessonDates(
            IReadOnlyList<ScheduleSlotDefinition> scheduleSlots,
            DateTime startDate,
            ISet<DateTime> holidayDates,
            int lessonCount)
        {
            var occurrences = new List<LessonOccurrence>(lessonCount);
            var firstDates = scheduleSlots
                .Select(slot => new
                {
                    Slot = slot,
                    FirstDate = GetFirstDateForDay(startDate, slot.DayOfWeek)
                })
                .ToList();

            var maxWeeksToCheck = Math.Max(lessonCount * 8, 104);
            for (int weekOffset = 0; weekOffset < maxWeeksToCheck && occurrences.Count < lessonCount; weekOffset++)
            {
                var weekOccurrences = firstDates
                    .Select(item => new LessonOccurrence(
                        item.FirstDate.AddDays(weekOffset * 7),
                        item.Slot.DayName,
                        item.Slot.TimeSlot))
                    .Where(item => item.Date >= startDate)
                    .OrderBy(item => item.Date)
                    .ThenBy(item => item.TimeSlot.StartTime)
                    .ToList();

                foreach (var occurrence in weekOccurrences)
                {
                    if (holidayDates.Contains(occurrence.Date.Date))
                    {
                        continue;
                    }

                    occurrences.Add(occurrence);
                    if (occurrences.Count == lessonCount)
                    {
                        break;
                    }
                }
            }

            return occurrences;
        }

        private static DateTime GetFirstDateForDay(DateTime startDate, DayOfWeek targetDay)
        {
            var offset = ((int)targetDay - (int)startDate.DayOfWeek + 7) % 7;
            return startDate.AddDays(offset).Date;
        }

        private static List<string> ExtractTopics(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return new List<string>();
            }

            return description
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => Regex.Replace(line.Trim(), @"^\d+[\.\)]\s*", string.Empty))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private static string GetMonthNameInGenitive(int month)
        {
            return month switch
            {
                1 => "января",
                2 => "февраля",
                3 => "марта",
                4 => "апреля",
                5 => "мая",
                6 => "июня",
                7 => "июля",
                8 => "августа",
                9 => "сентября",
                10 => "октября",
                11 => "ноября",
                12 => "декабря",
                _ => string.Empty
            };
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }
    }

    public class TimeOptionInfo
    {
        public int HoursPerWeek { get; set; }
        public int WeeksDuration { get; set; }
    }

    public class StudyOptionInfo
    {
        public int HoursPerWeek { get; set; }
        public int WeeksDuration { get; set; }
    }

    internal sealed record ScheduleSlotDefinition(string DayName, DayOfWeek DayOfWeek, TimeSlot TimeSlot);

    internal sealed record LessonOccurrence(DateTime Date, string DayName, TimeSlot TimeSlot);
}
