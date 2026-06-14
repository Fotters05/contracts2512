using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Contract2512.Services;

public sealed class AiServiceHost : IDisposable
{
    private Process? _process;
    private bool _startedByApp;

    public string? ServiceRoot => ResolveServiceRoot();
    public bool IsRunning => _process is { HasExited: false };
    public bool StartedByApp => _startedByApp && IsRunning;

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

        await EnsureRequirementsInstalledAsync(serviceRoot, pythonExe).ConfigureAwait(false);

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
        startInfo.Environment["AI_SERVICE_ENV_FILE"] = EnvConfigService.GetEnvFilePath();

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

    public bool Stop()
    {
        if (!_startedByApp || _process == null)
        {
            return false;
        }

        var stopped = false;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
                stopped = true;
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

        return stopped;
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

    private static async Task EnsureRequirementsInstalledAsync(string serviceRoot, string pythonExe)
    {
        if (await PythonDependenciesAvailableAsync(serviceRoot, pythonExe).ConfigureAwait(false))
        {
            return;
        }

        var requirementsPath = Path.Combine(serviceRoot, "requirements.txt");
        if (!File.Exists(requirementsPath))
        {
            throw new FileNotFoundException("Не найден requirements.txt для установки зависимостей ai_service.", requirementsPath);
        }

        var installResult = await RunPythonAsync(
            pythonExe,
            serviceRoot,
            TimeSpan.FromMinutes(5),
            "-m",
            "pip",
            "install",
            "-r",
            requirementsPath).ConfigureAwait(false);

        if (installResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Не удалось установить зависимости ai_service. Проверьте интернет и Python/pip. {installResult.Error}".Trim());
        }

        if (!await PythonDependenciesAvailableAsync(serviceRoot, pythonExe).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Зависимости ai_service установлены, но Python всё ещё не может их импортировать.");
        }
    }

    private static async Task<bool> PythonDependenciesAvailableAsync(string serviceRoot, string pythonExe)
    {
        var result = await RunPythonAsync(
            pythonExe,
            serviceRoot,
            TimeSpan.FromSeconds(20),
            "-c",
            "import fastapi, uvicorn, pydantic, docx, dotenv, psycopg2, requests, multipart, pypdf").ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    private static async Task<ProcessResult> RunPythonAsync(
        string pythonExe,
        string workingDirectory,
        TimeSpan timeout,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                output.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                error.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Не удалось запустить Python.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var waitForExitTask = process.WaitForExitAsync();
        var exited = await Task.WhenAny(waitForExitTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (exited != waitForExitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill failures after timeout.
            }

            return new ProcessResult(-1, output.ToString(), "Python command timed out.");
        }

        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
