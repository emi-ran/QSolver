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
                ? $"Sorunun dersi. Mevcut dersler: [{string.Join(", ", availableLectures)}]. Bu derslerden biri eşleşiyorsa onu yaz, yoksa yeni ders adı yaz (örn: Türkçe, Matematik, Fizik, Kimya, Biyoloji, Tarih, Coğrafya, İngilizce, Din Kültürü)"
                : "Sorunun dersi (örn: Türkçe, Matematik, Fizik, Kimya, Biyoloji, Tarih, Coğrafya, İngilizce, Din Kültürü)";

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
                ? $"Sorunun dersi. Mevcut: [{string.Join(", ", availableLectures)}]. Eşleşiyorsa seç, yoksa yeni yaz."
                : "Sorunun dersi (Türkçe, Matematik, Fizik vb.)";

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
                                        new Part { text = "Bu görseldeki metni oku. Her satırı ve şıkkı ayrı satırda yaz (newline karakteri ile ayır). Görseldeki orijinal satır düzenini koru. Ek açıklama ekleme." },
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
                                            text = @"Sen bir soru çözme yapay zekasısın. Aşağıdaki soruyu analiz edip adım adım çöz ve JSON formatında yanıt ver.

                                                ÖNEMLİ KURALLAR:
                                                - Birden fazla soru varsa, cevapları SORU NUMARALARINA GÖRE KÜÇÜKTEN BÜYÜĞE sırala
                                                - Tek soru için answers alanına sadece şık harfini yaz (örn: 'A')
                                                - Çoklu soru için answers alanına virgülle ayırarak yaz (örn: 'A,B,C')
                                                - explanation alanında çözümü markdown formatında adım adım açıkla
                                                - title alanına kısa bir başlık yaz (max 50 karakter)
                                                - lecture alanına sorunun dersini yaz (Türkçe, Matematik, Fizik vb.)" + lectureHint + @"

                                                Soru:
                                                " + questionText
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
                                            text = @"Bu görseldeki soruyu çöz. Sadece doğru cevabı ver.
- Tek soru: 'A' gibi şık harfi
- Çoklu soru: 'A,B,C' gibi virgülle ayır (soru numarasına göre sırala)
- title: kısa başlık (max 30 karakter)" + lectureHint
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

        private string ExtractAnswer(string response)
        {
            try
            {
                // JSON formatındaki cevabı bul
                int jsonStart = response.LastIndexOf('{');
                int jsonEnd = response.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string jsonPart = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    // JSON'ı parse et
                    using JsonDocument document = JsonDocument.Parse(jsonPart);
                    var root = document.RootElement;

                    if (root.TryGetProperty("solved", out var solvedElement) &&
                        solvedElement.GetString() == "false")
                    {
                        return "Soru Çözülemedi";
                    }

                    // "answers" anahtarını kontrol et
                    if (root.TryGetProperty("answers", out var answersElement))
                    {
                        string? answer = answersElement.GetString();
                        return answer != null ? answer.ToUpper() : "?";
                    }

                    // Eski "answer" anahtarı kontrolü (geriye dönük uyumluluk için)
                    if (root.TryGetProperty("answer", out var answerElement))
                    {
                        string? answer = answerElement.GetString();
                        return answer != null ? answer.ToUpper() : "?";
                    }
                }

                // Cevap bulunamadı, metni analiz ederek bulmaya çalış
                if (response.Contains("Doğru cevaplar sırasıyla:") || response.Contains("Doğru cevap sırasıyla:"))
                {
                    // Metinden cevapları çıkarmaya çalış
                    var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Doğru cevaplar sırasıyla:") || line.Contains("Doğru cevap sırasıyla:"))
                        {
                            var answersText = line.Split(':')[1].Trim();
                            // Cevapları ayıkla, büyük harfe çevir ve virgülle birleştir
                            var answers = answersText.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(a => a.ToUpper());
                            return string.Join(",", answers);
                        }
                    }
                }

                return "?";
            }
            catch
            {
                return "?";
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
                                    text = $"Bu soru metni için kısa ve açıklayıcı bir başlık üret. Maksimum 50 karakter olsun. Sadece başlığı ver, JSON formatında değil. Soru metni: {questionText}"
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

        private string ExtractTitle(string response)
        {
            try
            {
                // JSON formatından title çıkar
                if (response.Contains("\"title\""))
                {
                    // JSON içerisinden title çıkarmaya çalış
                    var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"title\":"))
                        {
                            // { "title":"başlık", "solved":"true", "answers":"B" } formatını parse et
                            try
                            {
                                var jsonStart = line.IndexOf('{');
                                var jsonEnd = line.LastIndexOf('}');
                                if (jsonStart >= 0 && jsonEnd > jsonStart)
                                {
                                    var jsonStr = line.Substring(jsonStart, jsonEnd - jsonStart + 1);
                                    var jsonDoc = JsonDocument.Parse(jsonStr);
                                    if (jsonDoc.RootElement.TryGetProperty("title", out var titleElement))
                                    {
                                        var title = titleElement.GetString() ?? "";
                                        if (!string.IsNullOrWhiteSpace(title))
                                        {
                                            return title.Length > 50 ? title.Substring(0, 47) + "..." : title;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // JSON parse hatası, devam et
                            }
                        }
                    }
                }

                return $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch
            {
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