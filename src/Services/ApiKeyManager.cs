using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QSolver
{
    public class ApiKey
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ApiKeyManager : JsonConfigService<List<ApiKey>>
    {
        private static readonly ApiKeyManager Instance = new();
        private static readonly Random RandomInstance = new();

        protected override string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QSolver",
            "apikeys.json");

        protected override string LoadErrorKey => "ApiKey.LoadError";
        protected override string SaveErrorKey => "ApiKey.SaveError";

        static ApiKeyManager()
        {
            Instance.LoadData();
        }

        public static List<ApiKey> GetApiKeys() => Instance.Data;

        public static void AddApiKey(ApiKey apiKey)
        {
            Instance.Data.Add(apiKey);
            Instance.SaveData();
        }

        public static void RemoveApiKey(ApiKey apiKey)
        {
            Instance.Data.Remove(apiKey);
            Instance.SaveData();
        }

        public static void UpdateApiKey(int index, ApiKey apiKey)
        {
            if (index >= 0 && index < Instance.Data.Count)
            {
                Instance.Data[index] = apiKey;
                Instance.SaveData();
            }
        }

        public static string GetRandomApiKey()
        {
            if (Instance.Data.Count == 0)
            {
                return string.Empty;
            }

            int randomIndex = RandomInstance.Next(Instance.Data.Count);
            return Instance.Data[randomIndex].Key;
        }

        public static List<string> GetAllApiKeys()
        {
            return Instance.Data.Select(k => k.Key).ToList();
        }
    }
}