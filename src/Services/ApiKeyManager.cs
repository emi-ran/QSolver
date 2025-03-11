using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace QSolver
{
    public class ApiKey
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ApiKeyManager
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QSolver",
            "apikeys.json");

        private static readonly Random random = new Random();
        private static List<ApiKey> apiKeys = new List<ApiKey>();

        static ApiKeyManager()
        {
            LoadApiKeys();
        }

        public static List<ApiKey> GetApiKeys()
        {
            return apiKeys;
        }

        public static void AddApiKey(ApiKey apiKey)
        {
            apiKeys.Add(apiKey);
            SaveApiKeys();
        }

        public static void RemoveApiKey(ApiKey apiKey)
        {
            apiKeys.Remove(apiKey);
            SaveApiKeys();
        }

        public static void UpdateApiKey(int index, ApiKey apiKey)
        {
            if (index >= 0 && index < apiKeys.Count)
            {
                apiKeys[index] = apiKey;
                SaveApiKeys();
            }
        }

        public static string GetRandomApiKey()
        {
            if (apiKeys.Count == 0)
            {
                // Varsayılan API anahtarı (boş olabilir)
                return string.Empty;
            }

            int randomIndex = random.Next(apiKeys.Count);
            return apiKeys[randomIndex].Key;
        }

        private static void LoadApiKeys()
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
                    apiKeys = JsonSerializer.Deserialize<List<ApiKey>>(json) ?? new List<ApiKey>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"API anahtarları yüklenirken hata oluştu: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                apiKeys = new List<ApiKey>();
            }
        }

        private static void SaveApiKeys()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(apiKeys, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"API anahtarları kaydedilirken hata oluştu: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}