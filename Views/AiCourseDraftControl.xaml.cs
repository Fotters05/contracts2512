using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Contract2512.Models;
using Contract2512.Services;

namespace Contract2512.Views;

public partial class AiCourseDraftControl : UserControl, IDisposable
{
    private readonly ObservableCollection<AiModuleSeedRow> _moduleRows = new();
    private readonly ObservableCollection<AiStandardProfileCatalogEntry> _standardProfiles = new();
    private readonly ObservableCollection<AiStandardTrackOption> _standardTracks = new();
    private readonly AiCourseDraftClient _client = new();
    private readonly AiServiceHost _serviceHost = new();
    private readonly AiStandardProfileCatalogService _profileCatalogService = new();
    private readonly WordDocumentService _wordDocumentService = new();

    private bool _isInitialized;
    private bool _isBusy;
    private string? _lastDocumentPath;
    private string? _selectedFgosPdfPath;

    public AiCourseDraftControl()
    {
        InitializeComponent();
        ModulesSeedDataGrid.ItemsSource = _moduleRows;
        StandardProfileComboBox.ItemsSource = _standardProfiles;
        StandardTrackComboBox.ItemsSource = _standardTracks;
        BaseUrlTextBlock.Text = $"Подключение: {_client.BaseAddress}";
        OpenDocxButton.IsEnabled = false;
    }

    public void NotifyPanelShown()
    {
        if (_isInitialized)
        {
            _ = TryRefreshHealthAsync(showUnavailableState: true);
        }
    }

