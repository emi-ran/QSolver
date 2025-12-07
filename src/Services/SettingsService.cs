using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace QSolver
{
    public class AppSettings
    {
        public string SelectedModel { get; set; } = "gemini-2.5-flash";
        public bool TurboMode { get; set; } = false;
    }

    public class SettingsService
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QSolver",
            "settings.json");

        private static AppSettings settings = new AppSettings();

        static SettingsService()
        {
            LoadSettings();
        }

        public static AppSettings GetSettings()
        {
            return settings;
        }

        public static void UpdateSettings(AppSettings newSettings)
        {
            settings = newSettings;
            SaveSettings();
        }

        public static string GetSelectedModel()
        {
            return settings.SelectedModel;
        }

        public static void SetSelectedModel(string model)
        {
            settings.SelectedModel = model;
            SaveSettings();
        }

        public static bool GetTurboMode()
        {
            return settings.TurboMode;
        }

        public static void SetTurboMode(bool enabled)
        {
            settings.TurboMode = enabled;
            SaveSettings();
        }

        public static string[] GetAvailableModels()
        {
            return new string[]
            {
                "gemini-3-pro-preview",   // En akıllı model - Multimodal ve agentic
                "gemini-2.5-flash",        // Hızlı ve akıllı - Fiyat/performans dengesi
                "gemini-2.5-flash-lite",   // Ultra hızlı - Maliyet odaklı
                "gemini-2.5-pro",          // Gelişmiş düşünme - Kompleks problemler
                "gemini-2.0-flash",        // 2. nesil workhorse
                "gemini-2.0-flash-lite"    // 2. nesil hızlı model
            };
        }

        private static void LoadSettings()
        {
            try
            {
                // Dizin yoksa oluştur
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loadedSettings != null)
                    {
                        settings = loadedSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar yüklenirken hata oluştu: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                settings = new AppSettings();
            }
        }

        private static void SaveSettings()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken hata oluştu: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
