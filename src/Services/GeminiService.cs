using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;
using QSolver.Helpers;

namespace QSolver
{
    public class GeminiRequest
    {
        public required Content[] contents { get; set; }
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

                        // API isteği için JSON hazırla
                        var request = new GeminiRequest
                        {
                            contents = new[]
                            {
                                new Content
                                {
                                    parts = new[]
                                    {
                                        new Part { text = "Bu görselde ne yazıyor? Lütfen sadece görseldeki metni aynen yaz. Görselde yazanlar dışında hiçbir ek açıklama ekleme. Eğer görselde hiçbir yazı yoksa sadece 'NO_TEXT_FOUND' yaz." },
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

                        // Yanıtı işle
                        using JsonDocument document = JsonDocument.Parse(jsonResponse);
                        var root = document.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            string result = textElement.GetString() ?? "Yanıt alınamadı.";
                            LogHelper.LogInfo($"Görsel analiz sonucu: {result}");
                            return result;
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

        public async Task<(string fullResponse, string answer)> SolveQuestion(string questionText)
        {
            try
            {
                LogHelper.LogInfo("Soru çözülüyor...");
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
                                            text = @"Sen bir soru çözme yapay zekasısın. Aşağıdaki soruyu analiz edip adım adım çöz. 
Çözümün sonunda hangi şıkkın doğru olduğunu belirt. 

ÖNEMLİ: Eğer birden fazla soru numarası varsa (örneğin: ""43. Soru"", ""44. Soru"" gibi), bunları BİRDEN FAZLA SORU olarak değerlendir ve SADECE TEK BİR JSON formatında cevap ver.

ÇOK ÖNEMLİ: Soruları çözdükten sonra, cevapları SORU NUMARALARINA GÖRE KÜÇÜKTEN BÜYÜĞE doğru sırala. Örneğin, sorular 44, 49, 45, 50 sırasıyla verilmişse, cevapları 44, 45, 49, 50 sırasıyla (yani küçükten büyüğe) ver. CEVAPLARI MUTLAKA SORU NUMARALARINA GÖRE SIRALA!

Cevabını şu formatta bitir:
- Tek bir soru için: { ""solved"":""true"", ""answers"":""X"" } formatını kullan (X yerine doğru şıkkı yaz)
- Birden fazla soru için: { ""solved"":""true"", ""answers"":""X,Y,Z,..."" } formatını kullan (Her soru için doğru şıkkı virgülle ayırarak yaz, ASLA her soru için ayrı JSON formatı verme!)
- Eğer çözemediysen: { ""solved"":""false"" }

TEKRAR: Eğer metin içinde numaralı sorular varsa (örn: 44, 49, 45, 50), soruları çözdükten sonra CEVAPLARI SORU NUMARALARINA GÖRE KÜÇÜKTEN BÜYÜĞE DOĞRU SIRALA ve tek bir JSON'da ver.
Örnek: Sorular 44, 49, 45, 50 sırasıyla verilmiş olsun ve cevapları A, B, C, D olsun. Doğru sıralama şöyle olacaktır:
44. soru: A
45. soru: C
49. soru: B
50. soru: D

Bu durumda JSON cevabın { ""solved"":""true"", ""answers"":""A,C,B,D"" } şeklinde olmalıdır. Yani cevaplar soru numaralarına göre sıralanmalıdır.

Tek soru bile olsa her zaman ""answers"" anahtarını kullan, ""answer"" değil.

Soru:
" + questionText
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

                            // Kota hatası ise diğer anahtarı dene
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                triedKeys.Add(currentKey);
                                lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                                continue;
                            }

                            return ($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}", "Hata");
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        LogHelper.LogDebug($"Gemini Solver yanıtı: {jsonResponse}");

                        // Yanıtı işle
                        using JsonDocument document = JsonDocument.Parse(jsonResponse);
                        var root = document.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            string fullResponse = textElement.GetString() ?? "Yanıt alınamadı.";
                            string answer = ExtractAnswer(fullResponse);
                            LogHelper.LogInfo($"Soru çözüm sonucu: {fullResponse}");
                            LogHelper.LogInfo($"Çıkarılan cevap: {answer}");
                            return (fullResponse, answer);
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return ("Yanıt işlenemedi.", "Hata");
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

        public async Task<(string fullResponse, string answer)> SolveQuestionDirectly(string base64Image)
        {
            try
            {
                LogHelper.LogInfo("Soru görsel üzerinden doğrudan çözülüyor...");
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
                                            text = @"Sen bir soru çözme yapay zekasısın. Bu görseldeki soruları analiz edip adım adım çöz. 
Çözümün sonunda hangi şıkkın doğru olduğunu belirt. 

ÖNEMLİ: Eğer birden fazla soru numarası varsa (örneğin: ""43. Soru"", ""44. Soru"" gibi), bunları BİRDEN FAZLA SORU olarak değerlendir ve SADECE TEK BİR JSON formatında cevap ver.

ÇOK ÖNEMLİ: Soruları çözdükten sonra, cevapları SORU NUMARALARINA GÖRE KÜÇÜKTEN BÜYÜĞE doğru sırala. Örneğin, sorular 44, 49, 45, 50 sırasıyla verilmişse, cevapları 44, 45, 49, 50 sırasıyla (yani küçükten büyüğe) ver. CEVAPLARI MUTLAKA SORU NUMARALARINA GÖRE SIRALA!

Cevabını şu formatta bitir:
- Tek bir soru için: { ""solved"":""true"", ""answers"":""X"" } formatını kullan (X yerine doğru şıkkı yaz)
- Birden fazla soru için: { ""solved"":""true"", ""answers"":""X,Y,Z,..."" } formatını kullan (Her soru için doğru şıkkı virgülle ayırarak yaz, ASLA her soru için ayrı JSON formatı verme!)
- Eğer çözemediysen: { ""solved"":""false"" }

TEKRAR: Eğer görselde numaralı sorular varsa (örn: 44, 49, 45, 50), soruları çözdükten sonra CEVAPLARI SORU NUMARALARINA GÖRE KÜÇÜKTEN BÜYÜĞE DOĞRU SIRALA ve tek bir JSON'da ver.
Örnek: Sorular 44, 49, 45, 50 sırasıyla verilmiş olsun ve cevapları A, B, C, D olsun. Doğru sıralama şöyle olacaktır:
44. soru: A
45. soru: C
49. soru: B
50. soru: D

Bu durumda JSON cevabın { ""solved"":""true"", ""answers"":""A,C,B,D"" } şeklinde olmalıdır. Yani cevaplar soru numaralarına göre sıralanmalıdır.

Tek soru bile olsa her zaman ""answers"" anahtarını kullan, ""answer"" değil."
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

                            return ($"API hatası: HTTP {(int)response.StatusCode} - {errorContent}", "Hata");
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        LogHelper.LogDebug($"Gemini Direct Solver yanıtı: {jsonResponse}");

                        // Yanıtı işle
                        using JsonDocument document = JsonDocument.Parse(jsonResponse);
                        var root = document.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            string fullResponse = textElement.GetString() ?? "Yanıt alınamadı.";
                            string answer = ExtractAnswer(fullResponse);
                            LogHelper.LogInfo($"Doğrudan soru çözüm sonucu: {fullResponse}");
                            LogHelper.LogInfo($"Çıkarılan cevap: {answer}");
                            return (fullResponse, answer);
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return ("Yanıt işlenemedi.", "Hata");
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
    }
}