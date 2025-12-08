using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Contract2512.Services
{
    public class WordDocumentService
    {
        /// <summary>
        /// Заменяет плейсхолдеры в Word документе с сохранением форматирования
        /// </summary>
        /// <param name="sourceFilePath">Путь к исходному файлу</param>
        /// <param name="targetFilePath">Путь к результирующему файлу</param>
        /// <param name="replacements">Словарь замен: ключ - плейсхолдер, значение - текст для замены</param>
        public void ReplacePlaceholders(string sourceFilePath, string targetFilePath, Dictionary<string, string> replacements)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Файл не найден: {sourceFilePath}");
            }

            // Копируем исходный файл
            File.Copy(sourceFilePath, targetFilePath, true);

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(targetFilePath, true))
            {
                // Получаем основной документ
                MainDocumentPart mainPart = wordDoc.MainDocumentPart;
                if (mainPart == null)
                {
                    throw new InvalidOperationException("Не удалось открыть основной документ");
                }

                // Заменяем в основном тексте
                ReplaceInElement(mainPart.Document, replacements);

                // Заменяем в заголовках
                foreach (HeaderPart headerPart in mainPart.HeaderParts)
                {
                    ReplaceInElement(headerPart.Header, replacements);
                }

                // Заменяем в колонтитулах
                foreach (FooterPart footerPart in mainPart.FooterParts)
                {
                    ReplaceInElement(footerPart.Footer, replacements);
                }

                mainPart.Document.Save();
            }
        }

        /// <summary>
        /// Заменяет плейсхолдеры в элементе документа
        /// </summary>
        private void ReplaceInElement(OpenXmlElement element, Dictionary<string, string> replacements)
        {
            if (element == null) return;

            // Получаем все текстовые элементы
            var allTextElements = element.Descendants<Text>().ToList();
            
            // Собираем весь текст документа
            string fullDocumentText = string.Join("", allTextElements.Select(t => t.Text));

            // Проверяем наличие плейсхолдеров
            bool hasAnyPlaceholder = false;
            foreach (var key in replacements.Keys)
            {
                // Также проверяем вариант с пропущенной закрывающей скобкой (для обработки опечаток)
                if (fullDocumentText.Contains(key) || fullDocumentText.Contains(key.TrimEnd('}')))
                {
                    hasAnyPlaceholder = true;
                    break;
                }
            }

            if (!hasAnyPlaceholder) return;

            // Сначала удаляем параграфы с плейсхолдерами, которые заменяются на пустую строку
            // Это нужно сделать до замены, чтобы удалить весь блок
            var paragraphs = element.Descendants<Paragraph>().ToList();
            var paragraphsToRemove = new List<Paragraph>();
            
            foreach (var paragraph in paragraphs)
            {
                var runs = paragraph.Elements<Run>().ToList();
                string paragraphText = string.Join("", runs.SelectMany(r => r.Elements<Text>()).Select(t => t.Text));
                
                // Проверяем, содержит ли параграф плейсхолдер, который заменяется на пустую строку
                foreach (var replacement in replacements)
                {
                    if (string.IsNullOrWhiteSpace(replacement.Value) && 
                        (paragraphText.Contains(replacement.Key) || 
                         (replacement.Key.EndsWith("}}") && paragraphText.Contains(replacement.Key.TrimEnd('}')))))
                    {
                        // Если это option1, option2 или Option_study1-5, удаляем весь параграф и следующие до следующего заголовка
                        if (replacement.Key == "{{option1}}" || replacement.Key == "{{option2}}" ||
                            replacement.Key.StartsWith("{{Option_study"))
                        {
                            paragraphsToRemove.Add(paragraph);
                            // Удаляем следующие параграфы до следующего заголовка или до следующего option
                            var currentIndex = paragraphs.IndexOf(paragraph);
                            for (int i = currentIndex + 1; i < paragraphs.Count; i++)
                            {
                                var nextParagraph = paragraphs[i];
                                var nextRuns = nextParagraph.Elements<Run>().ToList();
                                string nextText = string.Join("", nextRuns.SelectMany(r => r.Elements<Text>()).Select(t => t.Text));
                                
                                // Останавливаемся, если встретили другой option
                                if (nextText.Contains("{{option1}}") || nextText.Contains("{{option2}}"))
                                {
                                    break;
                                }
                                
                                // Если это последний параграф блока (содержит "Просрочка" или похожий маркер конца), удаляем его тоже
                                if (nextText.Contains("Просрочка") || nextText.Contains("расторгнуть договор"))
                                {
                                    paragraphsToRemove.Add(nextParagraph);
                                    break;
                                }
                                
                                // Останавливаемся, если встретили заголовок следующего раздела (начинается с цифры и точки, например "3.2." или "4.")
                                if (System.Text.RegularExpressions.Regex.IsMatch(nextText.Trim(), @"^\d+[\.\)]"))
                                {
                                    break;
                                }
                                
                                // Добавляем параграф в список на удаление
                                paragraphsToRemove.Add(nextParagraph);
                            }
                        }
                        else
                        {
                            // Для других плейсхолдеров просто помечаем параграф на удаление
                            paragraphsToRemove.Add(paragraph);
                        }
                        break;
                    }
                }
            }
            
            // Удаляем помеченные параграфы
            foreach (var paragraph in paragraphsToRemove)
            {
                paragraph.Remove();
            }

            // Обрабатываем оставшиеся параграфы
            var remainingParagraphs = element.Descendants<Paragraph>().ToList();
            foreach (var paragraph in remainingParagraphs)
            {
                ReplaceInParagraph(paragraph, replacements);
            }

            // Удаляем параграфы, которые стали пустыми после замены
            var paragraphsAfterReplace = element.Descendants<Paragraph>().ToList();
            foreach (var paragraph in paragraphsAfterReplace)
            {
                var runs = paragraph.Elements<Run>().ToList();
                string paragraphText = string.Join("", runs.SelectMany(r => r.Elements<Text>()).Select(t => t.Text));
                
                // Если параграф пустой или содержит только пробелы/переносы строк, удаляем его
                if (string.IsNullOrWhiteSpace(paragraphText))
                {
                    paragraph.Remove();
                }
            }

            // Обрабатываем таблицы
            var tables = element.Descendants<Table>().ToList();
            foreach (var table in tables)
            {
                var cells = table.Descendants<TableCell>().ToList();
                foreach (var cell in cells)
                {
                    var cellParagraphs = cell.Descendants<Paragraph>().ToList();
                    foreach (var cellParagraph in cellParagraphs)
                    {
                        ReplaceInParagraph(cellParagraph, replacements);
                    }
                }
            }
        }

        /// <summary>
        /// Заменяет плейсхолдеры в параграфе с сохранением структуры и форматирования
        /// </summary>
        private void ReplaceInParagraph(Paragraph paragraph, Dictionary<string, string> replacements)
        {
            if (paragraph == null) return;

            // Получаем все Run элементы в параграфе
            var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count == 0) return;

            // Собираем весь текст параграфа
            string fullText = string.Join("", runs.SelectMany(r => r.Elements<Text>()).Select(t => t.Text));
            if (string.IsNullOrEmpty(fullText)) return;

            // Проверяем наличие плейсхолдеров (проверяем наличие {{ и }})
            if (!fullText.Contains("{{") || !fullText.Contains("}}"))
            {
                return;
            }

            bool hasPlaceholder = false;
            foreach (var replacement in replacements)
            {
                if (fullText.Contains(replacement.Key))
                {
                    hasPlaceholder = true;
                    break;
                }
            }

            if (!hasPlaceholder) return;

            // Заменяем плейсхолдеры в полном тексте
            string newFullText = fullText;
            bool shouldRemoveParagraph = false;
            
            foreach (var replacement in replacements)
            {
                string placeholder = replacement.Key;
                string value = replacement.Value ?? "";

                // Если плейсхолдер заменяется на пустую строку и это option1, option2 или Option_study1-5,
                // помечаем параграф на удаление
                if (string.IsNullOrWhiteSpace(value) && 
                    (placeholder == "{{option1}}" || placeholder == "{{option2}}" ||
                     placeholder.StartsWith("{{Option_study")))
                {
                    if (newFullText.Contains(placeholder))
                    {
                        shouldRemoveParagraph = true;
                        break; // Не заменяем, просто удалим весь параграф
                    }
                }

                // Заменяем все вхождения плейсхолдера в тексте
                if (newFullText.Contains(placeholder))
                {
                    // Для {{enter}} заменяем на пустую строку
                    if (placeholder == "{{enter}}")
                    {
                        newFullText = newFullText.Replace(placeholder, "");
                    }
                    else
                    {
                        // Replace заменяет все вхождения по умолчанию
                        newFullText = newFullText.Replace(placeholder, value);
                    }
                }
            }
            
            // Если нужно удалить параграф (содержит option1/option2, который заменяется на пустую строку)
            if (shouldRemoveParagraph)
            {
                paragraph.Remove();
                return;
            }

            // Если текст изменился, обновляем параграф
            if (newFullText != fullText)
            {
                // Сохраняем ВСЕ свойства параграфа (выравнивание, табуляции, отступы и т.д.)
                var paragraphProperties = paragraph.Elements<ParagraphProperties>().FirstOrDefault();
                
                // Сохраняем все дочерние элементы параграфа, кроме Run (это могут быть табуляции, свойства и т.д.)
                var nonRunElements = paragraph.Elements().Where(e => !(e is Run)).ToList();
                var savedNonRunElements = new List<OpenXmlElement>();
                foreach (var element in nonRunElements)
                {
                    savedNonRunElements.Add(element.CloneNode(true));
                }

                // Сохраняем свойства первого Run для форматирования текста
                var firstRun = runs.FirstOrDefault();
                RunProperties runProperties = null;
                if (firstRun != null)
                {
                    runProperties = firstRun.Elements<RunProperties>().FirstOrDefault();
                }

                // Сохраняем все Break элементы из исходных Run (для сохранения переносов строк)
                var savedBreaks = new List<Break>();
                foreach (var run in runs)
                {
                    var breaks = run.Elements<Break>().ToList();
                    foreach (var br in breaks)
                    {
                        savedBreaks.Add(br.CloneNode(true) as Break);
                    }
                }

                // Удаляем все старые Run элементы, но сохраняем структуру параграфа
                foreach (var run in runs.ToList())
                {
                    run.Remove();
                }

                // Удаляем все не-Run элементы, чтобы потом восстановить их
                foreach (var element in nonRunElements)
                {
                    element.Remove();
                }

                // Восстанавливаем свойства параграфа ПЕРВЫМИ (до добавления Run)
                if (paragraphProperties != null)
                {
                    paragraph.InsertAt(paragraphProperties.CloneNode(true), 0);
                }
                
                // Восстанавливаем остальные не-Run элементы
                foreach (var element in savedNonRunElements)
                {
                    if (!(element is ParagraphProperties))
                    {
                        paragraph.AppendChild(element);
                    }
                }

                // Создаем новый Run с сохраненными свойствами и обновленным текстом
                var newRun = new Run();
                if (runProperties != null)
                {
                    newRun.Append(runProperties.CloneNode(true));
                }
                newRun.Append(new Text(newFullText));

                // Добавляем новый Run в параграф
                paragraph.AppendChild(newRun);
                
                // Если текст не содержал переносов строк, но были сохраненные Break, добавляем их обратно
                if (savedBreaks.Count > 0)
                {
                    // Если в исходном тексте были Break, добавляем их в конец последнего Run
                    var lastRun = paragraph.Elements<Run>().LastOrDefault();
                    if (lastRun != null)
                    {
                        foreach (var br in savedBreaks)
                        {
                            lastRun.Append(br);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Открывает Word документ в приложении по умолчанию
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
    }
}

