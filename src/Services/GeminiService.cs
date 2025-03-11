using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;

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
        private const string OCR_MODEL = "gemini-1.5-pro";
        private const string SOLVER_MODEL = "gemini-1.5-pro";

        public GeminiService(string? apiKey)
        {
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
            }
        }

        public async Task<string> AnalyzeImage(string base64Image)
        {
            try
            {
                // Her istekten önce API key'i güncelle
                UpdateApiKey();

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
                // Her istekten önce API key'i güncelle
                UpdateApiKey();

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
                var response = await httpClient.PostAsync($"{API_URL_BASE}/{SOLVER_MODEL}:generateContent?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    return ($"API hatası: {response.StatusCode}\nDetay: {errorContent}", "Hata");
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