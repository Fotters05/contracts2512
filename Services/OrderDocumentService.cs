using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Contract2512.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;

namespace Contract2512.Services
{
    public sealed class OrderDocumentService
    {
        public const string AdmissionKey = "admission";
        public const string AdmissionGroupKey = "admission_group";
        public const string ExpulsionKey = "expulsion";
        public const string CommissionCompositionKey = "commission_composition";
        public const string FinalAttestationAdmissionKey = "final_attestation_admission";
        public const string CommissionMeetingProtocolKey = "commission_meeting_protocol";
        public const string StatementKey = "statement";

        private readonly WordDocumentService _wordDocumentService = new();

        private static readonly IReadOnlyDictionary<string, string> DefaultTemplatePaths = new Dictionary<string, string>
        {
            [AdmissionKey] = @"C:\Dogovora\Приказы\Пример приказа о зачислении.docx",
            [AdmissionGroupKey] = @"C:\Dogovora\Приказы\Пример приказа о зачислении группа.docx",
            [ExpulsionKey] = @"C:\Dogovora\Приказы\Пример приказа об отчислении.docx",
            [CommissionCompositionKey] = @"C:\Dogovora\Приказы\Пример приказа на состав комиссии.docx",
            [FinalAttestationAdmissionKey] = @"C:\Dogovora\Приказы\Пример приказа о допуске итоговой аттестации.docx",
            [CommissionMeetingProtocolKey] = @"C:\Dogovora\Приказы\Пример протокола заседании комиссии.docx",
            [StatementKey] = @"C:\Dogovora\Приказы\Пример ведомости.docx"
        };

        private static readonly IReadOnlyDictionary<string, string> OrderNames = new Dictionary<string, string>
        {
            [AdmissionKey] = "О зачислении",
            [AdmissionGroupKey] = "О зачислении группа",
            [ExpulsionKey] = "Об отчислении",
            [CommissionCompositionKey] = "На состав комиссии",
            [FinalAttestationAdmissionKey] = "О допуске итоговой аттестации",
            [CommissionMeetingProtocolKey] = "О заседании комиссии",
            [StatementKey] = "Ведомости"
        };

        public IReadOnlyList<OrderTemplate> GetTemplates()
        {
            using var db = new AppDbContext();
            return db.OrderTemplates
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToList();
        }

        public List<OrderDocument> GetGeneratedDocuments()
        {
            using var db = new AppDbContext();
            return db.OrderDocuments
                .AsNoTracking()
                .Include(d => d.Program)
                .Include(d => d.Listener)
                .Include(d => d.Teacher)
                .OrderByDescending(d => d.GeneratedAt)
                .ToList();
        }

        public string GenerateDocument(OrderGenerationRequest request)
        {
            using var db = new AppDbContext();

            var templatePath = ResolveTemplatePath(db, request.OrderTypeKey);
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Шаблон приказа не найден: {templatePath}");
            }

            var outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Приказы");

            Directory.CreateDirectory(outputDirectory);

            var timestamp = DateTime.Now;
            var outputFileName = BuildOutputFileName(request, timestamp);
            var outputPath = Path.Combine(outputDirectory, outputFileName);

            File.Copy(templatePath, outputPath, true);

            if (request.TableRows.Count > 0)
            {
                ExpandTableRows(outputPath, request.TableRows);
            }

            _wordDocumentService.ReplacePlaceholders(outputPath, outputPath, request.Placeholders);

            try
            {
                var pdfPath = Path.ChangeExtension(outputPath, ".pdf");
                _wordDocumentService.ConvertToPdf(outputPath, pdfPath);
            }
            catch
            {
                // PDF is optional.
            }

            var document = new OrderDocument
            {
                OrderTypeKey = request.OrderTypeKey,
                OrderName = request.OrderName,
                ProgramId = request.ProgramId,
                ContractId = request.ContractId,
                ListenerId = request.ListenerId,
                TeacherId = request.TeacherId,
                DocumentNumber = request.DocumentNumber,
                FileName = Path.GetFileName(outputPath),
                FilePath = outputPath,
                MetadataJson = request.Metadata is null ? null : JsonSerializer.Serialize(request.Metadata),
                GeneratedAt = timestamp,
                CreatedAt = timestamp
            };

