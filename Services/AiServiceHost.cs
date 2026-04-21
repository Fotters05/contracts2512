using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Contract2512.Services;

public sealed class AiServiceHost : IDisposable
{
    private Process? _process;
    private bool _startedByApp;

    public string? ServiceRoot => ResolveServiceRoot();

    public async Task<bool> StartIfNeededAsync(Uri baseAddress)
    {
        if (_process is { HasExited: false })
        {
            return false;
        }

        var serviceRoot = ResolveServiceRoot();
        if (string.IsNullOrWhiteSpace(serviceRoot))
        {
            throw new DirectoryNotFoundException("Не удалось найти папку ai_service рядом с приложением.");
        }

        var pythonExe = ResolvePythonExecutable(serviceRoot);
        if (string.IsNullOrWhiteSpace(pythonExe))
        {
            throw new FileNotFoundException("Не удалось найти Python для запуска ai_service.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"-m uvicorn app.main:app --host {baseAddress.Host} --port {baseAddress.Port}",
            WorkingDirectory = serviceRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Не удалось запустить процесс ai_service.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _startedByApp = true;

        await WaitUntilHealthyAsync(baseAddress).ConfigureAwait(false);
        return true;
    }

    public void Stop()
    {
        if (!_startedByApp || _process == null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // Ignore shutdown failures.
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _startedByApp = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task WaitUntilHealthyAsync(Uri baseAddress)
    {
        using var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(3),
        };

        Exception? lastError = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (_process is { HasExited: true })
            {
                var exitCode = _process.ExitCode;
                Stop();
                throw new InvalidOperationException($"ai_service завершился сразу после запуска. Код выхода: {exitCode}.");
            }

            try
            {
                using var response = await client.GetAsync("api/health").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        Stop();
        throw new InvalidOperationException($"ai_service не ответил после запуска. {lastError?.Message}".Trim());
    }

    private static string? ResolveServiceRoot()
    {
        var configuredRoot = EnvConfigService.Get("AI_SERVICE_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot) &&
            File.Exists(Path.Combine(configuredRoot, "app", "main.py")))
        {
            return configuredRoot;
        }

        foreach (var origin in new[]
                 {
                     AppDomain.CurrentDomain.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                 }
                 .Where(path => !string.IsNullOrWhiteSpace(path))
                 .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(origin);
            for (var depth = 0; current != null && depth < 8; depth++, current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "ai_service");
                if (File.Exists(Path.Combine(candidate, "app", "main.py")))
                {
                    return candidate;
                }

                if (File.Exists(Path.Combine(current.FullName, "app", "main.py")) &&
                    string.Equals(current.Name, "ai_service", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }
            }
        }

        return null;
    }

    private static string? ResolvePythonExecutable(string serviceRoot)
    {
        var venvPython = Path.Combine(serviceRoot, "venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        return "python";
    }
}
