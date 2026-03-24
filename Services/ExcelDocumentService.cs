using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Contract2512.Models;

namespace Contract2512.Services
{
    public class ExcelDocumentService
    {
        private const string WorkloadFontName = "Montserrat";
        private const double WorkloadFontSize = 12;
        private const int DateColumn = 1;
        private const int DayOfWeekColumn = 2;
        private const int StartTimeColumn = 3;
        private const int EndTimeColumn = 4;
        private const int HoursColumn = 5;
        private const int ThemeColumn = 6;
        private const int TeacherColumn = 7;

        /// <summary>
        /// Заменяет плейсхолдеры в Excel документе
        /// </summary>
        public void ReplacePlaceholders(string sourceFilePath, string targetFilePath, Dictionary<string, string> replacements)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Файл не найден: {sourceFilePath}");
            }

            if (sourceFilePath != targetFilePath)
            {
                File.Copy(sourceFilePath, targetFilePath, true);
            }

            using (var workbook = new XLWorkbook(targetFilePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    ReplacePlaceholdersInWorksheet(worksheet, replacements);
                }

                workbook.Save();
            }
        }

        /// <summary>
        /// Генерирует документ учебной нагрузки с заполнением таблицы модулями
        /// </summary>
        public void GenerateWorkloadDocument(
            string sourceFilePath,
            string targetFilePath,
            Dictionary<string, string> replacements,
            List<ProgramModule> modules,
            IReadOnlyList<WorkloadScheduleEntry>? scheduledLessons = null)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Файл не найден: {sourceFilePath}");
            }

            if (sourceFilePath != targetFilePath)
            {
                File.Copy(sourceFilePath, targetFilePath, true);
            }

            using (var workbook = new XLWorkbook(targetFilePath))
            {
                foreach (var workbookSheet in workbook.Worksheets)
                {
                    ReplacePlaceholdersInWorksheet(workbookSheet, replacements);
                    ApplyWorksheetFont(workbookSheet);
                }

                var worksheet = workbook.Worksheet(1);
                int tableStartRow = FindTableStartRow(worksheet);
                if (tableStartRow > 0)
                {
                    try
                    {
                        FillModulesTable(worksheet, tableStartRow, modules, GetTeacherName(replacements), scheduledLessons);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Ошибка при заполнении таблицы модулей: {ex.Message}", ex);
                    }
                }

                workbook.Save();
            }
        }

        /// <summary>
        /// Находит первую строку данных сразу под заголовком таблицы
        /// </summary>
        private int FindTableStartRow(IXLWorksheet worksheet)
        {
            for (int row = 1; row <= 50; row++)
            {
                string dateHeader = worksheet.Cell(row, DateColumn).GetString().Trim().ToLowerInvariant();
                string themeHeader = worksheet.Cell(row, ThemeColumn).GetString().Trim().ToLowerInvariant();

                if (dateHeader.Contains("дата") || dateHeader.Contains("date"))
                {
                    return row + 1;
                }

                if (themeHeader.Contains("тема") || themeHeader.Contains("topic"))
                {
                    return row + 1;
                }
            }

            return 10;
        }

        /// <summary>
        /// Заполняет таблицу строками модулей и тем, не затирая футер шаблона
        /// </summary>
        private void FillModulesTable(IXLWorksheet worksheet, int startRow, List<ProgramModule> modules, string teacherName, IReadOnlyList<WorkloadScheduleEntry>? scheduledLessons = null)
        {
            var rows = BuildScheduleRows(modules);
            if (rows.Count == 0)
            {
                return;
            }

            int footerRow = FindFooterRow(worksheet, startRow);
            int availableRows = Math.Max(footerRow - startRow, 1);
            int rowsToInsert = rows.Count - availableRows;
            if (rowsToInsert > 0)
            {
                worksheet.Row(footerRow).InsertRowsAbove(rowsToInsert);
                footerRow += rowsToInsert;
            }

            var moduleTemplateStyle = worksheet.Range(startRow, DateColumn, startRow, TeacherColumn).Style;
            double moduleTemplateHeight = worksheet.Row(startRow).Height;

            int lessonTemplateRow = Math.Min(footerRow, startRow + 1);
            var lessonTemplateStyles = Enumerable.Range(DateColumn, TeacherColumn - DateColumn + 1)
                .Select(column => worksheet.Cell(lessonTemplateRow, column).Style)
                .ToArray();
            double lessonTemplateHeight = worksheet.Row(lessonTemplateRow).Height;

            int currentRow = startRow;
            int lessonIndex = 0;
            
            foreach (var row in rows)
            {
                ClearRow(worksheet, currentRow);

                if (row.IsModuleHeader)
                {
                    var moduleRange = worksheet.Range(currentRow, DateColumn, currentRow, TeacherColumn);
                    moduleRange.Style = moduleTemplateStyle;
                    moduleRange.Merge();
                    moduleRange.Value = row.Theme;
                    ApplyModuleHeaderFormatting(moduleRange);
                    worksheet.Row(currentRow).Height = moduleTemplateHeight;
                }
                else
                {
                    try
                    {
                        for (int column = DateColumn; column <= TeacherColumn; column++)
                        {
                            worksheet.Cell(currentRow, column).Style = lessonTemplateStyles[column - DateColumn];
                        }

                        var scheduledLesson = scheduledLessons != null && lessonIndex < scheduledLessons.Count
                            ? scheduledLessons[lessonIndex]
                            : null;

                        worksheet.Cell(currentRow, DateColumn).Value = scheduledLesson?.LessonDate?.ToString("dd.MM.yyyy") ?? string.Empty;
                        worksheet.Cell(currentRow, DayOfWeekColumn).Value = scheduledLesson?.DayOfWeek ?? string.Empty;
                        worksheet.Cell(currentRow, StartTimeColumn).SetValue(FormatTimeValue(scheduledLesson?.StartTime));
                        worksheet.Cell(currentRow, EndTimeColumn).SetValue(FormatTimeValue(scheduledLesson?.EndTime));
                        worksheet.Cell(currentRow, HoursColumn).Value = scheduledLesson?.Hours ?? row.Hours ?? 1;
                        worksheet.Cell(currentRow, ThemeColumn).Value = row.Theme;
                        worksheet.Cell(currentRow, TeacherColumn).Value = teacherName;
                        ApplyLessonRowFormatting(worksheet.Range(currentRow, DateColumn, currentRow, TeacherColumn));
                        worksheet.Row(currentRow).Height = lessonTemplateHeight;
                        
                        lessonIndex++;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Ошибка заполнения урока {lessonIndex} в строке {currentRow}: {ex.Message}", ex);
                    }
                }

                currentRow++;
            }
        }

        private static string FormatTimeValue(TimeSpan? time)
        {
            if (time == null)
            {
                return string.Empty;
            }

            return $"{time:hh\\:mm}";
        }

        private void ReplacePlaceholdersInWorksheet(IXLWorksheet worksheet, Dictionary<string, string> replacements)
        {
            foreach (var cell in worksheet.CellsUsed())
            {
                if (!cell.Value.IsText)
                {
                    continue;
                }

                string cellValue = cell.Value.GetText();
                foreach (var replacement in replacements)
                {
                    if (cellValue.Contains(replacement.Key))
                    {
                        cellValue = cellValue.Replace(replacement.Key, replacement.Value ?? "");
                    }
                }

                cell.Value = cellValue;
            }
        }

        private void ApplyWorksheetFont(IXLWorksheet worksheet)
        {
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                return;
            }

            usedRange.Style.Font.FontName = WorkloadFontName;
            usedRange.Style.Font.FontSize = WorkloadFontSize;
        }

        private void ApplyModuleHeaderFormatting(IXLRange moduleRange)
        {
            moduleRange.Style.Font.FontName = WorkloadFontName;
            moduleRange.Style.Font.FontSize = WorkloadFontSize;
            moduleRange.Style.Font.Bold = true;
            moduleRange.Style.Font.FontColor = XLColor.Black;
            moduleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            moduleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            moduleRange.Style.Alignment.WrapText = true;
            moduleRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private void ApplyLessonRowFormatting(IXLRange lessonRange)
        {
            lessonRange.Style.Font.FontName = WorkloadFontName;
            lessonRange.Style.Font.FontSize = WorkloadFontSize;
            lessonRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            lessonRange.Cell(1, ThemeColumn - DateColumn + 1).Style.Alignment.WrapText = true;

            foreach (var cell in lessonRange.Cells())
            {
                cell.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }
        }

        private int FindFooterRow(IXLWorksheet worksheet, int startRow)
        {
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? startRow;
            for (int row = startRow; row <= Math.Max(lastRow, startRow + 20); row++)
            {
                string rowText = string.Join(" ",
                    Enumerable.Range(DateColumn, TeacherColumn)
                        .Select(column => worksheet.Cell(row, column).GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value)))
                    .Trim()
                    .ToLowerInvariant();

                if (rowText.Contains("итоговая аттестация") || rowText.Contains("примечания"))
                {
                    return row;
                }
            }

            return startRow + 1;
        }

        private void ClearRow(IXLWorksheet worksheet, int rowNumber)
        {
            var mergedRanges = worksheet.MergedRanges
                .Where(range => range.FirstRow().RowNumber() == rowNumber || range.LastRow().RowNumber() == rowNumber)
                .ToList();

            foreach (var mergedRange in mergedRanges)
            {
                mergedRange.Unmerge();
            }

            worksheet.Range(rowNumber, DateColumn, rowNumber, TeacherColumn).Clear(XLClearOptions.Contents);
        }

        private List<ScheduleRow> BuildScheduleRows(List<ProgramModule> modules)
        {
            var rows = new List<ScheduleRow>();

            foreach (var module in modules.OrderBy(m => m.ModuleNumber))
            {
                rows.Add(new ScheduleRow(true, BuildModuleTitle(module), null));

                foreach (var topic in ExtractTopics(module.Description))
                {
                    rows.Add(new ScheduleRow(false, topic, 1));
                }
            }

            return rows;
        }

        private string BuildModuleTitle(ProgramModule module)
        {
            if (!string.IsNullOrWhiteSpace(module.ModuleName))
            {
                return module.ModuleName.Trim();
            }

            return $"Модуль {module.ModuleNumber}";
        }

        private List<string> ExtractTopics(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return new List<string>();
            }

            return description
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => Regex.Replace(line.Trim(), @"^\d+[\.\)]\s*", ""))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private string GetTeacherName(Dictionary<string, string> replacements)
        {
            if (replacements.TryGetValue("{{Teacher}}", out var teacher) && !string.IsNullOrWhiteSpace(teacher))
            {
                return teacher;
            }

            if (replacements.TryGetValue("{{Teatcher}}", out teacher) && !string.IsNullOrWhiteSpace(teacher))
            {
                return teacher;
            }

            return string.Empty;
        }

        /// <summary>
        /// Открывает Excel документ в приложении по умолчанию
        /// </summary>
        public void OpenDocument(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(processStartInfo);
        }

        public string ConvertToPdf(string excelFilePath, string? pdfFilePath = null)
        {
            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException($"File not found: {excelFilePath}");
            }

            pdfFilePath ??= Path.ChangeExtension(excelFilePath, ".pdf");

            object? excelApp = null;
            object? workbooks = null;
            object? workbook = null;

            try
            {
                var excelType = Type.GetTypeFromProgID("Excel.Application")
                    ?? throw new InvalidOperationException("Microsoft Excel is not available for PDF export.");

                excelApp = Activator.CreateInstance(excelType)
                    ?? throw new InvalidOperationException("Unable to start Microsoft Excel.");

                excelType.InvokeMember("Visible", System.Reflection.BindingFlags.SetProperty, null, excelApp, new object[] { false });
                excelType.InvokeMember("DisplayAlerts", System.Reflection.BindingFlags.SetProperty, null, excelApp, new object[] { false });

                workbooks = excelType.InvokeMember(
                    "Workbooks",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    excelApp,
                    null);

                workbook = workbooks!.GetType().InvokeMember(
                    "Open",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    workbooks,
                    new object[] { excelFilePath });

                ApplyPdfReadableFormatting(workbook);

                workbook!.GetType().InvokeMember(
                    "ExportAsFixedFormat",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    workbook,
                    new object[] { 0, pdfFilePath });

                return pdfFilePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export workload PDF: {ex.Message}", ex);
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        workbook.GetType().InvokeMember(
                            "Close",
                            System.Reflection.BindingFlags.InvokeMethod,
                            null,
                            workbook,
                            new object[] { false });
                    }
                    catch
                    {
                    }
                }

                if (excelApp != null)
                {
                    try
                    {
                        excelApp.GetType().InvokeMember(
                            "Quit",
                            System.Reflection.BindingFlags.InvokeMethod,
                            null,
                            excelApp,
                            null);
                    }
                    catch
                    {
                    }
                }

                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(excelApp);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private static void ReleaseComObject(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }

        private static void ApplyPdfReadableFormatting(object workbook)
        {
            var worksheets = workbook.GetType().InvokeMember(
                "Worksheets",
                System.Reflection.BindingFlags.GetProperty,
                null,
                workbook,
                null);

            if (worksheets == null)
            {
                return;
            }

            try
            {
                int worksheetCount = Convert.ToInt32(
                    worksheets.GetType().InvokeMember(
                        "Count",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        worksheets,
                        null));

                for (int index = 1; index <= worksheetCount; index++)
                {
                    object? worksheet = null;
                    object? usedRange = null;
                    object? font = null;

                    try
                    {
                        worksheet = worksheets.GetType().InvokeMember(
                            "Item",
                            System.Reflection.BindingFlags.GetProperty,
                            null,
                            worksheets,
                            new object[] { index });

                        if (worksheet == null)
                        {
                            continue;
                        }

                        usedRange = worksheet.GetType().InvokeMember(
                            "UsedRange",
                            System.Reflection.BindingFlags.GetProperty,
                            null,
                            worksheet,
                            null);

                        if (usedRange == null)
                        {
                            continue;
                        }

                        font = usedRange.GetType().InvokeMember(
                            "Font",
                            System.Reflection.BindingFlags.GetProperty,
                            null,
                            usedRange,
                            null);

                        if (font == null)
                        {
                            continue;
                        }

                        font.GetType().InvokeMember(
                            "Bold",
                            System.Reflection.BindingFlags.SetProperty,
                            null,
                            font,
                            new object[] { true });

                        font.GetType().InvokeMember(
                            "Color",
                            System.Reflection.BindingFlags.SetProperty,
                            null,
                            font,
                            new object[] { 0 });
                    }
                    finally
                    {
                        ReleaseComObject(font);
                        ReleaseComObject(usedRange);
                        ReleaseComObject(worksheet);
                    }
                }
            }
            finally
            {
                ReleaseComObject(worksheets);
            }
        }

        private sealed record ScheduleRow(bool IsModuleHeader, string Theme, int? Hours);
    }
}
