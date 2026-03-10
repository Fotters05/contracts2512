using System;

namespace Contract2512.Services;

public static class DbConnectionStringProvider
{
    // Last resort fallback to keep the app working if no .env/config exists yet.
    private const string LegacyFallback =
        "Host=26.242.232.93;Port=5432;Username=postgres;Password=1;Database=MPT2512";

    public static string GetConnectionString()
    {
        var fromEnv = EnvConfigService.Get(EnvConfigService.ConnectionStringKey);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        return LegacyFallback;
    }
}
