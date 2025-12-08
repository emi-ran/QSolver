using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using QSolver.Helpers;

namespace QSolver.Services
{
    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
        public bool IsNewerVersion { get; set; } = false;
    }

    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/emi-ran/QSolver/releases/latest";
        private static readonly HttpClient httpClient = new HttpClient();
        private static UpdateInfo? cachedUpdateInfo = null;

        static UpdateService()
        {
            // GitHub API User-Agent gerektirir
            httpClient.DefaultRequestHeaders.Add("User-Agent", "QSolver-UpdateChecker");
        }

        /// <summary>
        /// Uygulamanın mevcut sürümünü döndürür
        /// </summary>
        public static string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            return "1.0.0";
        }

        /// <summary>
        /// GitHub'dan son sürümü kontrol eder
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                LogHelper.LogInfo("Güncelleme kontrolü başlatılıyor...");
                var response = await httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    LogHelper.LogWarning($"GitHub API yanıtı başarısız: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var htmlUrl = root.GetProperty("html_url").GetString() ?? "";

                // "v1.5.4" formatından "1.5.4" formatına çevir
                var latestVersion = tagName.TrimStart('v', 'V');
                var currentVersion = GetCurrentVersion();

                var updateInfo = new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    ReleaseUrl = htmlUrl,
                    TagName = tagName,
                    IsNewerVersion = IsNewerVersion(currentVersion, latestVersion)
                };

                cachedUpdateInfo = updateInfo;

                if (updateInfo.IsNewerVersion)
                {
                    LogHelper.LogInfo($"Yeni sürüm mevcut: {latestVersion} (Mevcut: {currentVersion})");
                }
                else
                {
                    LogHelper.LogInfo($"Güncel sürüm kullanılıyor: {currentVersion}");
                }

                return updateInfo;
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Güncelleme kontrolü sırasında hata oluştu", ex);
                return null;
            }
        }

        /// <summary>
        /// Önbellekteki güncelleme bilgisini döndürür
        /// </summary>
        public static UpdateInfo? GetCachedUpdateInfo()
        {
            return cachedUpdateInfo;
        }

        /// <summary>
        /// Güncelleme mevcut mu kontrol eder (önbellekten)
        /// </summary>
        public static bool IsUpdateAvailable()
        {
            return cachedUpdateInfo?.IsNewerVersion ?? false;
        }

        /// <summary>
        /// İki sürümü karşılaştırır (semantic versioning)
        /// </summary>
        private static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            try
            {
                var current = ParseVersion(currentVersion);
                var latest = ParseVersion(latestVersion);

                // Major karşılaştırma
                if (latest.Major > current.Major) return true;
                if (latest.Major < current.Major) return false;

                // Minor karşılaştırma
                if (latest.Minor > current.Minor) return true;
                if (latest.Minor < current.Minor) return false;

                // Patch karşılaştırma
                if (latest.Patch > current.Patch) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sürüm stringini parse eder
        /// </summary>
        private static (int Major, int Minor, int Patch) ParseVersion(string version)
        {
            var parts = version.Split('.');
            int major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            int minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            int patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            return (major, minor, patch);
        }

        /// <summary>
        /// Bu sürümün daha önce reddedilip reddedilmediğini kontrol eder
        /// </summary>
        public static bool IsUpdateDismissed(string version)
        {
            var dismissedVersion = SettingsService.GetSettings().DismissedUpdateVersion;
            return !string.IsNullOrEmpty(dismissedVersion) && dismissedVersion == version;
        }

        /// <summary>
        /// Güncellemeyi reddeder ve ayarlara kaydeder
        /// </summary>
        public static void DismissUpdate(string version)
        {
            LogHelper.LogInfo($"Güncelleme reddedildi: {version}");
            var settings = SettingsService.GetSettings();
            settings.DismissedUpdateVersion = version;
            SettingsService.UpdateSettings(settings);
        }

        /// <summary>
        /// Release sayfasını tarayıcıda açar
        /// </summary>
        public static void OpenReleasePage(string url)
        {
            try
            {
                LogHelper.LogInfo($"Release sayfası açılıyor: {url}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Release sayfası açılamadı", ex);
            }
        }
    }
}
