using System;
using System.IO;

namespace QSolver
{
    public class AppSettings
    {
        public string SelectedModel { get; set; } = "gemini-2.5-flash";
        public bool TurboMode { get; set; } = false;
        public string? DismissedUpdateVersion { get; set; } = null;
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

        public static string[] GetAvailableModels()
        {
            return new string[]
            {
                "gemini-3-pro-preview",    // En akıllı model - Multimodal ve agentic
                "gemini-2.5-flash",        // Hızlı ve akıllı - Fiyat/performans dengesi
                "gemini-2.5-flash-lite",   // Ultra hızlı - Maliyet odaklı
                "gemini-2.5-pro",          // Gelişmiş düşünme - Kompleks problemler
                "gemini-2.0-flash",        // 2. nesil workhorse
                "gemini-2.0-flash-lite"    // 2. nesil hızlı model
            };
        }
    }
}
