using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Contract2512.Services;

public static class EnvConfigService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Contract2512");

    public const string ConnectionStringKey = "DB_CONNECTION_STRING";

    public static string GetEnvFilePath()
    {
        // Prefer next to executable for dev/debug convenience.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = Path.Combine(baseDir, ".env");

        if (IsWritableDirectory(baseDir))
            return candidate;

        Directory.CreateDirectory(AppDataDir);
        return Path.Combine(AppDataDir, ".env");
    }

    public static string? Get(string key)
    {
        // Allow process env var override (useful for immediate switch without restart).
        var fromProcessEnv = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(fromProcessEnv))
            return fromProcessEnv;

        var path = GetEnvFilePath();
        if (!File.Exists(path))
            return null;

        var map = ReadEnvFile(path);
        return map.TryGetValue(key, out var value) ? value : null;
    }

    public static void Set(string key, string value)
    {
        var path = GetEnvFilePath();
        var map = File.Exists(path) ? ReadEnvFile(path) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        map[key] = value;
        WriteEnvFile(path, map);

        // Apply immediately for the current process.
        Environment.SetEnvironmentVariable(key, value);
    }

    private static Dictionary<string, string> ReadEnvFile(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            // Optional quotes.
            if (value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
                value = value[1..^1];

            if (key.Length == 0)
                continue;

            map[key] = value;
        }

        return map;
    }

    private static void WriteEnvFile(string path, Dictionary<string, string> map)
    {
        var lines = map
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => $"{kvp.Key}={kvp.Value}")
            .ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private static bool IsWritableDirectory(string directoryPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return false;

            var testFile = Path.Combine(directoryPath, $".__w_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "w", Encoding.UTF8);
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
