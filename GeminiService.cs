using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
        private readonly string apiKey;
        private readonly HttpClient httpClient;
        private const string API_URL_BASE = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string OCR_MODEL = "gemini-2.0-flash";
        private const string SOLVER_MODEL = "gemini-2.0-flash";

        public GeminiService(string apiKey)
        {
            this.apiKey = apiKey;
            httpClient = new HttpClient();
        }

        public async Task<string> AnalyzeImage(string base64Image)
        {
            try
            {
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
                var response = await httpClient.PostAsync($"{API_URL_BASE}/{OCR_MODEL}:generateContent?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    return $"API hatası: HTTP {(int)response.StatusCode} - {errorContent}";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

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

                    // ``` işaretlerini kaldır
                    result = result.Replace("```", "");

                    // Başındaki "text" kelimesini kaldır
                    if (result.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                    {
                        result = result.Substring(4).TrimStart();
                    }

                    // Görselde yazı yoksa özel JSON yanıtı döndür
                    if (result.Contains("NO_TEXT_FOUND") ||
                        string.IsNullOrWhiteSpace(result) ||
                        result.Contains("görselde yazı yok", StringComparison.OrdinalIgnoreCase) ||
                        result.Contains("görselde metin yok", StringComparison.OrdinalIgnoreCase) ||
                        result.Contains("görselde hiçbir yazı yok", StringComparison.OrdinalIgnoreCase))
                    {
                        return "{\"question_not_found\":\"try_again\"}";
                    }

                    return string.IsNullOrWhiteSpace(result) ? "Görselde metin bulunamadı." : result;
                }

                return "Yanıt işlenemedi. API yanıtı beklenen formatta değil.";
            }
            catch (Exception ex)
            {
                return $"Hata oluştu: {ex.Message}";
            }
        }

        public async Task<(string fullResponse, string answer)> SolveQuestion(string questionText)
        {
            try
            {
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
Cevabını şu formatta bitir: { ""solved"":""true"", ""answer"":""X"" } (X yerine doğru şıkkı yaz, eğer çözemediysen solved kısmını false yap).

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
                var response = await httpClient.PostAsync($"{API_URL_BASE}/{SOLVER_MODEL}:generateContent?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    return ("API hatası: " + response.StatusCode, "Hata");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

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
                    return (fullResponse, answer);
                }

                return ("Yanıt işlenemedi.", "Hata");
            }
            catch (Exception ex)
            {
                return ($"Hata oluştu: {ex.Message}", "Hata");
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

                    if (root.TryGetProperty("answer", out var answerElement))
                    {
                        return answerElement.GetString() ?? "?";
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