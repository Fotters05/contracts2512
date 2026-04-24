using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Contract2512.Models;

namespace Contract2512.Services;

public sealed class AiStandardProfileCatalogService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<AiStandardProfileCatalogEntry> LoadProfiles()
    {
        var path = ResolveProfilesPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Array.Empty<AiStandardProfileCatalogEntry>();
        }

        var raw = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<Dictionary<string, AiStandardProfileCatalogEntry>>(raw, _jsonOptions);
        if (map == null || map.Count == 0)
        {
            return Array.Empty<AiStandardProfileCatalogEntry>();
        }

        return map.Values
            .Where(item => !string.IsNullOrWhiteSpace(item.ProfileId))
            .OrderBy(item => item.FgosCode)
            .ThenBy(item => item.Title)
            .ToList();
    }

    private static string? ResolveProfilesPath()
    {
        var serviceRoot = new AiServiceHost().ServiceRoot;
        if (string.IsNullOrWhiteSpace(serviceRoot))
        {
            return null;
        }

        return Path.Combine(serviceRoot, "storage", "standards", "profiles.json");
    }
}
