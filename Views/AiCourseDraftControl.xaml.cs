using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Contract2512.Models;
using Contract2512.Services;

namespace Contract2512.Views;

public partial class AiCourseDraftControl : UserControl, IDisposable
{
    private readonly AiCourseDraftClient _client = new();
    private readonly AiServiceHost _serviceHost = new();
    private readonly WordDocumentService _wordDocumentService = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private bool _isBusy;
    private bool _isInitialized;
    private string? _lastDocumentPath;

    public AiCourseDraftControl()
    {
        InitializeComponent();
        BaseUrlTextBlock.Text = $"Base URL: {_client.BaseAddress}";
        OpenDocxButton.IsEnabled = false;
    }

    public void NotifyPanelShown()
    {
        if (_isInitialized)
        {
            _ = RefreshHealthAsync();
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
        LoadExamplePayload(force: false);
        DraftSummaryTextBox.Text = "Черновик еще не получен.";
        LastResponseTextBox.Text = "Здесь будет показан последний ответ ai_service.";
        HealthResponseTextBox.Text = "Пока пусто.";
        await RefreshHealthAsync();
    }

    private void LoadExampleButton_Click(object sender, RoutedEventArgs e)
    {
        LoadExamplePayload(force: true);
        AiStatusTextBlock.Text = "В форму подставлен пример JSON для POST /api/course-drafts/generate.";
    }

    private async void CheckHealthButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Проверяю GET /api/health...", RefreshHealthAsync);
    }

