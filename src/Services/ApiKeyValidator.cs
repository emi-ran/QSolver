using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using QSolver.Services;

namespace QSolver
{
    public enum ApiKeyStatus
    {
        Valid,      // Geçerli
        Invalid,    // Geçersiz
        RateLimit,  // Rate limit aşıldı
        Unknown     // Bilinmeyen durum
    }

    public class ApiKeyValidationResult
    {
        public ApiKeyStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public class ApiKeyValidator
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_URL_BASE = "https://generativelanguage.googleapis.com/v1beta/models";

        public static async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey)
        {
            var result = new ApiKeyValidationResult
            {
                ApiKey = apiKey,
                Status = ApiKeyStatus.Unknown,
                Message = LocalizationService.Get("ApiKey.Checking")
            };

            try
            {
                // API key'i doğrulamak için models:list endpoint'ini kullan
                // Bu endpoint daha hafif ve sadece API key kontrolü için yeterli
                var response = await httpClient.GetAsync(
                    $"{API_URL_BASE}?key={apiKey}");

                if (response.IsSuccessStatusCode)
                {
                    result.Status = ApiKeyStatus.Valid;
                    result.Message = LocalizationService.Get("ApiKey.Status.Valid");
                    return result;
                }

                // Hata durumlarını kontrol et
                string errorContent = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    result.Status = ApiKeyStatus.RateLimit;
                    result.Message = LocalizationService.Get("ApiKey.Status.RateLimit");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // API key geçersiz olabilir
                    if (errorContent.Contains("API_KEY_INVALID") || errorContent.Contains("invalid"))
                    {
                        result.Status = ApiKeyStatus.Invalid;
                        result.Message = LocalizationService.Get("ApiKey.Status.Invalid");
                    }
                    else
                    {
                        result.Status = ApiKeyStatus.Invalid;
                        result.Message = LocalizationService.Get("Common.Error") + " (Bad Request)";
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    result.Status = ApiKeyStatus.Invalid;
                    result.Message = LocalizationService.Get("ApiKey.Status.Invalid") + " (Forbidden)";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 hatası - muhtemelen API key geçersiz veya endpoint yanlış
                    result.Status = ApiKeyStatus.Invalid;
                    result.Message = LocalizationService.Get("ApiKey.Status.Invalid") + " (404)";
                }
                else
                {
                    result.Status = ApiKeyStatus.Invalid;
                    result.Message = $"HTTP {(int)response.StatusCode}";
                }
            }
            catch (HttpRequestException ex)
            {
                result.Status = ApiKeyStatus.Unknown;
                result.Message = LocalizationService.Get("Common.Error") + ": " + ex.Message;
            }
            catch (Exception ex)
            {
                result.Status = ApiKeyStatus.Unknown;
                result.Message = LocalizationService.Get("Common.Error") + ": " + ex.Message;
            }

            return result;
        }

        public static async Task<ApiKeyValidationResult[]> ValidateAllApiKeysAsync()
        {
            var apiKeys = ApiKeyManager.GetApiKeys();
            var results = new ApiKeyValidationResult[apiKeys.Count];

            // Tüm API keylerini paralel olarak kontrol et
            var tasks = new Task<ApiKeyValidationResult>[apiKeys.Count];
            for (int i = 0; i < apiKeys.Count; i++)
            {
                tasks[i] = ValidateApiKeyAsync(apiKeys[i].Key);
            }

            results = await Task.WhenAll(tasks);
            return results;
        }
    }
}
