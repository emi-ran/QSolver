using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;
using QSolver.Helpers;
using QSolver.Models;

using QSolver.Services;

namespace QSolver
{
    public class GeminiRequest
    {
        public required Content[] contents { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GenerationConfig? generationConfig { get; set; }
    }

    public class GenerationConfig
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? responseMimeType { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? responseSchema { get; set; }
    }

    public class Content
    {
        public required Part[] parts { get; set; }
    }

    public class Part
    {
        public string? text { get; set; }
        public InlineData? inline_data { get; set; }
    }

    public class InlineData
    {
        public required string mime_type { get; set; }
        public required string data { get; set; }
    }

    public class GeminiService
    {
        private string apiKey;
        private readonly HttpClient httpClient;
        private const string API_URL_BASE = "https://generativelanguage.googleapis.com/v1beta/models";

        public GeminiService(string? apiKey)
        {
            LogHelper.LogInfo("GeminiService başlatılıyor");
            this.apiKey = string.IsNullOrEmpty(apiKey) ?
                ApiKeyManager.GetRandomApiKey() :
                apiKey;

            httpClient = new HttpClient();
        }

        private void UpdateApiKey()
        {
            string newKey = ApiKeyManager.GetRandomApiKey();
            if (!string.IsNullOrEmpty(newKey))
            {
                apiKey = newKey;
                LogHelper.LogDebug("API anahtarı güncellendi");
            }
        }

