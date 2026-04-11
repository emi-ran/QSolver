using System;
using System.Threading.Tasks;
using Google.GenAI;
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
                var client = new Client(apiKey: apiKey);
                // Model listesi çekerek API key'i doğrula
                var pager = await client.Models.ListAsync();
                // İlk modeli almayı dene - başarılıysa key geçerli
                await foreach (var model in pager)
                {
                    // En az bir model geldiyse key geçerlidir
                    result.Status = ApiKeyStatus.Valid;
                    result.Message = LocalizationService.Get("ApiKey.Status.Valid");
                    return result;
                }

                // Model gelmediyse de key geçerlidir ama sonuç boş
                result.Status = ApiKeyStatus.Valid;
                result.Message = LocalizationService.Get("ApiKey.Status.Valid");
            }
            catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
            {
                result.Status = ApiKeyStatus.RateLimit;
                result.Message = LocalizationService.Get("ApiKey.Status.RateLimit");
            }
            catch (Exception ex) when (ex.Message.Contains("API_KEY_INVALID") || ex.Message.Contains("invalid") ||
                                       ex.Message.Contains("403") || ex.Message.Contains("401"))
            {
                result.Status = ApiKeyStatus.Invalid;
                result.Message = LocalizationService.Get("ApiKey.Status.Invalid");
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
