using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Contract2512.Services
{
    /// <summary>
    /// Service for managing Node.js packages (npm install)
    /// </summary>
    public class NodePackageService
    {
        private readonly string _parserPath;
        private readonly string _nodeModulesPath;
        private readonly string? _sharedNodeModulesPath;
        private readonly bool _isSquirrelInstall;

        public NodePackageService()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var parentDir = Directory.GetParent(appDir)?.FullName;
            _isSquirrelInstall = parentDir != null &&
                                 appDir.Contains("app-", StringComparison.OrdinalIgnoreCase) &&
                                 File.Exists(Path.Combine(parentDir, "Update.exe"));

            var publishedPath = Path.Combine(appDir, "parser_nodejs");
            if (Directory.Exists(publishedPath))
            {
                _parserPath = publishedPath;
            }
            else
            {
                var projectRoot = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\"));
                _parserPath = Path.Combine(projectRoot, "parser_nodejs");
            }

            _nodeModulesPath = Path.Combine(_parserPath, "node_modules");
            _sharedNodeModulesPath = _isSquirrelInstall && parentDir != null
                ? Path.Combine(parentDir, "shared-data", "parser_nodejs", "node_modules")
                : null;

            RestoreNodeModulesFromSharedStorage();

            Debug.WriteLine($"App directory: {appDir}");
            Debug.WriteLine($"Parser path: {_parserPath}");
            Debug.WriteLine($"Parser exists: {Directory.Exists(_parserPath)}");
            Debug.WriteLine($"Node modules path: {_nodeModulesPath}");
            Debug.WriteLine($"Shared node modules path: {_sharedNodeModulesPath}");
        }

        /// <summary>
        /// Checks if node_modules folder exists
        /// </summary>
        public bool IsNodeModulesInstalled()
        {
            if (Directory.Exists(_nodeModulesPath))
            {
                return true;
            }

            RestoreNodeModulesFromSharedStorage();
            return Directory.Exists(_nodeModulesPath);
        }

        /// <summary>
        /// Checks if Node.js is installed on the system
        /// </summary>
        public async Task<bool> IsNodeJsInstalledAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Installs npm packages in parser_nodejs folder
        /// </summary>
        public async Task<(bool success, string output)> InstallPackagesAsync(IProgress<string>? progress = null)
        {
            try
            {
                if (!Directory.Exists(_parserPath))
                {
                    return (false, $"Parser folder not found: {_parserPath}");
                }

                progress?.Report("Starting npm install...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c npm install",
                        WorkingDirectory = _parserPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                var output = "";
                var error = "";

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output += e.Data + Environment.NewLine;
                        progress?.Report(e.Data);
                        Debug.WriteLine($"npm: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error += e.Data + Environment.NewLine;
                        Debug.WriteLine($"npm error: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    PersistNodeModulesToSharedStorage();
                    progress?.Report("npm install completed successfully!");
                    return (true, output);
                }

                return (false, $"npm install failed with exit code {process.ExitCode}\n{error}");
            }
            catch (Exception ex)
            {
                return (false, $"Error during npm install: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows dialog to install Node.js if not found
        /// </summary>
        public void ShowNodeJsInstallDialog()
        {
            var result = MessageBox.Show(
                "Node.js is not installed on your system.\n\n" +
                "The parser requires Node.js to work.\n\n" +
                "Would you like to download Node.js now?",
                "Node.js Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://nodejs.org/",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to open browser: {ex.Message}\n\n" +
                        "Please visit https://nodejs.org/ manually to download Node.js",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private void RestoreNodeModulesFromSharedStorage()
        {
            try
            {
                if (Directory.Exists(_nodeModulesPath) || !Directory.Exists(_parserPath))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_sharedNodeModulesPath) && Directory.Exists(_sharedNodeModulesPath))
                {
                    CopyDirectory(_sharedNodeModulesPath, _nodeModulesPath);
                    return;
                }

                if (!_isSquirrelInstall)
                {
                    return;
                }

                var previousNodeModulesPath = FindPreviousVersionNodeModulesPath();
                if (string.IsNullOrWhiteSpace(previousNodeModulesPath) || !Directory.Exists(previousNodeModulesPath))
                {
                    return;
                }

                CopyDirectory(previousNodeModulesPath, _nodeModulesPath);

                if (!string.IsNullOrWhiteSpace(_sharedNodeModulesPath))
                {
                    CopyDirectory(previousNodeModulesPath, _sharedNodeModulesPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to restore node_modules from shared storage: {ex.Message}");
            }
        }

        private void PersistNodeModulesToSharedStorage()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_sharedNodeModulesPath) || !Directory.Exists(_nodeModulesPath))
                {
                    return;
                }

                CopyDirectory(_nodeModulesPath, _sharedNodeModulesPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to persist node_modules to shared storage: {ex.Message}");
            }
        }

        private string? FindPreviousVersionNodeModulesPath()
        {
            try
            {
                var currentAppDir = Directory.GetParent(_parserPath)?.FullName;
                var installRoot = currentAppDir != null ? Directory.GetParent(currentAppDir)?.FullName : null;
                if (string.IsNullOrWhiteSpace(currentAppDir) || string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
                {
                    return null;
                }

                return Directory.GetDirectories(installRoot, "app-*")
                    .Where(dir => !string.Equals(dir.TrimEnd('\\'), currentAppDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(dir => dir, StringComparer.OrdinalIgnoreCase)
                    .Select(dir => Path.Combine(dir, "parser_nodejs", "node_modules"))
                    .FirstOrDefault(Directory.Exists);
            }
            catch
            {
                return null;
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var source = new DirectoryInfo(sourceDir);
            if (!source.Exists)
            {
                return;
            }

            if (Directory.Exists(destinationDir))
            {
                Directory.Delete(destinationDir, recursive: true);
            }

            Directory.CreateDirectory(destinationDir);

            foreach (var directory in source.GetDirectories("*", SearchOption.AllDirectories))
            {
                var targetDirectory = directory.FullName.Replace(source.FullName, destinationDir, StringComparison.OrdinalIgnoreCase);
                Directory.CreateDirectory(targetDirectory);
            }

            foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
            {
                var targetFile = file.FullName.Replace(source.FullName, destinationDir, StringComparison.OrdinalIgnoreCase);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                file.CopyTo(targetFile, overwrite: true);
            }
        }
    }
}