    private async void GenerateDraftButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Отправляю POST /api/course-drafts/generate...", async () =>
        {
            await EnsureServiceAvailableAsync();
            var request = ParseGenerateRequest();
            var response = await _client.GenerateDraftAsync(request);

            DraftIdTextBox.Text = response.DraftId;
            _lastDocumentPath = null;
            DocumentPathTextBox.Text = string.Empty;
            OpenDocxButton.IsEnabled = false;

            LastResponseTextBox.Text = JsonSerializer.Serialize(response, _jsonOptions);
            DraftSummaryTextBox.Text = BuildDraftSummary(response);
            AiStatusTextBlock.Text = $"Черновик {response.DraftId} успешно создан.";
        });
    }

    private async void LoadDraftButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Загружаю GET /api/course-drafts/{draft_id}...", async () =>
        {
            await EnsureServiceAvailableAsync();
            var draftId = RequireDraftId();
            var response = await _client.GetDraftAsync(draftId);

            LastResponseTextBox.Text = JsonSerializer.Serialize(response, _jsonOptions);
            DraftSummaryTextBox.Text = BuildDraftSummary(response);
            AiStatusTextBlock.Text = $"Черновик {response.DraftId} загружен из ai_service.";
        });
    }

    private async void ExportDraftButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Отправляю POST /api/course-drafts/{draft_id}/export-docx...", async () =>
        {
            await EnsureServiceAvailableAsync();
            var draftId = RequireDraftId();
            var response = await _client.ExportDocxAsync(draftId);

            _lastDocumentPath = response.DocumentPath;
            DocumentPathTextBox.Text = response.DocumentPath;
            OpenDocxButton.IsEnabled = File.Exists(response.DocumentPath);
            LastResponseTextBox.Text = JsonSerializer.Serialize(response, _jsonOptions);
            AiStatusTextBlock.Text = $"DOCX для {response.DraftId} сформирован.";
        });
    }

    private void OpenDocxButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastDocumentPath))
            {
                throw new InvalidOperationException("Сначала сформируйте DOCX через API.");
            }

            if (!File.Exists(_lastDocumentPath))
            {
                throw new FileNotFoundException("Файл DOCX не найден.", _lastDocumentPath);
            }

            _wordDocumentService.OpenDocument(_lastDocumentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Ошибка открытия DOCX",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task RefreshHealthAsync()
    {
        try
        {
            var health = await _client.GetHealthAsync();
            HealthSummaryTextBlock.Text = BuildHealthSummary(health);
            HealthResponseTextBox.Text = JsonSerializer.Serialize(health, _jsonOptions);
            AiStatusTextBlock.Text = "ai_service доступен и готов к запросам.";
        }
        catch (Exception)
        {
            var health = await EnsureServiceAvailableAsync();
            HealthSummaryTextBlock.Text = BuildHealthSummary(health);
            HealthResponseTextBox.Text = JsonSerializer.Serialize(health, _jsonOptions);
            AiStatusTextBlock.Text = "ai_service был недоступен, но соединение восстановлено.";
        }
    }

    private async Task<AiHealthResponse> EnsureServiceAvailableAsync()
    {
        try
        {
            return await _client.GetHealthAsync();
        }
        catch
        {
            AiStatusTextBlock.Text = "ai_service не ответил. Пробую локальный запуск сервиса...";
            await _serviceHost.StartIfNeededAsync(_client.BaseAddress);
            return await _client.GetHealthAsync();
        }
    }

    private async Task RunBusyAsync(string statusText, Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusyState(true, statusText);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AiStatusTextBlock.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                "Ошибка ai_service",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, null);
        }
    }

    private void SetBusyState(bool isBusy, string? statusText)
    {
        _isBusy = isBusy;

        CheckHealthButton.IsEnabled = !isBusy;
        LoadExampleButton.IsEnabled = !isBusy;
        GenerateDraftButton.IsEnabled = !isBusy;
        LoadDraftButton.IsEnabled = !isBusy;
        ExportDraftButton.IsEnabled = !isBusy;
        OpenDocxButton.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(_lastDocumentPath) && File.Exists(_lastDocumentPath);

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            AiStatusTextBlock.Text = statusText;
        }
    }

    private void LoadExamplePayload(bool force)
    {
        if (!force && !string.IsNullOrWhiteSpace(GenerateRequestTextBox.Text))
        {
            return;
        }

        GenerateRequestTextBox.Text = JsonSerializer.Serialize(CreateExampleRequest(), _jsonOptions);
    }

    private AiCourseSeedRequest CreateExampleRequest()
    {
        var request = new AiCourseSeedRequest
        {
            CourseName = "Инженерия промптов для команд",
            ProgramType = "Дополнительная профессиональная программа повышения квалификации",
            Format = "Очно-заочная",
            Hours = 72,
            TargetAudience = "Руководители команд, аналитики, разработчики и специалисты, работающие с ИИ-инструментами",
            Qualification = "Специалист по внедрению ИИ-практик",
            ProfessionalArea = "Автоматизация рабочих процессов и использование генеративного ИИ в бизнесе",
            TrainingGoal = "Научить команду безопасно и системно применять генеративный ИИ в рабочих сценариях",
            BriefDescription = "Практический курс по внедрению prompt engineering, шаблонов запросов и контроля качества ответов ИИ.",
            SourceUrl = "https://example.com/ai-course",
            PricingMeta = new AiPricingMetaRequest
            {
                Price = "45000",
                LessonsCount = 12,
                ProgramView = "Повышение квалификации",
            },
            Constraints = new AiConstraintsRequest
            {
                Standards = { "Приказ Минобрнауки РФ" },
                RequiredPhrases = { "генеративный искусственный интеллект", "практические занятия" },
                City = "Москва",
                DocumentYear = DateTime.Now.Year,
                OrganizationName = "ООО Ромашка",
                ApprovalPosition = "Генеральный директор",
                ApprovalName = "Иванов И.И.",
                ApprovalDate = $"«___» ____________ {DateTime.Now.Year} г.",
                TeacherName = "Петров П.П.",
                TeacherPosition = "Преподаватель",
                ProgramManagerName = "Сидоров С.С.",
                ProgramManagerPosition = "Руководитель направления дополнительного профессионального образования",
            },
            ModulesSeed =
            {
                new AiModuleSeedRequest
                {
                    Name = "Основы prompt engineering",
                    DesiredHours = 24,
                    Summary = "Типы промптов, структура качественного запроса, шаблоны и типовые ошибки.",
                },
                new AiModuleSeedRequest
                {
                    Name = "Интеграция ИИ в бизнес-процессы",
                    DesiredHours = 24,
                    Summary = "Практические сценарии использования ИИ в аналитике, поддержке и создании контента.",
                },
                new AiModuleSeedRequest
                {
                    Name = "Контроль качества и безопасность",
                    DesiredHours = 24,
                    Summary = "Проверка фактов, минимизация рисков, защита данных и регламенты работы с ИИ.",
                },
            },
        };

        try
        {
            using var db = new AppDbContext();
            var organization = db.Organizations.FirstOrDefault();
            if (organization != null)
            {
                request.Constraints.OrganizationName = organization.OrganizationName;
                request.Constraints.ApprovalName = organization.DirectorFio;
            }
        }
        catch
        {
            // Ignore local DB issues and keep example defaults.
        }

        return request;
    }

    private AiCourseSeedRequest ParseGenerateRequest()
    {
        if (string.IsNullOrWhiteSpace(GenerateRequestTextBox.Text))
        {
            throw new InvalidOperationException("Заполни JSON для POST /api/course-drafts/generate.");
        }

        var request = JsonSerializer.Deserialize<AiCourseSeedRequest>(GenerateRequestTextBox.Text, _jsonOptions);
        if (request == null)
        {
            throw new InvalidOperationException("Не удалось разобрать JSON запроса.");
        }

        if (request.ModulesSeed.Count == 0)
        {
            throw new InvalidOperationException("В modules_seed должен быть хотя бы один модуль.");
        }

        return request;
    }

    private string RequireDraftId()
    {
        var draftId = DraftIdTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(draftId))
        {
            throw new InvalidOperationException("Укажи draft_id.");
        }

        return draftId;
    }

    private static string BuildHealthSummary(AiHealthResponse health)
    {
        return
            $"Сервис: {health.Service ?? "ai_service"}{Environment.NewLine}" +
            $"Шаблон DOCX: {(health.TemplateExists ? "доступен" : "недоступен")}{Environment.NewLine}" +
            $"Ollama: {(health.OllamaAvailable ? "доступна" : "недоступна")}{Environment.NewLine}" +
            $"База данных: {(health.DbAvailable ? "доступна" : "недоступна")}";
    }

    private static string BuildDraftSummary(AiGenerateDraftResponse response)
    {
        if (response.Draft == null)
        {
            return $"draft_id: {response.DraftId}{Environment.NewLine}Тело draft отсутствует.";
        }

        var draft = response.Draft;
        var builder = new StringBuilder();
        builder.AppendLine($"draft_id: {response.DraftId}");
        builder.AppendLine($"status: {draft.Status}");

        if (draft.ProgramCard != null)
        {
            builder.AppendLine($"course_name: {draft.ProgramCard.CourseName}");
            builder.AppendLine($"hours: {draft.ProgramCard.Hours}");
            builder.AppendLine($"program_type: {draft.ProgramCard.ProgramType}");
        }

        builder.AppendLine($"modules: {draft.Modules.Count}");
        builder.AppendLine($"study_plan_rows: {draft.StudyPlan.Count}");

        if (draft.DocumentMeta != null)
        {
            builder.AppendLine($"organization: {draft.DocumentMeta.OrganizationName}");
            builder.AppendLine($"updated_at: {draft.DocumentMeta.UpdatedAt:dd.MM.yyyy HH:mm}");
        }

        return builder.ToString().TrimEnd();
    }
}
