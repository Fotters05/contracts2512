using Clowd.Squirrel;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace Contract2512.Services
{
    public class AutoUpdateService
    {
        private readonly string _updateUrl;

        public AutoUpdateService(string updateUrl)
        {
            _updateUrl = updateUrl;
        }

        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                Debug.WriteLine($"🔍 Checking updates: {_updateUrl}");

                using var mgr = new UpdateManager(_updateUrl);

                var update = await mgr.CheckForUpdate();

                if (update.ReleasesToApply.Count > 0)
                {
                    var newVersion = update.FutureReleaseEntry.Version.ToString();

                    Debug.WriteLine($"✅ Update found: {newVersion}");

                    return new UpdateInfo
                    {
                        HasUpdate = true,
                        Version = newVersion,
                        CurrentVersion = GetCurrentVersion(),
                        ReleaseNotes = "Доступна новая версия приложения"
                    };
                }

                return new UpdateInfo
                {
                    HasUpdate = false,
                    CurrentVersion = GetCurrentVersion()
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Update error: {ex.Message}");

                return new UpdateInfo
                {
                    HasUpdate = false,
                    Error = ex.Message,
                    CurrentVersion = GetCurrentVersion()
                };
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(IProgress<int>? progress = null)
        {
            try
            {
                using var mgr = new UpdateManager(_updateUrl);

                var update = await mgr.CheckForUpdate();

                if (update.ReleasesToApply.Count == 0)
                    return false;

                await mgr.DownloadReleases(update.ReleasesToApply, p =>
                {
                    progress?.Report(p);
                });

                await mgr.ApplyReleases(update);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Install error: {ex.Message}");
                return false;
            }
        }

        public static void RestartApp()
        {
            UpdateManager.RestartApp();
        }
    }

    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string Version { get; set; }
        public string CurrentVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string Error { get; set; }
    }
}