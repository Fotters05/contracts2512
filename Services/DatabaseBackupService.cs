using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Contract2512.Services;

public sealed class DatabaseBackupService
{
    private const int BackupVersion = 1;

    public async Task ExportAsync(string connectionString, string filePath, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tables = await LoadTablesAsync(connection, cancellationToken);

        await using var stream = File.Create(filePath);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        writer.WriteStartObject();
        writer.WriteNumber("version", BackupVersion);
        writer.WriteString("exportedAt", DateTimeOffset.Now);
        writer.WriteString("database", connection.Database);
        writer.WriteStartArray("tables");

        foreach (var table in tables)
        {
            var columns = await LoadColumnsAsync(connection, table, cancellationToken);
            var rowsJson = await ExportRowsAsync(connection, table, cancellationToken);

            writer.WriteStartObject();
            writer.WriteString("schema", table.Schema);
            writer.WriteString("name", table.Name);
            writer.WriteStartArray("columns");
            foreach (var column in columns)
            {
                writer.WriteStringValue(column);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("rows");
            writer.WriteRawValue(rowsJson);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    public async Task ImportAsync(string connectionString, string filePath, CancellationToken cancellationToken = default)
    {
        await using var fileStream = File.OpenRead(filePath);
        using var backup = await JsonDocument.ParseAsync(fileStream, cancellationToken: cancellationToken);

        if (!backup.RootElement.TryGetProperty("version", out var versionElement) ||
            versionElement.GetInt32() != BackupVersion)
        {
            throw new InvalidOperationException("Неподдерживаемая версия файла резервной копии.");
        }

        var tables = ReadBackupTables(backup.RootElement).ToList();
        if (tables.Count == 0)
        {
            throw new InvalidOperationException("В файле резервной копии нет таблиц.");
        }

        await EnsureApplicationSchemaAsync(connectionString, cancellationToken);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        tables = await KeepExistingTablesAndColumnsAsync(connection, tables, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await TruncateTablesAsync(connection, transaction, tables, cancellationToken);

            var orderedTables = await OrderTablesForImportAsync(connection, transaction, tables, cancellationToken);
            foreach (var table in orderedTables)
            {
                await ImportTableAsync(connection, transaction, table, cancellationToken);
            }

            await ResetSequencesAsync(connection, transaction, tables, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<List<TableRef>> LoadTablesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;

        var tables = new List<TableRef>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new TableRef(reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    private static async Task<List<string>> LoadColumnsAsync(NpgsqlConnection connection, TableRef table, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @schema
              AND table_name = @table
            ORDER BY ordinal_position;
            """;

        var columns = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", table.Schema);
        command.Parameters.AddWithValue("table", table.Name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<BackupTable>> KeepExistingTablesAndColumnsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<BackupTable> tables,
        CancellationToken cancellationToken)
    {
        var existingTables = await LoadTablesAsync(connection, cancellationToken);
        var existingTableKeys = existingTables.Select(table => table.Key).ToHashSet(StringComparer.Ordinal);
        var result = new List<BackupTable>();

        foreach (var table in tables)
        {
            if (!existingTableKeys.Contains(table.Table.Key))
            {
                continue;
            }

            var existingColumns = await LoadColumnsAsync(connection, table.Table, cancellationToken);
            var existingColumnSet = existingColumns.ToHashSet(StringComparer.Ordinal);
            var columns = table.Columns
                .Where(existingColumnSet.Contains)
                .ToList();

            if (columns.Count > 0)
            {
                result.Add(table with { Columns = columns });
            }
        }

        return result;
    }

    private static async Task<string> ExportRowsAsync(NpgsqlConnection connection, TableRef table, CancellationToken cancellationToken)
    {
        var primaryKeys = await LoadPrimaryKeyColumnsAsync(connection, table, cancellationToken);
        var orderBy = primaryKeys.Count == 0
            ? string.Empty
            : $" ORDER BY {string.Join(", ", primaryKeys.Select(QuoteIdentifier))}";

        var sql = $"""
            SELECT COALESCE(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text
            FROM (SELECT * FROM {QuoteTable(table)}{orderBy}) AS t;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "[]";
    }

    private static async Task<List<string>> LoadPrimaryKeyColumnsAsync(NpgsqlConnection connection, TableRef table, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE i.indisprimary
              AND n.nspname = @schema
              AND c.relname = @table
            ORDER BY array_position(i.indkey, a.attnum);
            """;

        var columns = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", table.Schema);
        command.Parameters.AddWithValue("table", table.Name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static IEnumerable<BackupTable> ReadBackupTables(JsonElement root)
    {
        foreach (var tableElement in root.GetProperty("tables").EnumerateArray())
        {
            var table = new TableRef(
                tableElement.GetProperty("schema").GetString() ?? "public",
                tableElement.GetProperty("name").GetString() ?? string.Empty);

            var columns = tableElement.GetProperty("columns")
                .EnumerateArray()
                .Select(column => column.GetString() ?? string.Empty)
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .ToList();

            var rowsJson = tableElement.GetProperty("rows").GetRawText();
            if (!string.IsNullOrWhiteSpace(table.Name) && columns.Count > 0)
            {
                yield return new BackupTable(table, columns, rowsJson);
            }
        }
    }

    private static async Task TruncateTablesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<BackupTable> tables,
        CancellationToken cancellationToken)
    {
        var tableList = string.Join(", ", tables.Select(table => QuoteTable(table.Table)));
        await using var command = new NpgsqlCommand($"TRUNCATE TABLE {tableList} RESTART IDENTITY CASCADE;", connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<BackupTable>> OrderTablesForImportAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<BackupTable> tables,
        CancellationToken cancellationToken)
    {
        var tableMap = tables.ToDictionary(table => table.Table.Key, StringComparer.Ordinal);
        var dependencies = tableMap.Keys.ToDictionary(key => key, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        const string sql = """
            SELECT child_ns.nspname, child.relname, parent_ns.nspname, parent.relname
            FROM pg_constraint fk
            JOIN pg_class child ON child.oid = fk.conrelid
            JOIN pg_namespace child_ns ON child_ns.oid = child.relnamespace
            JOIN pg_class parent ON parent.oid = fk.confrelid
            JOIN pg_namespace parent_ns ON parent_ns.oid = parent.relnamespace
            WHERE fk.contype = 'f';
            """;

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var child = new TableRef(reader.GetString(0), reader.GetString(1)).Key;
                var parent = new TableRef(reader.GetString(2), reader.GetString(3)).Key;
                if (dependencies.ContainsKey(child) && tableMap.ContainsKey(parent) && child != parent)
                {
                    dependencies[child].Add(parent);
                }
            }
        }

        var ordered = new List<BackupTable>();
        var remaining = new HashSet<string>(tableMap.Keys, StringComparer.Ordinal);

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(key => dependencies[key].All(parent => !remaining.Contains(parent)))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            if (ready.Count == 0)
            {
                ready = remaining.OrderBy(key => key, StringComparer.Ordinal).ToList();
            }

            foreach (var key in ready)
            {
                ordered.Add(tableMap[key]);
                remaining.Remove(key);
            }
        }

        return ordered;
    }

    private static async Task ImportTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BackupTable table,
        CancellationToken cancellationToken)
    {
        if (table.RowsJson == "[]")
        {
            return;
        }

        var columns = string.Join(", ", table.Columns.Select(QuoteIdentifier));
        var sql = $"""
            INSERT INTO {QuoteTable(table.Table)} ({columns})
            SELECT {columns}
            FROM json_populate_recordset(NULL::{QuoteTable(table.Table)}, @rows::json);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("rows", table.RowsJson);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ResetSequencesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<BackupTable> tables,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_schema, table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND (
                    column_default LIKE 'nextval(%'
                    OR identity_generation IS NOT NULL
                  );
            """;

        var sequenceColumns = new List<(TableRef Table, string Column)>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            var tableKeys = tables.Select(table => table.Table.Key).ToHashSet(StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = new TableRef(reader.GetString(0), reader.GetString(1));
                if (tableKeys.Contains(table.Key))
                {
                    sequenceColumns.Add((table, reader.GetString(2)));
                }
            }
        }

        foreach (var (table, column) in sequenceColumns)
        {
            var sequenceName = await GetSerialSequenceAsync(connection, transaction, table, column, cancellationToken);
            if (string.IsNullOrWhiteSpace(sequenceName))
            {
                continue;
            }

            var maxSql = $"SELECT MAX({QuoteIdentifier(column)}) FROM {QuoteTable(table)};";
            await using var maxCommand = new NpgsqlCommand(maxSql, connection, transaction);
            var maxValue = await maxCommand.ExecuteScalarAsync(cancellationToken);
            var hasRows = maxValue != DBNull.Value && maxValue is not null;
            var nextValue = hasRows ? Convert.ToInt64(maxValue) : 1L;

            await using var resetCommand = new NpgsqlCommand("SELECT setval(@sequence::regclass, @value, @is_called);", connection, transaction);
            resetCommand.Parameters.AddWithValue("sequence", sequenceName);
            resetCommand.Parameters.AddWithValue("value", nextValue);
            resetCommand.Parameters.AddWithValue("is_called", hasRows);
            await resetCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<string?> GetSerialSequenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TableRef table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_get_serial_sequence(@table, @column);", connection, transaction);
        command.Parameters.AddWithValue("table", $"{table.Schema}.{table.Name}");
        command.Parameters.AddWithValue("column", column);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task EnsureApplicationSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        db.EnsureSchemaCompatibility();
    }

    private static string QuoteTable(TableRef table) => $"{QuoteIdentifier(table.Schema)}.{QuoteIdentifier(table.Name)}";

    private static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct TableRef(string Schema, string Name)
    {
        public string Key => $"{Schema}.{Name}";
    }

    private sealed record BackupTable(TableRef Table, IReadOnlyList<string> Columns, string RowsJson);
}