        /// <summary>
        /// OCR yanıtı için JSON schema döndürür
        /// </summary>
        private static object GetOcrSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Görselde tespit edilen metin. Her satırı newline (\\n) karakteri ile ayır. Şıklar ve paragraflar ayrı satırlarda olmalı. Metin yoksa boş string." },
                    hasText = new { type = "boolean", description = "Görselde metin var mı?" }
                },
                required = new[] { "text", "hasText" },
                propertyOrdering = new[] { "text", "hasText" }
            };
        }

        /// <summary>
        /// Soru çözümü yanıtı için JSON schema döndürür
        /// </summary>
        private static object GetSolutionSchema(List<string> availableLectures)
        {
            string lectureDescription = availableLectures.Count > 0
                ? $"{LocalizationService.Get("Result.Lecture", "")} [{string.Join(", ", availableLectures)}]. " + (LocalizationService.IsTurkish ? "Bu derslerden biri eşleşiyorsa onu yaz, yoksa yeni ders adı yaz." : "If matches write it, else write new lecture name.")
                : LocalizationService.IsTurkish ? "Sorunun dersi (örn: Türkçe, Matematik, Fizik...)" : "Lecture of the question (e.g. Math, Physics...)";

            return new
            {
                type = "object",
                properties = new
                {
                    lecture = new { type = "string", description = lectureDescription },
                    title = new { type = "string", description = "Sorunun kısa başlığı (max 50 karakter), örn: 'Matematik: Türev', 'Fizik: Hareket'" },
                    explanation = new { type = "string", description = "Çözümün markdown formatında adım adım açıklaması" },
                    solved = new { type = "boolean", description = "Sorunun başarıyla çözülüp çözülemediği" },
                    answers = new { type = "string", description = "Cevap(lar) - tek soru için 'A', çoklu sorular için soru numarasına göre sıralı 'A,B,C' formatında" }
                },
                required = new[] { "lecture", "title", "explanation", "solved", "answers" },
                propertyOrdering = new[] { "lecture", "title", "explanation", "solved", "answers" }
            };
        }

        /// <summary>
        /// Turbo Mode için minimal schema - sadece cevap ve başlık
        /// </summary>
        private static object GetTurboSchema(List<string> availableLectures)
        {
            string lectureDescription = availableLectures.Count > 0
                ? $"{LocalizationService.Get("Result.Lecture", "")} [{string.Join(", ", availableLectures)}]. " + (LocalizationService.IsTurkish ? "Eşleşiyorsa seç, yoksa yeni yaz." : "Select if matches, else write new.")
                : LocalizationService.IsTurkish ? "Sorunun dersi (Türkçe, Matematik, Fizik vb.)" : "Lecture of the question (Math, Physics etc.)";

            return new
            {
                type = "object",
                properties = new
                {
                    lecture = new { type = "string", description = lectureDescription },
                    title = new { type = "string", description = "Sorunun kısa başlığı (max 30 karakter)" },
                    solved = new { type = "boolean", description = "Çözüldü mü?" },
                    answers = new { type = "string", description = "Cevap(lar) - 'A' veya 'A,B,C'" }
                },
                required = new[] { "lecture", "title", "solved", "answers" },
                propertyOrdering = new[] { "lecture", "title", "solved", "answers" }
            };
        }

        public async Task<string> AnalyzeImage(string base64Image)
        {
            try
            {
                LogHelper.LogInfo("Görsel analiz ediliyor...");
                List<string> triedKeys = new List<string>();
                Exception? lastException = null;

                // Tüm API anahtarlarını al
                var allApiKeys = ApiKeyManager.GetAllApiKeys();
                if (allApiKeys.Count == 0)
                {
                    throw new Exception("Kullanılabilir API anahtarı bulunamadı");
                }

                // Her API anahtarı için deneme yap
                foreach (var currentKey in allApiKeys)
                {
                    if (triedKeys.Contains(currentKey)) continue;

                    try
                    {
                        apiKey = currentKey;
                        LogHelper.LogDebug($"API anahtarı deneniyor: {currentKey.Substring(0, 5)}...");

                        // API isteği için JSON hazırla (Structured Output ile)
                        var request = new GeminiRequest
                        {
                            contents = new[]
                            {
                                new Content
                                {
                                    parts = new[]
                                    {
                                        new Part { text = LocalizationService.Get("Prompts.Analysis") },
                                        new Part
                                        {
                                            inline_data = new InlineData
                                            {
                                                mime_type = "image/png",
                                                data = base64Image
                                            }
                                        }
                                    }
                                }
                            },
                            generationConfig = new GenerationConfig
                            {
                                responseMimeType = "application/json",
                                responseSchema = GetOcrSchema()
                            }
                        };

                        string jsonRequest = JsonSerializer.Serialize(request);
                        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                        // API'ye istek gönder
                        var selectedModel = SettingsService.GetSelectedModel();
                        var response = await httpClient.PostAsync($"{API_URL_BASE}/{selectedModel}:generateContent?key={apiKey}", content);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            LogHelper.LogError($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}");

                            // Kota hatası ise diğer anahtarı dene
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                triedKeys.Add(currentKey);
                                lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                                continue;
                            }

                            return $"API hatası: HTTP {(int)response.StatusCode} - {errorContent}";
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        LogHelper.LogDebug($"Gemini OCR yanıtı: {jsonResponse}");

                        // Structured output yanıtını işle
                        using JsonDocument document = JsonDocument.Parse(jsonResponse);
                        var root = document.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            string structuredJson = textElement.GetString() ?? "{}";
                            var ocrResult = JsonSerializer.Deserialize<OcrResponse>(structuredJson);

                            if (ocrResult != null && ocrResult.hasText)
                            {
                                LogHelper.LogInfo($"Görsel analiz sonucu: {ocrResult.text}");
                                return ocrResult.text;
                            }
                            else
                            {
                                LogHelper.LogInfo("Görselde metin bulunamadı");
                                return "NO_TEXT_FOUND";
                            }
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return "Yanıt işlenemedi. API yanıtı beklenen formatta değil.";
                    }
                    catch (Exception ex)
                    {
                        triedKeys.Add(currentKey);
                        lastException = ex;
                        LogHelper.LogError($"API anahtarı ile hata oluştu: {currentKey.Substring(0, 5)}...", ex);
                        continue;
                    }
                }

                // Tüm anahtarlar denenmiş ve başarısız olmuşsa
                if (lastException != null)
                {
                    throw lastException;
                }

                throw new Exception("Tüm API anahtarları denenmiş ve başarısız olmuş");
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Görsel analizi sırasında hata oluştu", ex);
                throw;
            }
        }

        public async Task<(string fullResponse, string answer, string lecture)> SolveQuestion(string questionText)
        {
            try
            {
                LogHelper.LogInfo("Soru çözülüyor...");
                List<string> triedKeys = new List<string>();
                Exception? lastException = null;

                // Mevcut dersleri al
                var availableLectures = SolutionHistoryService.GetAvailableLectures();

                // Tüm API anahtarlarını al
                var allApiKeys = ApiKeyManager.GetAllApiKeys();
                if (allApiKeys.Count == 0)
                {
                    throw new Exception("Kullanılabilir API anahtarı bulunamadı");
                }

                // Her API anahtarı için deneme yap
                foreach (var currentKey in allApiKeys)
                {
                    if (triedKeys.Contains(currentKey)) continue;

                    try
                    {
                        apiKey = currentKey;
                        LogHelper.LogDebug($"API anahtarı deneniyor: {currentKey.Substring(0, 5)}...");

                        // Prompt'a mevcut dersleri ekle
                        string lectureHint = availableLectures.Count > 0
                            ? $"\n\nMevcut dersler: {string.Join(", ", availableLectures)}. Bu derslerden biri eşleşiyorsa onu kullan, yoksa yeni ders adı belirle."
                            : "";

                        // API isteği için JSON hazırla (Structured Output ile)
                        var request = new GeminiRequest
                        {
                            contents = new[]
                            {
                                new Content
                                {
                                    parts = new[]
                                    {
                                        new Part
                                        {
                                            text = LocalizationService.Get("Prompts.Solve") + lectureHint + "\n\n" + (LocalizationService.IsTurkish ? "Metin:" : "Text:") + "\n" + questionText
                                        }
                                    }
                                }
                            },
                            generationConfig = new GenerationConfig
                            {
                                responseMimeType = "application/json",
                                responseSchema = GetSolutionSchema(availableLectures)
                            }
                        };

                        string jsonRequest = JsonSerializer.Serialize(request);
                        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                        // API'ye istek gönder
                        var selectedModel = SettingsService.GetSelectedModel();
                        var response = await httpClient.PostAsync($"{API_URL_BASE}/{selectedModel}:generateContent?key={apiKey}", content);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            LogHelper.LogError($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}");

                            // Kota hatası ise diğer anahtarı dene
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                triedKeys.Add(currentKey);
                                lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                                continue;
                            }

                            return ($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}", "Hata", "");
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        LogHelper.LogDebug($"Gemini Solver yanıtı: {jsonResponse}");

                        // Structured output yanıtını işle
                        using JsonDocument document = JsonDocument.Parse(jsonResponse);
                        var root = document.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            string structuredJson = textElement.GetString() ?? "{}";
                            var solutionResult = JsonSerializer.Deserialize<SolutionResponse>(structuredJson);

                            if (solutionResult != null)
                            {
                                string fullResponse = solutionResult.explanation;
                                string answer = solutionResult.solved ? solutionResult.answers.ToUpper() : "Çözülemedi";
                                string lecture = solutionResult.lecture ?? "";
                                LogHelper.LogInfo($"Soru çözüm sonucu: {fullResponse}");
                                LogHelper.LogInfo($"Çıkarılan cevap: {answer}");
                                LogHelper.LogInfo($"Ders: {lecture}");
                                return (fullResponse, answer, lecture);
                            }
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return ("Yanıt işlenemedi.", "Hata", "");
                    }
                    catch (Exception ex)
                    {
                        triedKeys.Add(currentKey);
                        lastException = ex;
                        LogHelper.LogError($"API anahtarı ile hata oluştu: {currentKey.Substring(0, 5)}...", ex);
                        continue;
                    }
                }

                // Tüm anahtarlar denenmiş ve başarısız olmuşsa
                if (lastException != null)
                {
                    throw lastException;
                }

                throw new Exception("Tüm API anahtarları denenmiş ve başarısız olmuş");
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Soru çözümü sırasında hata oluştu", ex);
                throw;
            }
        }

        public async Task<(string fullResponse, string answer, string title, string lecture)> SolveQuestionDirectly(string base64Image)
        {
            try
            {
                LogHelper.LogInfo("Soru görsel üzerinden doğrudan çözülüyor...");
                List<string> triedKeys = new List<string>();
                Exception? lastException = null;

                // Mevcut dersleri al
                var availableLectures = SolutionHistoryService.GetAvailableLectures();

                // Tüm API anahtarlarını al
                var allApiKeys = ApiKeyManager.GetAllApiKeys();
                if (allApiKeys.Count == 0)
                {
                    throw new Exception("Kullanılabilir API anahtarı bulunamadı");
                }

                // Her API anahtarı için deneme yap
                foreach (var currentKey in allApiKeys)
                {
                    if (triedKeys.Contains(currentKey)) continue;

                    try
                    {
                        apiKey = currentKey;
                        LogHelper.LogDebug($"API anahtarı deneniyor: {currentKey.Substring(0, 5)}...");

                        // Prompt'a mevcut dersleri ekle
                        string lectureHint = availableLectures.Count > 0
                            ? $"\n- lecture: ders adı. Mevcut: [{string.Join(", ", availableLectures)}]. Eşleşiyorsa seç, yoksa yeni yaz."
                            : "\n- lecture: ders adı (Türkçe, Matematik, Fizik vb.)";

                        // API isteği için JSON hazırla (Turbo Mode - Minimal Schema)
                        var request = new GeminiRequest
                        {
                            contents = new[]
                            {
                                new Content
                                {
                                    parts = new[]
                                    {
                                        new Part
                                        {
                                            text = LocalizationService.Get("Prompts.DirectSolve") + lectureHint
                                        },
                                        new Part
                                        {
                                            inline_data = new InlineData
                                            {
                                                mime_type = "image/png",
                                                data = base64Image
                                            }
                                        }
                                    }
                                }
                            },
                            generationConfig = new GenerationConfig
                            {
                                responseMimeType = "application/json",
                                responseSchema = GetTurboSchema(availableLectures)
                            }
                        };

                        string jsonRequest = JsonSerializer.Serialize(request);
                        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                        // API'ye istek gönder
                        var selectedModel = SettingsService.GetSelectedModel();
                        var response = await httpClient.PostAsync($"{API_URL_BASE}/{selectedModel}:generateContent?key={apiKey}", content);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            LogHelper.LogError($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}");

                            // Kota hatası ise diğer anahtarı dene
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                triedKeys.Add(currentKey);
                                lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                                continue;
                            }

                            return ($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}", "Hata", "API Hatası", "");
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        LogHelper.LogDebug($"Gemini Turbo yanıtı: {jsonResponse}");

                        // Turbo structured output yanıtını işle
                        using JsonDocument document = JsonDocument.Parse(jsonResponse);
                        var root = document.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            string structuredJson = textElement.GetString() ?? "{}";
                            var turboResult = JsonSerializer.Deserialize<TurboResponse>(structuredJson);

                            if (turboResult != null)
                            {
                                // Turbo modda explanation yok, sadece "Turbo Mode" yazılır
                                string fullResponse = "Turbo Mode - Hızlı çözüm";
                                string answer = turboResult.solved ? turboResult.answers.ToUpper() : "Çözülemedi";
                                string title = !string.IsNullOrEmpty(turboResult.title)
                                    ? (turboResult.title.Length > 50 ? turboResult.title.Substring(0, 47) + "..." : turboResult.title)
                                    : $"Turbo - {DateTime.Now:HH:mm}";
                                string lecture = turboResult.lecture ?? "";
                                LogHelper.LogInfo($"Turbo çözüm: cevap={answer}, başlık={title}, ders={lecture}");
                                return (fullResponse, answer, title, lecture);
                            }
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return ("Yanıt işlenemedi.", "Hata", "Çözülemeyen Soru", "");
                    }
                    catch (Exception ex)
                    {
                        triedKeys.Add(currentKey);
                        lastException = ex;
                        LogHelper.LogError($"API anahtarı ile hata oluştu: {currentKey.Substring(0, 5)}...", ex);
                        continue;
                    }
                }

                // Tüm anahtarlar denenmiş ve başarısız olmuşsa
                if (lastException != null)
                {
                    throw lastException;
                }

                throw new Exception("Tüm API anahtarları denenmiş ve başarısız olmuş");
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Doğrudan soru çözümü sırasında hata oluştu", ex);
                throw;
            }
        }

        public async Task<string> GenerateQuestionTitle(string questionText)
        {
            try
            {
                LogHelper.LogInfo("Soru başlığı üretiliyor...");

                // API isteği için JSON hazırla
                var request = new GeminiRequest
                {
                    contents = new[]
                    {
                        new Content
                        {
                            parts = new[]
                            {
                                new Part
                                {
                                    text = LocalizationService.Get("Prompts.TitleGen", questionText)
                                }
                            }
                        }
                    }
                };

                string jsonRequest = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // API'ye istek gönder
                var selectedModel = SettingsService.GetSelectedModel();
                var response = await httpClient.PostAsync($"{API_URL_BASE}/{selectedModel}:generateContent?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    LogHelper.LogError($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}");
                    return $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                LogHelper.LogDebug($"Gemini Title yanıtı: {jsonResponse}");

                var cleanTitle = CleanTitle(jsonResponse);

                // Başlık çok uzunsa kısalt
                if (cleanTitle.Length > 50)
                {
                    cleanTitle = cleanTitle.Substring(0, 47) + "...";
                }

                LogHelper.LogInfo($"Üretilen başlık: {cleanTitle}");
                return cleanTitle;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Title generation error: {ex.Message}");
                // Hata durumunda varsayılan başlık döndür
                return $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
        }

        private string CleanTitle(string response)
        {
            try
            {
                // JSON response'u parse et
                var jsonDoc = JsonDocument.Parse(response);
                var content = jsonDoc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                // Gereksiz karakterleri temizle
                content = content.Trim()
                    .Replace("\"", "")
                    .Replace("'", "")
                    .Replace("\n", " ")
                    .Replace("\r", " ");

                // JSON formatında gelirse parse et
                if (content.StartsWith("{") && content.Contains("title"))
                {
                    try
                    {
                        var titleJson = JsonDocument.Parse(content);
                        if (titleJson.RootElement.TryGetProperty("title", out var titleElement))
                        {
                            content = titleElement.GetString() ?? content;
                        }
                    }
                    catch
                    {
                        // JSON parse edilemezse orijinal içeriği kullan
                    }
                }

                return string.IsNullOrWhiteSpace(content) ? $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}" : content;
            }
            catch
            {
                return $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
        }
    }
}