    public void Shutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
        _serviceHost.Dispose();
        _client.Dispose();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        ApplyDefaults();
        LoadStandardProfiles();
        EnsureAtLeastOneModuleRow();
        SetEmptyDraftState();
        await TryRefreshHealthAsync(showUnavailableState: true);
    }

    private async void CheckHealthButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Проверяю состояние ai_service...", async () =>
        {
            var health = await EnsureServiceAvailableAsync();
            ApplyHealthState(health);
        });
    }

    private async void GenerateDraftButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Формирую новый черновик курса...", async () =>
        {
            await EnsureServiceAvailableAsync();
            var response = await _client.GenerateDraftAsync(BuildGenerateRequest());
            ApplyDraftResponse(response);
            AiStatusTextBlock.Text = $"Черновик {response.DraftId} успешно сформирован.";
        });
    }

    private async void LoadDraftButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Загружаю черновик по идентификатору...", async () =>
        {
            await EnsureServiceAvailableAsync();
            var response = await _client.GetDraftAsync(RequireDraftId());
            ApplyDraftResponse(response);
            AiStatusTextBlock.Text = $"Черновик {response.DraftId} загружен.";
        });
    }

    private async void ExportDraftButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Формирую DOCX по выбранному черновику...", async () =>
        {
            await EnsureServiceAvailableAsync();
            var response = await _client.ExportDocxAsync(RequireDraftId());
            _lastDocumentPath = response.DocumentPath;
            DocumentPathTextBox.Text = response.DocumentPath;
            OpenDocxButton.IsEnabled = File.Exists(response.DocumentPath);
            DraftStatusTextBlock.Text = $"Документ сформирован для черновика {response.DraftId}.";
            AiStatusTextBlock.Text = $"DOCX успешно подготовлен.";
        });
    }

    private void OpenDocxButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastDocumentPath))
            {
                throw new InvalidOperationException("Сначала сформируйте документ.");
            }

            _wordDocumentService.OpenDocument(_lastDocumentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка открытия DOCX", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddModuleButton_Click(object sender, RoutedEventArgs e)
    {
        _moduleRows.Add(new AiModuleSeedRow
        {
            DesiredHours = 8,
            Summary = "Ключевые темы и ожидаемый результат раздела",
        });
    }

    private void RemoveModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModulesSeedDataGrid.SelectedItem is AiModuleSeedRow selected)
        {
            _moduleRows.Remove(selected);
        }

        EnsureAtLeastOneModuleRow();
    }

    private void RefreshProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        LoadStandardProfiles();
        StandardResolveStatusTextBlock.Text = _standardProfiles.Count == 0
            ? "Профили стандартов не найдены."
            : $"Загружено профилей: {_standardProfiles.Count}.";
    }

    private void ChooseFgosPdfButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выбор PDF ФГОС",
            Filter = "PDF files (*.pdf)|*.pdf",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedFgosPdfPath = dialog.FileName;
            SelectedFgosPdfPathTextBox.Text = dialog.FileName;
            StandardResolveStatusTextBlock.Text = "PDF ФГОС выбран. Можно запускать определение профиля.";
        }
    }

    private async void ResolveFgosPdfButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Определяю профиль ФГОС по PDF...", async () =>
        {
            if (string.IsNullOrWhiteSpace(_selectedFgosPdfPath))
            {
                throw new InvalidOperationException("Сначала выберите PDF-файл ФГОС.");
            }

            var fgosCode = RequireText(FgosCodeTextBox, "Код ФГОС");
            await EnsureServiceAvailableAsync();

            var response = await _client.ResolveStandardPdfAsync(_selectedFgosPdfPath, fgosCode);
            ApplyStandardResolveResponse(response);
            AiStatusTextBlock.Text = "ФГОС успешно разобран, профиль и трек обновлены.";
        });
    }

    private void StandardProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StandardProfileComboBox.SelectedItem is not AiStandardProfileCatalogEntry selectedProfile)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedProfile.TrackId))
        {
            SetTrackOptions(
                new[]
                {
                    new AiStandardTrackOption
                    {
                        TrackId = selectedProfile.TrackId!,
                        QualificationTitle = selectedProfile.QualificationTitle ?? string.Empty,
                    },
                },
                selectedProfile.TrackId);
        }

        StandardResolveStatusTextBlock.Text = $"Выбран профиль: {selectedProfile.ProfileId}";
        StandardDetailsTextBox.Text = BuildProfileDetails(selectedProfile);
    }

    private void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.IsDropDownOpen)
        {
            return;
        }

        comboBox.Focus();
        comboBox.IsDropDownOpen = true;
        e.Handled = true;
    }

    private async Task RunBusyAsync(string statusMessage, Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusyState(true, statusMessage);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AiStatusTextBlock.Text = ex.Message;
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, null);
        }
    }

    private void SetBusyState(bool isBusy, string? statusMessage)
    {
        _isBusy = isBusy;
        CheckHealthButton.IsEnabled = !isBusy;
        GenerateDraftButton.IsEnabled = !isBusy;
        LoadDraftButton.IsEnabled = !isBusy;
        ExportDraftButton.IsEnabled = !isBusy;
        AddModuleButton.IsEnabled = !isBusy;
        RemoveModuleButton.IsEnabled = !isBusy;
        ChooseFgosPdfButton.IsEnabled = !isBusy;
        ResolveFgosPdfButton.IsEnabled = !isBusy;
        RefreshProfilesButton.IsEnabled = !isBusy;
        StandardProfileComboBox.IsEnabled = !isBusy;
        StandardTrackComboBox.IsEnabled = !isBusy;
        OpenDocxButton.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(_lastDocumentPath) && File.Exists(_lastDocumentPath);

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            AiStatusTextBlock.Text = statusMessage;
        }
    }

    private async Task<AiHealthResponse> EnsureServiceAvailableAsync()
    {
        var health = await TryRefreshHealthAsync(showUnavailableState: true);
        if (health != null)
        {
            return health;
        }

        AiStatusTextBlock.Text = "ai_service недоступен. Пытаюсь запустить локальный сервис...";
        await _serviceHost.StartIfNeededAsync(_client.BaseAddress);

        health = await TryRefreshHealthAsync(showUnavailableState: true);
        if (health == null)
        {
            throw new InvalidOperationException("Не удалось подключиться к ai_service.");
        }

        return health;
    }

    private async Task<AiHealthResponse?> TryRefreshHealthAsync(bool showUnavailableState)
    {
        try
        {
            var health = await _client.GetHealthAsync();
            ApplyHealthState(health);
            return health;
        }
        catch (Exception ex)
        {
            if (showUnavailableState)
            {
                ApplyUnavailableHealthState(ex.Message);
            }

            return null;
        }
    }

    private void ApplyHealthState(AiHealthResponse health)
    {
        AiStatusTextBlock.Text = BuildHealthSummary(health);
        ServiceHealthTextBlock.Text = $"Сервис: {health.Service ?? "ai_service"}";
        ServiceHealthTextBlock.Foreground = Brushes.LightGreen;
        SetHealthLine(TemplateHealthTextBlock, "Шаблон DOCX", health.TemplateExists);
        SetHealthLine(OllamaHealthTextBlock, "Ollama", health.OllamaAvailable);
        SetHealthLine(DatabaseHealthTextBlock, "База данных", health.DbAvailable);
    }

    private void ApplyUnavailableHealthState(string message)
    {
        AiStatusTextBlock.Text = $"ai_service недоступен: {message}";
        ServiceHealthTextBlock.Text = "Сервис: недоступен";
        ServiceHealthTextBlock.Foreground = Brushes.OrangeRed;
        TemplateHealthTextBlock.Text = "Шаблон DOCX: неизвестно";
        TemplateHealthTextBlock.Foreground = Brushes.Gainsboro;
        OllamaHealthTextBlock.Text = "Ollama: неизвестно";
        OllamaHealthTextBlock.Foreground = Brushes.Gainsboro;
        DatabaseHealthTextBlock.Text = "База данных: неизвестно";
        DatabaseHealthTextBlock.Foreground = Brushes.Gainsboro;
    }

    private static void SetHealthLine(TextBlock textBlock, string label, bool isAvailable)
    {
        textBlock.Text = $"{label}: {(isAvailable ? "доступно" : "недоступно")}";
        textBlock.Foreground = isAvailable ? Brushes.LightGreen : Brushes.OrangeRed;
    }

    private static string BuildHealthSummary(AiHealthResponse health)
    {
        return $"ai_service доступен. Шаблон: {(health.TemplateExists ? "OK" : "нет")}, Ollama: {(health.OllamaAvailable ? "OK" : "нет")}, БД: {(health.DbAvailable ? "OK" : "нет")}.";
    }

    private void ApplyDraftResponse(AiGenerateDraftResponse response)
    {
        _lastDocumentPath = null;
        OpenDocxButton.IsEnabled = false;
        DocumentPathTextBox.Text = string.Empty;
        DraftIdTextBox.Text = response.DraftId;

        var draft = response.Draft;
        if (draft == null)
        {
            DraftStatusTextBlock.Text = $"Черновик {response.DraftId} получен без содержимого.";
            ProgramCardPreviewTextBox.Text = string.Empty;
            ModulesPreviewTextBox.Text = string.Empty;
            StudyPlanPreviewTextBox.Text = string.Empty;
            return;
        }

        var updatedAtText = draft.DocumentMeta?.UpdatedAt == default
            ? "нет отметки времени"
            : draft.DocumentMeta!.UpdatedAt.ToString("dd.MM.yyyy HH:mm");

        DraftStatusTextBlock.Text = $"Статус: {draft.Status}. Последнее обновление: {updatedAtText}.";
        ProgramCardPreviewTextBox.Text = FormatProgramCard(draft.ProgramCard, draft.DocumentMeta);
        ModulesPreviewTextBox.Text = FormatModules(draft.Modules);
        StudyPlanPreviewTextBox.Text = FormatStudyPlan(draft.StudyPlan);
    }

    private static string FormatProgramCard(AiProgramCard? programCard, AiDocumentMeta? documentMeta)
    {
        if (programCard == null)
        {
            return "Карточка программы пока недоступна.";
        }

        var lines = new List<string>
        {
            $"Название: {programCard.CourseName}",
            $"Тип программы: {programCard.ProgramType}",
            $"Формат: {programCard.Format}",
            $"Часы: {programCard.Hours}",
            $"Вид программы: {programCard.ProgramView}",
            $"Количество занятий: {programCard.LessonsCount}",
            $"Стоимость: {programCard.Price}",
            $"Целевая аудитория: {programCard.TargetAudience}",
            $"Квалификация: {programCard.Qualification}",
            $"Профессиональная область: {programCard.ProfessionalArea}",
            $"Цель: {programCard.TrainingGoal}",
            $"Описание: {programCard.BriefDescription}",
        };

        if (!string.IsNullOrWhiteSpace(programCard.SourceUrl))
        {
            lines.Add($"Источник: {programCard.SourceUrl}");
        }

        if (documentMeta != null)
        {
            lines.Add($"Организация: {documentMeta.OrganizationName}");
            lines.Add($"Город / год: {documentMeta.City}, {documentMeta.DocumentYear}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatModules(IReadOnlyCollection<AiDraftModule>? modules)
    {
        if (modules == null || modules.Count == 0)
        {
            return "Модули пока не сформированы.";
        }

        var builder = new StringBuilder();
        foreach (var module in modules.OrderBy(m => m.Number))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.AppendLine($"{module.Number}. {module.Name} ({module.Hours} ч.)");
            if (!string.IsNullOrWhiteSpace(module.Summary))
            {
                builder.AppendLine(module.Summary);
            }
        }

        return builder.ToString();
    }

    private static string FormatStudyPlan(IReadOnlyCollection<AiStudyPlanEntry>? studyPlan)
    {
        if (studyPlan == null || studyPlan.Count == 0)
        {
            return "Учебный план пока не сформирован.";
        }

        var builder = new StringBuilder();
        foreach (var entry in studyPlan)
        {
            builder.AppendLine($"{entry.Number}. {entry.Name}");
            builder.AppendLine($"Всего: {entry.TotalHours} ч. | Лекции: {entry.Lectures} | Практика: {entry.Practice} | Лабораторные: {entry.Labs} | СРС: {entry.Srs}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private AiCourseSeedRequest BuildGenerateRequest()
    {
        return new AiCourseSeedRequest
        {
            CourseName = RequireText(CourseNameTextBox, "Название курса"),
            ProgramType = RequireText(ProgramTypeTextBox, "Тип программы"),
            Format = RequireText(FormatTextBox, "Формат обучения"),
            Hours = ParsePositiveInt(HoursTextBox, "Часы"),
            TargetAudience = RequireText(TargetAudienceTextBox, "Целевая аудитория"),
            Qualification = RequireText(QualificationTextBox, "Квалификация"),
            ProfessionalArea = RequireText(ProfessionalAreaTextBox, "Профессиональная область"),
            TrainingGoal = RequireText(TrainingGoalTextBox, "Цель обучения"),
            BriefDescription = RequireText(BriefDescriptionTextBox, "Краткое описание"),
            SourceUrl = NormalizeOptional(SourceUrlTextBox.Text),
            PricingMeta = new AiPricingMetaRequest
            {
                Price = RequireText(PriceTextBox, "Стоимость"),
                LessonsCount = ParseNonNegativeInt(LessonsCountTextBox, "Количество занятий"),
                ProgramView = RequireText(ProgramViewTextBox, "Вид программы"),
            },
            Constraints = new AiConstraintsRequest
            {
                Standards = ParseMultilineValues(StandardsTextBox.Text),
                RequiredPhrases = ParseMultilineValues(RequiredPhrasesTextBox.Text),
                StandardProfileId = GetSelectedStandardProfileId(),
                StandardTrackId = GetSelectedStandardTrackId(),
                City = RequireText(CityTextBox, "Город"),
                DocumentYear = ParsePositiveInt(DocumentYearTextBox, "Год документа"),
                OrganizationName = RequireText(OrganizationNameTextBox, "Организация"),
                ApprovalPosition = RequireText(ApprovalPositionTextBox, "Кто утверждает"),
                ApprovalName = RequireText(ApprovalNameTextBox, "ФИО на утверждение"),
                ApprovalDate = RequireText(ApprovalDateTextBox, "Дата утверждения"),
                TeacherName = RequireText(TeacherNameTextBox, "Преподаватель"),
                TeacherPosition = RequireText(TeacherPositionTextBox, "Должность преподавателя"),
                ProgramManagerName = RequireText(ProgramManagerNameTextBox, "Руководитель программы"),
                ProgramManagerPosition = RequireText(ProgramManagerPositionTextBox, "Должность руководителя"),
            },
            ModulesSeed = BuildModuleSeedRequest(),
        };
    }

    private List<AiModuleSeedRequest> BuildModuleSeedRequest()
    {
        var modules = new List<AiModuleSeedRequest>();

        foreach (var row in _moduleRows)
        {
            var isEmpty = string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.Summary) && row.DesiredHours <= 0;
            if (isEmpty)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                throw new InvalidOperationException("У каждого раздела курса должно быть название.");
            }

            if (row.DesiredHours <= 0)
            {
                throw new InvalidOperationException($"У раздела \"{row.Name}\" часы должны быть больше нуля.");
            }

            if (string.IsNullOrWhiteSpace(row.Summary))
            {
                throw new InvalidOperationException($"У раздела \"{row.Name}\" должно быть описание.");
            }

            modules.Add(new AiModuleSeedRequest
            {
                Name = row.Name.Trim(),
                DesiredHours = row.DesiredHours,
                Summary = row.Summary.Trim(),
            });
        }

        if (modules.Count == 0)
        {
            throw new InvalidOperationException("Добавьте хотя бы один раздел курса.");
        }

        return modules;
    }

    private void ApplyDefaults()
    {
        CourseNameTextBox.Text = string.Empty;
        ProgramTypeTextBox.Text = string.Empty;
        FormatTextBox.Text = string.Empty;
        HoursTextBox.Text = string.Empty;
        LessonsCountTextBox.Text = string.Empty;
        ProgramViewTextBox.Text = string.Empty;
        PriceTextBox.Text = string.Empty;
        SourceUrlTextBox.Text = string.Empty;
        TargetAudienceTextBox.Text = string.Empty;
        QualificationTextBox.Text = string.Empty;
        ProfessionalAreaTextBox.Text = string.Empty;
        TrainingGoalTextBox.Text = string.Empty;
        BriefDescriptionTextBox.Text = string.Empty;
        StandardsTextBox.Text = string.Empty;
        RequiredPhrasesTextBox.Text = string.Empty;
        FgosCodeTextBox.Text = string.Empty;
        SelectedFgosPdfPathTextBox.Text = string.Empty;
        CityTextBox.Text = string.Empty;
        DocumentYearTextBox.Text = DateTime.Now.Year.ToString();
        ApprovalPositionTextBox.Text = string.Empty;
        ApprovalDateTextBox.Text = string.Empty;
        TeacherNameTextBox.Text = string.Empty;
        TeacherPositionTextBox.Text = string.Empty;
        ProgramManagerNameTextBox.Text = string.Empty;
        ProgramManagerPositionTextBox.Text = string.Empty;
        OrganizationNameTextBox.Text = string.Empty;
        ApprovalNameTextBox.Text = string.Empty;
        StandardResolveStatusTextBlock.Text = "Можно выбрать профиль вручную или определить его по PDF ФГОС.";
        StandardDetailsTextBox.Text = "Здесь появятся сведения о выбранном профиле стандарта.";
        DetectedCompetenciesTextBox.Text = "Здесь появятся компетенции после загрузки PDF ФГОС.";
        StandardProfileComboBox.SelectedIndex = -1;
        StandardTrackComboBox.SelectedIndex = -1;
        _selectedFgosPdfPath = null;
    }

    private void EnsureAtLeastOneModuleRow()
    {
        if (_moduleRows.Count > 0)
        {
            return;
        }

        _moduleRows.Add(new AiModuleSeedRow
        {
            Name = "Введение в программу",
            DesiredHours = 8,
            Summary = "Коротко опишите, что именно войдет в этот раздел.",
        });
    }

    private void SetEmptyDraftState()
    {
        DraftStatusTextBlock.Text = "Черновик еще не сформирован.";
        ProgramCardPreviewTextBox.Text = "Здесь появится карточка программы после формирования черновика.";
        ModulesPreviewTextBox.Text = "Здесь появятся сформированные разделы курса.";
        StudyPlanPreviewTextBox.Text = "Здесь появится учебный план.";
        StandardDetailsTextBox.Text = "Здесь появятся сведения о выбранном профиле стандарта.";
        DetectedCompetenciesTextBox.Text = "Здесь появятся компетенции после загрузки PDF ФГОС.";
    }

    private void LoadStandardProfiles()
    {
        var selectedId = GetSelectedStandardProfileId();
        _standardProfiles.Clear();

        foreach (var profile in _profileCatalogService.LoadProfiles())
        {
            _standardProfiles.Add(profile);
        }

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var existing = _standardProfiles.FirstOrDefault(item => string.Equals(item.ProfileId, selectedId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                StandardProfileComboBox.SelectedItem = existing;
            }
        }
    }

    private void ApplyStandardResolveResponse(AiStandardResolveResponse response)
    {
        StandardResolveStatusTextBlock.Text = response.Detail;

        if (!string.IsNullOrWhiteSpace(response.FgosCode))
        {
            FgosCodeTextBox.Text = response.FgosCode;
        }

        if (!string.IsNullOrWhiteSpace(response.FgosTitle))
        {
            var standards = ParseMultilineValues(StandardsTextBox.Text);
            if (!standards.Contains(response.FgosTitle))
            {
                standards.Add(response.FgosTitle);
                StandardsTextBox.Text = string.Join(Environment.NewLine, standards);
            }
        }

        if (!string.IsNullOrWhiteSpace(response.QualificationTitle) && string.IsNullOrWhiteSpace(QualificationTextBox.Text))
        {
            QualificationTextBox.Text = response.QualificationTitle;
        }

        var selectedProfile = EnsureProfileSelected(response.StandardProfileId, response.FgosCode, response.FgosTitle, response.ResolvedTrackId, response.QualificationTitle);
        if (selectedProfile != null)
        {
            StandardProfileComboBox.SelectedItem = selectedProfile;
            StandardDetailsTextBox.Text = BuildProfileDetails(selectedProfile, response);
        }
        else
        {
            StandardDetailsTextBox.Text = BuildResolveDetails(response);
        }

        if (response.SupportedTracks.Count > 0)
        {
            SetTrackOptions(response.SupportedTracks, response.ResolvedTrackId);
        }
        else if (!string.IsNullOrWhiteSpace(response.ResolvedTrackId))
        {
            SetTrackOptions(
                new[]
                {
                    new AiStandardTrackOption
                    {
                        TrackId = response.ResolvedTrackId!,
                        QualificationTitle = response.QualificationTitle ?? string.Empty,
                    },
                },
                response.ResolvedTrackId);
        }

        DetectedCompetenciesTextBox.Text = response.DetectedCompetencies.Count == 0
            ? "Компетенции в ответе не обнаружены."
            : string.Join(Environment.NewLine, response.DetectedCompetencies.Select(item => $"- {item}"));
    }

    private AiStandardProfileCatalogEntry? EnsureProfileSelected(string? profileId, string? fgosCode, string? title, string? trackId, string? qualificationTitle)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        var existing = _standardProfiles.FirstOrDefault(item => string.Equals(item.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        var created = new AiStandardProfileCatalogEntry
        {
            ProfileId = profileId,
            FgosCode = fgosCode,
            Title = title,
            TrackId = trackId,
            QualificationTitle = qualificationTitle,
        };

        _standardProfiles.Add(created);
        return created;
    }

    private void SetTrackOptions(IEnumerable<AiStandardTrackOption> tracks, string? selectedTrackId = null)
    {
        _standardTracks.Clear();
        foreach (var track in tracks.Where(item => !string.IsNullOrWhiteSpace(item.TrackId)))
        {
            _standardTracks.Add(track);
        }

        if (_standardTracks.Count == 0)
        {
            StandardTrackComboBox.SelectedIndex = -1;
            return;
        }

        var target = !string.IsNullOrWhiteSpace(selectedTrackId)
            ? _standardTracks.FirstOrDefault(item => string.Equals(item.TrackId, selectedTrackId, StringComparison.OrdinalIgnoreCase))
            : _standardTracks.FirstOrDefault();

        StandardTrackComboBox.SelectedItem = target ?? _standardTracks[0];
    }

    private string? GetSelectedStandardProfileId()
    {
        return StandardProfileComboBox.SelectedItem is AiStandardProfileCatalogEntry selected
            ? NormalizeOptional(selected.ProfileId)
            : null;
    }

    private string? GetSelectedStandardTrackId()
    {
        return StandardTrackComboBox.SelectedItem is AiStandardTrackOption selected
            ? NormalizeOptional(selected.TrackId)
            : null;
    }

    private static string BuildProfileDetails(AiStandardProfileCatalogEntry profile, AiStandardResolveResponse? response = null)
    {
        var lines = new List<string>
        {
            $"standard_profile_id: {profile.ProfileId}",
            $"fgos_code: {profile.FgosCode ?? "-"}",
            $"title: {profile.Title ?? "-"}",
            $"track_id: {profile.TrackId ?? "-"}",
            $"qualification: {profile.QualificationTitle ?? "-"}",
        };

        if (response != null)
        {
            lines.Add($"supported: {(response.Supported ? "yes" : "no")}");
            lines.Add($"detail: {response.Detail}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildResolveDetails(AiStandardResolveResponse response)
    {
        var lines = new List<string>
        {
            $"standard_profile_id: {response.StandardProfileId ?? "-"}",
            $"fgos_code: {response.FgosCode}",
            $"fgos_title: {response.FgosTitle ?? "-"}",
            $"resolved_track_id: {response.ResolvedTrackId ?? "-"}",
            $"qualification: {response.QualificationTitle ?? "-"}",
            $"detail: {response.Detail}",
        };

        return string.Join(Environment.NewLine, lines);
    }

    private string RequireDraftId()
    {
        var draftId = DraftIdTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(draftId))
        {
            throw new InvalidOperationException("Укажите идентификатор черновика.");
        }

        return draftId;
    }

    private static string RequireText(TextBox textBox, string fieldName)
    {
        var value = textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Заполните поле \"{fieldName}\".");
        }

        return value;
    }

    private static int ParsePositiveInt(TextBox textBox, string fieldName)
    {
        if (!int.TryParse(textBox.Text, out var value) || value <= 0)
        {
            throw new InvalidOperationException($"Поле \"{fieldName}\" должно быть положительным числом.");
        }

        return value;
    }

    private static int ParseNonNegativeInt(TextBox textBox, string fieldName)
    {
        if (!int.TryParse(textBox.Text, out var value) || value < 0)
        {
            throw new InvalidOperationException($"Поле \"{fieldName}\" должно быть числом не меньше нуля.");
        }

        return value;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static List<string> ParseMultilineValues(string raw)
    {
        return raw
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}
