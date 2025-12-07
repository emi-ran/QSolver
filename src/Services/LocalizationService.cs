using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using QSolver.Helpers;

namespace QSolver.Services
{
    public static class LocalizationService
    {
        private const string REGISTRY_KEY = @"SOFTWARE\QSolver";
        private const string REGISTRY_VALUE = "Language";
        private const string DEFAULT_LANGUAGE = "en-US";

        private static Dictionary<string, string> _currentLanguage = new();
        private static string _currentLanguageCode = DEFAULT_LANGUAGE;

        public static string CurrentLanguageCode => _currentLanguageCode;

        public static readonly string[] AvailableLanguages = { "en-US", "tr-TR" };
        public static readonly Dictionary<string, string> LanguageNames = new()
        {
            { "en-US", "English" },
            { "tr-TR", "Türkçe" }
        };

        static LocalizationService()
        {
            Initialize();
        }

        /// <summary>
        /// Dil servisini başlatır
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Önce registry'den dil tercihini kontrol et
                string? savedLanguage = GetLanguageFromRegistry();

                if (!string.IsNullOrEmpty(savedLanguage) && IsValidLanguage(savedLanguage))
                {
                    _currentLanguageCode = savedLanguage;
                    LogHelper.LogInfo($"Registry'den dil yüklendi: {savedLanguage}");
                }
                else
                {
                    // Registry'de kayıtlı dil yoksa sistem dilini kullan
                    string systemLanguage = CultureInfo.CurrentUICulture.Name;

                    // Sistem dili destekleniyor mu kontrol et
                    if (IsValidLanguage(systemLanguage))
                    {
                        _currentLanguageCode = systemLanguage;
                        LogHelper.LogInfo($"Sistem dili kullanılıyor: {systemLanguage}");
                    }
                    else if (systemLanguage.StartsWith("tr"))
                    {
                        // Türkçe varyantları için tr-TR kullan
                        _currentLanguageCode = "tr-TR";
                        LogHelper.LogInfo($"Sistem dili Türkçe varyantı, tr-TR kullanılıyor");
                    }
                    else
                    {
                        // Varsayılan olarak İngilizce
                        _currentLanguageCode = DEFAULT_LANGUAGE;
                        LogHelper.LogInfo($"Sistem dili desteklenmiyor, varsayılan dil kullanılıyor: {DEFAULT_LANGUAGE}");
                    }
                }

