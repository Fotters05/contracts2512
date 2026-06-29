using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Contract2512.Services;

public static class UserErrorMessageService
{
    public static string ToRussian(Exception? exception)
    {
        if (exception is null)
        {
            return "Произошла неизвестная ошибка.";
        }

        return TranslateException(Unwrap(exception), includeInner: true);
    }

    public static string ToRussianText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Произошла неизвестная ошибка.";
        }

        var trimmed = text.Trim();
        var known = TranslateKnownText(trimmed);
        if (!string.IsNullOrWhiteSpace(known))
        {
            return known;
        }

        return ContainsCyrillic(trimmed)
            ? trimmed
            : "Произошла техническая ошибка. Проверьте введённые данные и повторите операцию.";
    }

    private static string TranslateException(Exception exception, bool includeInner)
    {
        var message = exception switch
        {
            PostgresException postgresException => TranslatePostgresException(postgresException),
            DbUpdateException dbUpdateException => TranslateDbUpdateException(dbUpdateException),
            NpgsqlException npgsqlException => TranslateNpgsqlException(npgsqlException),
            JsonException => "Файл резервной копии повреждён или имеет неверный формат JSON.",
            UnauthorizedAccessException => "Нет прав на чтение или запись файла. Выберите другую папку или запустите приложение с нужными правами.",
            FileNotFoundException fileNotFoundException => TranslateFileNotFoundException(fileNotFoundException),
            DirectoryNotFoundException => "Папка не найдена. Проверьте путь и повторите операцию.",
            IOException ioException => TranslateIoException(ioException),
            FormatException => "Ошибка формата данных. Проверьте введённые значения или файл резервной копии.",
            TimeoutException => "Операция заняла слишком много времени. Проверьте подключение и повторите попытку.",
            HttpRequestException => "Не удалось выполнить сетевой запрос. Проверьте интернет и доступность сервера.",
            InvalidOperationException invalidOperationException => ToRussianText(invalidOperationException.Message),
            _ => ToRussianText(exception.Message)
        };

        if (!includeInner || exception.InnerException is null)
        {
            return message;
        }

        var inner = TranslateException(Unwrap(exception.InnerException), includeInner: false);
        return string.Equals(message, inner, StringComparison.Ordinal)
            ? message
            : $"{message}\nПодробности: {inner}";
    }

    private static string TranslateDbUpdateException(DbUpdateException exception)
    {
        if (exception.InnerException is not null)
        {
            return TranslateException(Unwrap(exception.InnerException), includeInner: false);
        }

        return "Не удалось сохранить данные в базе данных. Проверьте заполненные поля и повторите операцию.";
    }

    private static string TranslatePostgresException(PostgresException exception)
    {
        var table = FormatDbObject(exception.TableName, "нужная таблица");
        var column = FormatDbColumn(exception.TableName, exception.ColumnName, "нужное поле");
        var constraint = FormatDbObject(exception.ConstraintName, "ограничение");

        return exception.SqlState switch
        {
            PostgresErrorCodes.InvalidPassword => "Неверный логин или пароль PostgreSQL.",
            PostgresErrorCodes.InvalidCatalogName => "База данных не найдена. Проверьте параметр Database в строке подключения.",
            PostgresErrorCodes.UndefinedTable => $"В базе данных нет таблицы {table}. Для пустой базы импорт должен создать структуру автоматически; если ошибка повторится, проверьте права пользователя и файл резервной копии.",
            PostgresErrorCodes.UndefinedColumn => $"В базе данных нет поля {column}. Возможно, структура базы отличается от файла резервной копии.",
            PostgresErrorCodes.NotNullViolation => BuildRequiredFieldMessage(exception.TableName, exception.ColumnName, table, column),
            PostgresErrorCodes.ForeignKeyViolation => "Нарушена связь между таблицами. Возможно, файл резервной копии повреждён или восстановлен не полностью.",
            PostgresErrorCodes.UniqueViolation => "Такая запись уже есть в базе данных.",
            PostgresErrorCodes.CheckViolation => $"Данные не прошли проверку ограничения {constraint}. Проверьте значения в файле или форме.",
            PostgresErrorCodes.InsufficientPrivilege => "Недостаточно прав PostgreSQL для этой операции.",
            PostgresErrorCodes.TooManyConnections => "Сервер PostgreSQL отклонил подключение: слишком много активных подключений.",
            PostgresErrorCodes.QueryCanceled => "Операция PostgreSQL была отменена или превысила лимит времени.",
            _ => TranslateKnownText(exception.MessageText)
                 ?? $"PostgreSQL вернул ошибку с кодом {exception.SqlState}. Проверьте данные и повторите операцию."
        };
    }

    private static string TranslateNpgsqlException(NpgsqlException exception)
    {
        return TranslateKnownText(exception.Message)
            ?? "Не удалось выполнить операцию с PostgreSQL. Проверьте строку подключения, доступность сервера и права пользователя.";
    }

    private static string TranslateFileNotFoundException(FileNotFoundException exception)
    {
        if (!string.IsNullOrWhiteSpace(exception.FileName))
        {
            return $"Файл не найден: {exception.FileName}";
        }

        return TranslateKnownText(exception.Message) ?? "Файл не найден. Проверьте путь и повторите операцию.";
    }

    private static string TranslateIoException(IOException exception)
    {
        return TranslateKnownText(exception.Message)
            ?? "Ошибка работы с файлом. Проверьте, что файл не открыт в другой программе и доступен для чтения или записи.";
    }

    private static string? TranslateKnownText(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var lower = message.ToLowerInvariant();

        if (lower.Contains("index (zero based) must be greater than or equal to zero"))
        {
            return "Внутренняя ошибка формирования SQL-команды: неверное количество параметров. Попробуйте повторить операцию; если ошибка повторится, сообщите разработчику.";
        }

        if (lower.Contains("an error occurred while saving the entity changes"))
        {
            var requiredFieldMessage = TryTranslateRequiredFieldFromText(lower);
            return requiredFieldMessage
                ?? "Не удалось сохранить данные. Проверьте, что заполнены все обязательные поля.";
        }

        var notNullMessage = TryTranslateRequiredFieldFromText(lower);
        if (notNullMessage is not null)
        {
            return notNullMessage;
        }

        if (lower.Contains("password authentication failed"))
        {
            return "Неверный логин или пароль PostgreSQL.";
        }

        if (lower.Contains("database") && lower.Contains("does not exist"))
        {
            return "База данных не найдена. Проверьте параметр Database в строке подключения.";
        }

        if (lower.Contains("relation") && lower.Contains("does not exist"))
        {
            return "В базе данных нет нужной таблицы. Для пустой базы импорт должен создать структуру автоматически; если ошибка повторится, проверьте права пользователя и файл резервной копии.";
        }

        if (lower.Contains("column") && lower.Contains("does not exist"))
        {
            return "В базе данных нет нужного поля. Возможно, структура базы отличается от файла резервной копии.";
        }

        if (lower.Contains("no such host") || lower.Contains("name or service not known"))
        {
            return "Хост базы данных не найден. Проверьте значение Host в строке подключения.";
        }

        if (lower.Contains("connection refused") || lower.Contains("actively refused"))
        {
            return "Сервер PostgreSQL отклонил подключение. Проверьте Host, Port и запущен ли сервер.";
        }

        if (lower.Contains("timeout") || lower.Contains("timed out"))
        {
            return "Подключение или операция заняли слишком много времени. Проверьте интернет и доступность сервера.";
        }

        if (lower.Contains("permission denied") || lower.Contains("access denied"))
        {
            return "Недостаточно прав для выполнения операции. Проверьте права пользователя или выберите другую папку.";
        }

        if (lower.Contains("could not find file") || lower.Contains("file not found"))
        {
            return "Файл не найден. Проверьте путь и повторите операцию.";
        }

        if (lower.Contains("microsoft excel is not available"))
        {
            return "Microsoft Excel недоступен. Установите Excel или проверьте его запуск.";
        }

        if (lower.Contains("unable to start microsoft excel"))
        {
            return "Не удалось запустить Microsoft Excel.";
        }

        if (lower.Contains("failed to export workload pdf"))
        {
            return "Не удалось экспортировать нагрузку в PDF.";
        }

        if (lower.Contains("failed to open browser"))
        {
            return "Не удалось открыть браузер.";
        }

        if (lower.Contains("error during npm install") || lower.Contains("failed to install npm packages"))
        {
            return "Не удалось установить npm-пакеты. Проверьте интернет и установлен ли Node.js/npm.";
        }

        if (lower.Contains("ai service returned an empty response"))
        {
            return "AI-сервис вернул пустой ответ. Попробуйте повторить запрос.";
        }

        return null;
    }

    private static Exception Unwrap(Exception exception)
    {
        return exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count == 1
            ? Unwrap(aggregateException.InnerExceptions[0])
            : exception;
    }

    private static string FormatDbObject(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : $"\"{value}\"";
    }

    private static string FormatDbColumn(string? tableName, string? columnName, string fallback)
    {
        var friendlyName = GetFriendlyColumnName(tableName, columnName);
        return string.IsNullOrWhiteSpace(friendlyName) ? FormatDbObject(columnName, fallback) : $"\"{friendlyName}\"";
    }

    private static string BuildRequiredFieldMessage(string? tableName, string? columnName, string table, string column)
    {
        var friendlyName = GetFriendlyColumnName(tableName, columnName);
        var friendlySection = GetFriendlyTableName(tableName);

        if (!string.IsNullOrWhiteSpace(friendlyName) && !string.IsNullOrWhiteSpace(friendlySection))
        {
            return $"Не заполнено обязательное поле \"{friendlyName}\" в разделе \"{friendlySection}\".";
        }

        if (!string.IsNullOrWhiteSpace(friendlyName))
        {
            return $"Не заполнено обязательное поле \"{friendlyName}\".";
        }

        return $"Не заполнено обязательное поле {column} в таблице {table}.";
    }

    private static string? TryTranslateRequiredFieldFromText(string lowerMessage)
    {
        if (!lowerMessage.Contains("null") && !lowerMessage.Contains("not-null"))
        {
            return null;
        }

        if (lowerMessage.Contains("enrollment_date"))
        {
            return "Не заполнено обязательное поле \"Дата поступления\" в разделе \"Образование\".";
        }

        if (lowerMessage.Contains("last_name"))
        {
            return "Не заполнено обязательное поле \"Фамилия\".";
        }

        if (lowerMessage.Contains("first_name"))
        {
            return "Не заполнено обязательное поле \"Имя\".";
        }

        if (lowerMessage.Contains("contract_number"))
        {
            return "Не заполнено обязательное поле \"Номер договора\".";
        }

        if (lowerMessage.Contains("contract_date"))
        {
            return "Не заполнено обязательное поле \"Дата договора\".";
        }

        if (lowerMessage.Contains("order_number"))
        {
            return "Не заполнено обязательное поле \"Номер приказа\".";
        }

        if (lowerMessage.Contains("order_date"))
        {
            return "Не заполнено обязательное поле \"Дата приказа\".";
        }

        return null;
    }

    private static string? GetFriendlyColumnName(string? tableName, string? columnName)
    {
        return (tableName, columnName) switch
        {
            ("education", "enrollment_date") => "Дата поступления",
            ("education", "base_education_id") => "Базовое образование",
            ("education", "education_level_id") => "Уровень образования",
            ("education", "number") => "Номер документа об образовании",
            ("education", "issue_date") => "Дата выдачи документа об образовании",
            ("education", "issued_by") => "Кем выдан документ об образовании",
            ("person", "last_name") => "Фамилия",
            ("person", "first_name") => "Имя",
            ("contract", "contract_number") => "Номер договора",
            ("contract", "contract_date") => "Дата договора",
            ("order_registry", "order_number") => "Номер приказа",
            ("order_registry", "order_date") => "Дата приказа",
            _ => null
        };
    }

    private static string? GetFriendlyTableName(string? tableName)
    {
        return tableName switch
        {
            "education" => "Образование",
            "person" => "Физическое лицо",
            "contract" => "Договор",
            "order_registry" => "Реестр приказов",
            _ => null
        };
    }

    private static bool ContainsCyrillic(string value)
    {
        foreach (var character in value)
        {
            if (character >= 'А' && character <= 'я' || character == 'ё' || character == 'Ё')
            {
                return true;
            }
        }

        return false;
    }
}
