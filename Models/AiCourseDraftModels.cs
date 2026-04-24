using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Contract2512.Models;

public sealed class AiCourseSeedRequest
{
    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("program_type")]
    public string ProgramType { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("hours")]
    public int Hours { get; set; }

    [JsonPropertyName("target_audience")]
    public string TargetAudience { get; set; } = string.Empty;

    [JsonPropertyName("qualification")]
    public string Qualification { get; set; } = string.Empty;

    [JsonPropertyName("professional_area")]
    public string ProfessionalArea { get; set; } = string.Empty;

    [JsonPropertyName("training_goal")]
    public string TrainingGoal { get; set; } = string.Empty;

    [JsonPropertyName("brief_description")]
    public string BriefDescription { get; set; } = string.Empty;

    [JsonPropertyName("modules_seed")]
    public List<AiModuleSeedRequest> ModulesSeed { get; set; } = new();

    [JsonPropertyName("constraints")]
    public AiConstraintsRequest Constraints { get; set; } = new();

    [JsonPropertyName("pricing_meta")]
    public AiPricingMetaRequest PricingMeta { get; set; } = new();

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }
}

public sealed class AiModuleSeedRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("desired_hours")]
    public int DesiredHours { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public sealed class AiConstraintsRequest
{
    [JsonPropertyName("standards")]
    public List<string> Standards { get; set; } = new();

    [JsonPropertyName("required_phrases")]
    public List<string> RequiredPhrases { get; set; } = new();

    [JsonPropertyName("standard_profile_id")]
    public string? StandardProfileId { get; set; }

    [JsonPropertyName("standard_track_id")]
    public string? StandardTrackId { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("document_year")]
    public int DocumentYear { get; set; }

    [JsonPropertyName("organization_name")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("approval_position")]
    public string ApprovalPosition { get; set; } = string.Empty;

    [JsonPropertyName("approval_name")]
    public string ApprovalName { get; set; } = string.Empty;

    [JsonPropertyName("approval_date")]
    public string ApprovalDate { get; set; } = string.Empty;

    [JsonPropertyName("teacher_name")]
    public string TeacherName { get; set; } = string.Empty;

    [JsonPropertyName("teacher_position")]
    public string TeacherPosition { get; set; } = string.Empty;

    [JsonPropertyName("program_manager_name")]
    public string ProgramManagerName { get; set; } = string.Empty;

    [JsonPropertyName("program_manager_position")]
    public string ProgramManagerPosition { get; set; } = string.Empty;
}

public sealed class AiPricingMetaRequest
{
    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("lessons_count")]
    public int LessonsCount { get; set; }

    [JsonPropertyName("program_view")]
    public string ProgramView { get; set; } = string.Empty;
}

public sealed class AiHealthResponse
{
    [JsonPropertyName("service")]
    public string? Service { get; set; }

    [JsonPropertyName("template_exists")]
    public bool TemplateExists { get; set; }

    [JsonPropertyName("ollama_available")]
    public bool OllamaAvailable { get; set; }

    [JsonPropertyName("db_available")]
    public bool DbAvailable { get; set; }
}

public sealed class AiGenerateDraftResponse
{
    [JsonPropertyName("draft_id")]
    public string DraftId { get; set; } = string.Empty;

    [JsonPropertyName("draft")]
    public AiCourseDraft? Draft { get; set; }
}

public sealed class AiDocumentExportResponse
{
    [JsonPropertyName("draft_id")]
    public string DraftId { get; set; } = string.Empty;

    [JsonPropertyName("document_path")]
    public string DocumentPath { get; set; } = string.Empty;
}

public sealed class AiCourseDraft
{
    [JsonPropertyName("draft_id")]
    public string DraftId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("program_card")]
    public AiProgramCard? ProgramCard { get; set; }

    [JsonPropertyName("modules")]
    public List<AiDraftModule> Modules { get; set; } = new();

    [JsonPropertyName("study_plan")]
    public List<AiStudyPlanEntry> StudyPlan { get; set; } = new();

    [JsonPropertyName("document_meta")]
    public AiDocumentMeta? DocumentMeta { get; set; }
}