                // Dil dosyasını yükle
                LoadLanguageFile(_currentLanguageCode);
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Lokalizasyon servisi başlatılırken hata oluştu", ex);
                _currentLanguageCode = DEFAULT_LANGUAGE;
                LoadEmbeddedFallback();
            }
        }

        /// <summary>
        /// Dil dosyasını yükler
        /// </summary>
        private static void LoadLanguageFile(string languageCode)
        {
            try
            {
                string languageFilePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Languages",
                    $"{languageCode}.json");

                if (File.Exists(languageFilePath))
                {
                    string jsonContent = File.ReadAllText(languageFilePath);
                    var loadedLanguage = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                    if (loadedLanguage != null)
                    {
                        _currentLanguage = loadedLanguage;
                        LogHelper.LogInfo($"Dil dosyası yüklendi: {languageFilePath}");
                        return;
                    }
                }

                LogHelper.LogWarning($"Dil dosyası bulunamadı: {languageFilePath}");
                LoadEmbeddedFallback();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Dil dosyası yüklenirken hata oluştu: {languageCode}", ex);
                LoadEmbeddedFallback();
            }
        }

        /// <summary>
        /// Gömülü varsayılan dil verilerini yükler
        /// </summary>
        private static void LoadEmbeddedFallback()
        {
            // Varsayılan İngilizce çeviriler
            _currentLanguage = new Dictionary<string, string>
            {
                // Common
                { "Common.Error", "Error" },
                { "Common.Warning", "Warning" },
                { "Common.Info", "Info" },
                { "Common.Confirm", "Confirm" },
                { "Common.Yes", "Yes" },
                { "Common.No", "No" },
                { "Common.OK", "OK" },
                { "Common.Cancel", "Cancel" },
                { "Common.Save", "Save" },
                { "Common.Close", "Close" },
                { "Common.Delete", "Delete" },
                { "Common.Edit", "Edit" },
                { "Common.Add", "Add" },
                { "Common.Search", "Search" },
                { "Common.Refresh", "Refresh" },
                { "Common.Clear", "Clear" },
                { "Common.Loading", "Loading..." },
                
                // App
                { "App.Started", "Program started (Shortcut: Ctrl+Shift+Q)" },
                { "App.NoApiKey", "No API key added yet. Please add a key from the API Keys menu." },
                { "App.Exit", "Exiting program..." },
                
                // Tray menu
                { "Tray.Capture", "🔍 Select Question" },
                { "Tray.History", "📚 Solution History" },
                { "Tray.Settings", "⚙️ Settings" },
                { "Tray.ApiKeys", "🔑 API Keys" },
                { "Tray.Logs", "📋 View Logs" },
                { "Tray.Exit", "❌ Exit" },
                
                // Settings related strings
                { "Settings.Title", "QSolver - Settings" },
                { "Settings.ModelSelection", "Model Selection:" },
                { "Settings.LanguageSelection", "Language:" },
                { "Settings.TurboMode", "Turbo Mode (Answer only, no solution steps - Fast)" },
                { "Settings.Saved", "Settings saved successfully!" },
                { "Settings.SelectModel", "Please select a model!" },
                
                // API Keys
                { "ApiKey.Title", "API Keys" },
                { "ApiKey.AddTitle", "Add API Key" },
                { "ApiKey.EditTitle", "Edit API Key" },
                { "ApiKey.KeyLabel", "API Key:" },
                { "ApiKey.DescLabel", "Description:" },
                { "ApiKey.EmptyError", "API key cannot be empty." },
                { "ApiKey.ValidateButton", "Check Validity" },
                { "ApiKey.Checking", "Checking..." },
                { "ApiKey.CheckComplete", "API keys checked." },
                { "ApiKey.DeleteConfirm", "Are you sure you want to delete the selected API key?" },
                { "ApiKey.Status.Valid", "Valid" },
                { "ApiKey.Status.Invalid", "Invalid" },
                { "ApiKey.Status.RateLimit", "Rate Limit Exceeded" },
                
                // Result
                { "Result.Title", "Solution" },
                { "Result.Analyze", "Analyzing Question" },
                { "Result.Solving", "Solving Question..." },
                { "Result.SolveButton", "Solve" },
                { "Result.StepsButton", "Solution Steps" },
                { "Result.NotFound", "Question not found." },
                { "Result.Answer", "Answer" },
                { "Result.Answers", "Answers" },
                { "Result.Lecture", "Lecture" },
                
                // History
                { "History.Title", "QSolver - Solution History" },
                { "History.Total", "Total: {0} solutions" },
                { "History.Found", "Found: {0} solutions" },
                { "History.ClearAll", "Clear All" },
                { "History.ViewSteps", "View Solution Steps" },
                { "History.SelectPrompt", "Select a question..." },
                { "History.DeleteConfirm", "Are you sure you want to delete this solution?" },
                { "History.ClearAllConfirm", "Are you sure you want to delete ALL history?" },
                
                // Viewer & QuestionEdit
                { "Viewer.Title", "QSolver - Image Viewer" },
                { "Viewer.Zoom", "Zoom: {0}%" },
                { "QuestionEdit.Title", "Edit Question" },
                { "QuestionEdit.EmptyError", "Question text cannot be empty." },
                
                // Logs
                { "Logs.Title", "Log Viewer" },
                { "Logs.Clear", "Clear Logs" },
                { "Logs.Refresh", "Refresh" },
                { "Logs.ClearConfirm", "Are you sure you want to clear logs?" }
            };

            LogHelper.LogInfo("Gömülü varsayılan dil verileri yüklendi");
        }

        /// <summary>
        /// Registry'den dil tercihini okur
        /// </summary>
        private static string? GetLanguageFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
                return key?.GetValue(REGISTRY_VALUE) as string;
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Registry'den dil okunamadı", ex);
                return null;
            }
        }

        /// <summary>
        /// Dil tercihini Registry'ye kaydeder
        /// </summary>
        public static void SaveLanguageToRegistry(string languageCode)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);
                key?.SetValue(REGISTRY_VALUE, languageCode);
                LogHelper.LogInfo($"Dil tercihi Registry'ye kaydedildi: {languageCode}");
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Dil tercihi Registry'ye kaydedilemedi", ex);
            }
        }

        /// <summary>
        /// Dil kodunun geçerli olup olmadığını kontrol eder
        /// </summary>
        private static bool IsValidLanguage(string languageCode)
        {
            return Array.Exists(AvailableLanguages, lang => lang.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Dili değiştirir
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            if (!IsValidLanguage(languageCode))
            {
                LogHelper.LogWarning($"Geçersiz dil kodu: {languageCode}");
                return;
            }

            _currentLanguageCode = languageCode;
            SaveLanguageToRegistry(languageCode);
            LoadLanguageFile(languageCode);

            LogHelper.LogInfo($"Dil değiştirildi: {languageCode}");
        }

        /// <summary>
        /// Belirtilen anahtar için çeviriyi döndürür
        /// </summary>
        public static string Get(string key)
        {
            if (_currentLanguage.TryGetValue(key, out var value))
            {
                return value;
            }

            // Anahtar bulunamadığında debug için loglama
            LogHelper.LogDebug($"Çeviri bulunamadı: {key}");
            return $"[{key}]";
        }

        /// <summary>
        /// Belirtilen anahtar için çeviriyi döndürür, {0}, {1} vb. parametreleri değiştirir
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        /// <summary>
        /// Mevcut dilin Türkçe olup olmadığını kontrol eder
        /// </summary>
        public static bool IsTurkish => _currentLanguageCode.StartsWith("tr");

        /// <summary>
        /// Mevcut dilin İngilizce olup olmadığını kontrol eder
        /// </summary>
        public static bool IsEnglish => _currentLanguageCode.StartsWith("en");
    }
}