            db.OrderDocuments.Add(document);
            db.SaveChanges();

            SaveApplications(db, document, request.ApplicationEntries);

            return outputPath;
        }

        public void OpenDocument(string filePath)
        {
            _wordDocumentService.OpenDocument(filePath);
        }

        public static string GetOrderName(string orderTypeKey)
        {
            return OrderNames.TryGetValue(orderTypeKey, out var name) ? name : orderTypeKey;
        }

        public static IReadOnlyDictionary<string, string> GetDefaultTemplates() => DefaultTemplatePaths;

        private static string ResolveTemplatePath(AppDbContext db, string orderTypeKey)
        {
            var fromDb = db.OrderTemplates
                .AsNoTracking()
                .Where(t => t.OrderTypeKey == orderTypeKey)
                .Select(t => t.FilePath)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(fromDb))
            {
                return fromDb;
            }

            return DefaultTemplatePaths.TryGetValue(orderTypeKey, out var fallbackPath)
                ? fallbackPath
                : string.Empty;
        }

        private static string BuildOutputFileName(OrderGenerationRequest request, DateTime timestamp)
        {
            var baseName = $"{request.OrderName}_{timestamp:yyyyMMdd_HHmmss}";
            if (!string.IsNullOrWhiteSpace(request.DocumentNumber))
            {
                baseName += $"_{request.DocumentNumber}";
            }

            if (!string.IsNullOrWhiteSpace(request.SubjectName))
            {
                baseName += $"_{request.SubjectName}";
            }

            var sanitized = string.Concat(baseName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            return $"{sanitized}.docx";
        }

        private static void SaveApplications(
            AppDbContext db,
            OrderDocument document,
            IReadOnlyList<ApplicationSaveRequest> applicationEntries)
        {
            if (applicationEntries.Count == 0)
            {
                return;
            }

            foreach (var entry in applicationEntries)
            {
                var existing = db.ListenerApplications
                    .FirstOrDefault(a =>
                        a.ContractId == entry.ContractId &&
                        a.ApplicationTypeKey == entry.ApplicationTypeKey);

                if (existing == null)
                {
                    db.ListenerApplications.Add(new ListenerApplication
                    {
                        ApplicationTypeKey = entry.ApplicationTypeKey,
                        ContractId = entry.ContractId,
                        ListenerId = entry.ListenerId,
                        ProgramId = entry.ProgramId,
                        OrderDocumentId = document.Id,
                        ApplicationNumber = entry.ApplicationNumber,
                        ApplicationDate = entry.ApplicationDate.Date,
                        Notes = entry.Notes,
                        CreatedAt = document.GeneratedAt,
                        UpdatedAt = document.GeneratedAt
                    });

                    continue;
                }

                existing.ListenerId = entry.ListenerId;
                existing.ProgramId = entry.ProgramId;
                existing.OrderDocumentId = document.Id;
                existing.ApplicationNumber = entry.ApplicationNumber;
                existing.ApplicationDate = entry.ApplicationDate.Date;
                existing.Notes = entry.Notes;
                existing.UpdatedAt = document.GeneratedAt;
            }

            db.SaveChanges();
        }

        private static void ExpandTableRows(string filePath, IReadOnlyList<TableExpansionRequest> expansions)
        {
            using var document = WordprocessingDocument.Open(filePath, true);
            var mainPart = document.MainDocumentPart;
            if (mainPart?.Document is null)
            {
                throw new InvalidOperationException("Не удалось открыть шаблон приказа.");
            }

            foreach (var expansion in expansions)
            {
                var templateRow = mainPart.Document
                    .Descendants<TableRow>()
                    .FirstOrDefault(row => GetElementText(row).Contains(expansion.AnchorPlaceholder, StringComparison.Ordinal));

                if (templateRow is null)
                {
                    continue;
                }

                foreach (var rowValues in expansion.Rows)
                {
                    var rowClone = (TableRow)templateRow.CloneNode(true);
                    ReplaceInElement(rowClone, rowValues);
                    templateRow.InsertBeforeSelf(rowClone);
                }

                templateRow.Remove();
            }

            mainPart.Document.Save();
        }

        private static string GetElementText(OpenXmlElement element)
        {
            return string.Concat(element.Descendants<Text>().Select(t => t.Text));
        }

        private static void ReplaceInElement(OpenXmlElement element, Dictionary<string, string> replacements)
        {
            if (element == null)
            {
                return;
            }

            foreach (var paragraph in element.Descendants<Paragraph>().ToList())
            {
                ReplaceInParagraph(paragraph, replacements);
            }
        }

        private static void ReplaceInParagraph(Paragraph paragraph, Dictionary<string, string> replacements)
        {
            var textElements = paragraph.Descendants<Text>().ToList();
            if (textElements.Count == 0)
            {
                return;
            }

            var fullText = string.Concat(textElements.Select(t => t.Text));
            if (string.IsNullOrEmpty(fullText))
            {
                return;
            }

            foreach (var replacement in replacements)
            {
                if (!fullText.Contains(replacement.Key, StringComparison.Ordinal))
                {
                    continue;
                }

                var placeholderIndex = fullText.IndexOf(replacement.Key, StringComparison.Ordinal);
                while (placeholderIndex >= 0)
                {
                    var placeholderLength = replacement.Key.Length;
                    var replacementValue = replacement.Value ?? string.Empty;

                    var currentPosition = 0;
                    var startTextIndex = -1;
                    var startOffset = 0;
                    var endTextIndex = -1;
                    var endOffset = 0;

                    for (var i = 0; i < textElements.Count; i++)
                    {
                        var textLength = textElements[i].Text.Length;

                        if (startTextIndex == -1 && currentPosition + textLength > placeholderIndex)
                        {
                            startTextIndex = i;
                            startOffset = placeholderIndex - currentPosition;
                        }

                        if (currentPosition + textLength >= placeholderIndex + placeholderLength)
                        {
                            endTextIndex = i;
                            endOffset = placeholderIndex + placeholderLength - currentPosition;
                            break;
                        }

                        currentPosition += textLength;
                    }

                    if (startTextIndex == -1 || endTextIndex == -1)
                    {
                        break;
                    }

                    if (startTextIndex == endTextIndex)
                    {
                        var text = textElements[startTextIndex].Text;
                        textElements[startTextIndex].Text =
                            text[..startOffset] + replacementValue + text[endOffset..];
                        textElements[startTextIndex].Space = SpaceProcessingModeValues.Preserve;
                    }
                    else
                    {
                        var firstText = textElements[startTextIndex].Text;
                        textElements[startTextIndex].Text = firstText[..startOffset] + replacementValue;
                        textElements[startTextIndex].Space = SpaceProcessingModeValues.Preserve;

                        for (var i = startTextIndex + 1; i < endTextIndex; i++)
                        {
                            textElements[i].Text = string.Empty;
                        }

                        var lastText = textElements[endTextIndex].Text;
                        textElements[endTextIndex].Text = lastText[endOffset..];
                    }

                    fullText = string.Concat(textElements.Select(t => t.Text));
                    placeholderIndex = fullText.IndexOf(replacement.Key, StringComparison.Ordinal);
                }
            }
        }
    }

    public sealed class OrderGenerationRequest
    {
        public string OrderTypeKey { get; init; } = string.Empty;
        public string OrderName { get; init; } = string.Empty;
        public long? ProgramId { get; init; }
        public long? ContractId { get; init; }
        public long? ListenerId { get; init; }
        public long? TeacherId { get; init; }
        public string? DocumentNumber { get; init; }
        public string? SubjectName { get; init; }
        public Dictionary<string, string> Placeholders { get; init; } = new();
        public List<TableExpansionRequest> TableRows { get; init; } = new();
        public List<ApplicationSaveRequest> ApplicationEntries { get; init; } = new();
        public object? Metadata { get; init; }
    }

    public sealed class TableExpansionRequest
    {
        public string AnchorPlaceholder { get; init; } = string.Empty;
        public List<Dictionary<string, string>> Rows { get; init; } = new();
    }

    public sealed class ApplicationSaveRequest
    {
        public string ApplicationTypeKey { get; init; } = string.Empty;
        public long ContractId { get; init; }
        public long ListenerId { get; init; }
        public long ProgramId { get; init; }
        public DateTime ApplicationDate { get; init; }
        public string? ApplicationNumber { get; init; }
        public string? Notes { get; init; }
    }
}