public sealed class AiProgramCard
{
    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("course_name_upper")]
    public string CourseNameUpper { get; set; } = string.Empty;

    [JsonPropertyName("program_type")]
    public string ProgramType { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("hours")]
    public int Hours { get; set; }

    [JsonPropertyName("target_audience")]
    public string TargetAudience { get; set; } = string.Empty;

    [JsonPropertyName("qualification")]
    public string Qualification { get; set; } = string.Empty;

    [JsonPropertyName("professional_area")]
    public string ProfessionalArea { get; set; } = string.Empty;

    [JsonPropertyName("training_goal")]
    public string TrainingGoal { get; set; } = string.Empty;

    [JsonPropertyName("brief_description")]
    public string BriefDescription { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("lessons_count")]
    public int LessonsCount { get; set; }

    [JsonPropertyName("program_view")]
    public string ProgramView { get; set; } = string.Empty;

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }
}

public sealed class AiDraftModule
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("hours")]
    public int Hours { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("theme_titles")]
    public List<string> ThemeTitles { get; set; } = new();
}

public sealed class AiStudyPlanEntry
{
    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("total_hours")]
    public int TotalHours { get; set; }

    [JsonPropertyName("distance_total")]
    public int DistanceTotal { get; set; }

    [JsonPropertyName("lectures")]
    public int Lectures { get; set; }

    [JsonPropertyName("labs")]
    public int Labs { get; set; }

    [JsonPropertyName("practice")]
    public int Practice { get; set; }

    [JsonPropertyName("srs")]
    public int Srs { get; set; }
}

public sealed class AiDocumentMeta
{
    [JsonPropertyName("organization_name")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("approval_position")]
    public string ApprovalPosition { get; set; } = string.Empty;

    [JsonPropertyName("approval_name")]
    public string ApprovalName { get; set; } = string.Empty;

    [JsonPropertyName("approval_date")]
    public string ApprovalDate { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("document_year")]
    public int DocumentYear { get; set; }

    [JsonPropertyName("template_name")]
    public string TemplateName { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }
}

public sealed class AiModuleSeedRow
{
    public string Name { get; set; } = string.Empty;

    public int DesiredHours { get; set; } = 8;

    public string Summary { get; set; } = string.Empty;
}

public sealed class AiStandardResolveResponse
{
    [JsonPropertyName("supported")]
    public bool Supported { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("fgos_code")]
    public string FgosCode { get; set; } = string.Empty;

    [JsonPropertyName("standard_profile_id")]
    public string? StandardProfileId { get; set; }

    [JsonPropertyName("fgos_title")]
    public string? FgosTitle { get; set; }

    [JsonPropertyName("resolved_track_id")]
    public string? ResolvedTrackId { get; set; }

    [JsonPropertyName("qualification_title")]
    public string? QualificationTitle { get; set; }

    [JsonPropertyName("supported_tracks")]
    public List<AiStandardTrackOption> SupportedTracks { get; set; } = new();

    [JsonPropertyName("detected_competencies")]
    public List<string> DetectedCompetencies { get; set; } = new();
}

public sealed class AiStandardTrackOption
{
    [JsonPropertyName("track_id")]
    public string TrackId { get; set; } = string.Empty;

    [JsonPropertyName("qualification_title")]
    public string QualificationTitle { get; set; } = string.Empty;

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(QualificationTitle)
            ? TrackId
            : $"{TrackId} - {QualificationTitle}";
    }
}

public sealed class AiStandardProfileCatalogEntry
{
    [JsonPropertyName("profile_id")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("fgos_code")]
    public string? FgosCode { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("track_id")]
    public string? TrackId { get; set; }

    [JsonPropertyName("qualification_title")]
    public string? QualificationTitle { get; set; }

    public string DisplayName
    {
        get
        {
            var code = string.IsNullOrWhiteSpace(FgosCode) ? "без кода" : FgosCode;
            var title = string.IsNullOrWhiteSpace(Title) ? ProfileId : Title;
            return $"{code} - {title} ({ProfileId})";
        }
    }

    public override string ToString() => DisplayName;
}
