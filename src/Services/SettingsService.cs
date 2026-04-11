using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace QSolver
{
    public class AppSettings
    {
        public string SelectedModel { get; set; } = "gemini-2.5-flash";
        public bool TurboMode { get; set; } = false;
        public string? DismissedUpdateVersion { get; set; } = null;
        public List<string> CachedModels { get; set; } = new();
    }

    public class SettingsService : JsonConfigService<AppSettings>
    {
        private static readonly SettingsService Instance = new();

        protected override string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QSolver",
            "settings.json");

        protected override string LoadErrorKey => "Settings.LoadError";
        protected override string SaveErrorKey => "Settings.SaveError";

        static SettingsService()
        {
            Instance.LoadData();
        }

        public static AppSettings GetSettings() => Instance.Data;

        public static void UpdateSettings(AppSettings newSettings)
        {
            Instance.Data = newSettings;
            Instance.SaveData();
        }

        public static string GetSelectedModel() => Instance.Data.SelectedModel;

        public static void SetSelectedModel(string model)
        {
            Instance.Data.SelectedModel = model;
            Instance.SaveData();
        }

        public static bool GetTurboMode() => Instance.Data.TurboMode;

        public static void SetTurboMode(bool enabled)
        {
            Instance.Data.TurboMode = enabled;
            Instance.SaveData();
        }

        /// <summary>
        /// Varsayılan (fallback) model listesi - API'ye ulaşılamadığında kullanılır
        /// </summary>
        public static string[] GetFallbackModels()
        {
            return new string[]
            {
                "gemini-3-pro-preview",
                "gemini-2.5-flash",
                "gemini-2.5-flash-lite",
                "gemini-2.5-pro",
                "gemini-2.0-flash",
                "gemini-2.0-flash-lite"
            };
        }

        /// <summary>
        /// Önbelleğe alınmış modelleri döndürür, yoksa fallback
        /// </summary>
        public static List<string> GetCachedModels()
        {
            if (Instance.Data.CachedModels.Count > 0)
                return Instance.Data.CachedModels;

            return new List<string>(GetFallbackModels());
        }

        /// <summary>
        /// Model listesini önbelleğe kaydeder
        /// </summary>
        public static void SetCachedModels(List<string> models)
        {
            Instance.Data.CachedModels = models;
            Instance.SaveData();
        }
    }
}
