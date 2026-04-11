using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Google.GenAI;
using Google.GenAI.Types;
using QSolver.Helpers;
using QSolver.Models;
using QSolver.Services;
using GContent = Google.GenAI.Types.Content;
using GPart = Google.GenAI.Types.Part;
using SchemaType = Google.GenAI.Types.Type;

namespace QSolver
{
    public class GeminiService
    {
        private string apiKey;

        public GeminiService(string? apiKey)
        {
            LogHelper.LogInfo("GeminiService başlatılıyor");
            this.apiKey = string.IsNullOrEmpty(apiKey) ?
                ApiKeyManager.GetRandomApiKey() :
                apiKey;
        }

        private Client CreateClient(string key)
        {
            return new Client(apiKey: key);
        }

        /// <summary>
        /// OCR yanıtı için Schema döndürür
        /// </summary>
        private static Schema GetOcrSchema()
        {
            return new Schema
            {
                Type = SchemaType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    { "text", new Schema { Type = SchemaType.String, Description = "Görselde tespit edilen metin. Her satırı newline (\\n) karakteri ile ayır. Şıklar ve paragraflar ayrı satırlarda olmalı. Metin yoksa boş string." } },
                    { "hasText", new Schema { Type = SchemaType.Boolean, Description = "Görselde metin var mı?" } }
                },
                Required = new List<string> { "text", "hasText" },
                PropertyOrdering = new List<string> { "text", "hasText" }
            };
        }

        /// <summary>
        /// Soru çözümü yanıtı için Schema döndürür
        /// </summary>
        private static Schema GetSolutionSchema(List<string> availableLecturesEn, List<string> availableLecturesTr)
        {
            string lectureEnDescription = availableLecturesEn.Count > 0
                ? $"Lecture name in ENGLISH. Known: [{string.Join(", ", availableLecturesEn)}]. Use if matches, else write new."
                : "Lecture name in ENGLISH (e.g. Mathematics, Physics, Chemistry, Biology, History, Geography, Turkish, English)";

            string lectureTrDescription = availableLecturesTr.Count > 0
                ? $"Ders adı TÜRKÇE. Bilinenler: [{string.Join(", ", availableLecturesTr)}]. Eşleşiyorsa kullan, yoksa yeni yaz."
                : "Ders adı TÜRKÇE (örn: Matematik, Fizik, Kimya, Biyoloji, Tarih, Coğrafya, Türkçe, İngilizce)";

            return new Schema
            {
                Type = SchemaType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    { "lecture_en", new Schema { Type = SchemaType.String, Description = lectureEnDescription } },
                    { "lecture_tr", new Schema { Type = SchemaType.String, Description = lectureTrDescription } },
                    { "title", new Schema { Type = SchemaType.String, Description = "Sorunun kısa başlığı (max 50 karakter), örn: 'Matematik: Türev', 'Fizik: Hareket'" } },
                    { "explanation", new Schema { Type = SchemaType.String, Description = "Çözümün markdown formatında adım adım açıklaması" } },
                    { "solved", new Schema { Type = SchemaType.Boolean, Description = "Sorunun başarıyla çözülüp çözülemediği" } },
                    { "answers", new Schema { Type = SchemaType.String, Description = "Cevap(lar) - tek soru için 'A', çoklu sorular için soru numarasına göre sıralı 'A,B,C' formatında" } }
                },
                Required = new List<string> { "lecture_en", "lecture_tr", "title", "explanation", "solved", "answers" },
                PropertyOrdering = new List<string> { "lecture_en", "lecture_tr", "title", "explanation", "solved", "answers" }
            };
        }

        /// <summary>
        /// Turbo Mode için minimal schema - sadece cevap ve başlık
        /// </summary>
        private static Schema GetTurboSchema(List<string> availableLecturesEn, List<string> availableLecturesTr)
        {
            string lectureEnDescription = availableLecturesEn.Count > 0
                ? $"Lecture in ENGLISH. Known: [{string.Join(", ", availableLecturesEn)}]. Select if matches, else write new."
                : "Lecture in ENGLISH (Mathematics, Physics, Chemistry etc.)";

            string lectureTrDescription = availableLecturesTr.Count > 0
                ? $"Ders adı TÜRKÇE. [{string.Join(", ", availableLecturesTr)}]. Eşleşiyorsa seç, yoksa yeni yaz."
                : "Ders adı TÜRKÇE (Matematik, Fizik, Kimya vb.)";

            return new Schema
            {
                Type = SchemaType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    { "lecture_en", new Schema { Type = SchemaType.String, Description = lectureEnDescription } },
                    { "lecture_tr", new Schema { Type = SchemaType.String, Description = lectureTrDescription } },
                    { "title", new Schema { Type = SchemaType.String, Description = "Sorunun kısa başlığı (max 30 karakter)" } },
                    { "solved", new Schema { Type = SchemaType.Boolean, Description = "Çözüldü mü?" } },
                    { "answers", new Schema { Type = SchemaType.String, Description = "Cevap(lar) - 'A' veya 'A,B,C'" } }
                },
                Required = new List<string> { "lecture_en", "lecture_tr", "title", "solved", "answers" },
                PropertyOrdering = new List<string> { "lecture_en", "lecture_tr", "title", "solved", "answers" }
            };
        }

        /// <summary>
        /// SDK yanıtından text çıkarır
        /// </summary>
        private static string? ExtractText(GenerateContentResponse response)
        {
            var content = response.Candidates?
                .FirstOrDefault()?.Content;
            var part = content?.Parts?
                .FirstOrDefault();
            return part?.Text;
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

                byte[] imageBytes = Convert.FromBase64String(base64Image);

                // Her API anahtarı için deneme yap
                foreach (var currentKey in allApiKeys)
                {
                    if (triedKeys.Contains(currentKey)) continue;

                    try
                    {
                        apiKey = currentKey;
                        LogHelper.LogDebug($"API anahtarı deneniyor: {currentKey.Substring(0, 5)}...");

                        var client = CreateClient(currentKey);
                        var selectedModel = SettingsService.GetSelectedModel();

                        var response = await client.Models.GenerateContentAsync(
                            model: selectedModel,
                            contents: new List<GContent>
                            {
                                new GContent
                                {
                                    Role = "user",
                                    Parts = new List<GPart>
                                    {
                                        new GPart { Text = LocalizationService.Get("Prompts.Analysis") },
                                        new GPart
                                        {
                                            InlineData = new Blob
                                            {
                                                Data = imageBytes,
                                                MimeType = "image/png"
                                            }
                                        }
                                    }
                                }
                            },
                            config: new GenerateContentConfig
                            {
                                ResponseMimeType = "application/json",
                                ResponseSchema = GetOcrSchema()
                            }
                        );

                        string? structuredJson = ExtractText(response);
                        LogHelper.LogDebug($"Gemini OCR yanıtı: {structuredJson}");

                        if (structuredJson != null)
                        {
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
                    catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                    {
                        triedKeys.Add(currentKey);
                        lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                        LogHelper.LogError($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                        continue;
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

        public async Task<(string fullResponse, string answer, string lectureEn, string lectureTr)> SolveQuestion(string questionText)
        {
            try
            {
                LogHelper.LogInfo("Soru çözülüyor...");
                List<string> triedKeys = new List<string>();
                Exception? lastException = null;

                // Mevcut dersleri al (her iki dilde)
                var availableLecturesEn = SolutionHistoryService.GetAvailableLecturesEn();
                var availableLecturesTr = SolutionHistoryService.GetAvailableLecturesTr();

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
                        string lectureHint = "";
                        if (availableLecturesEn.Count > 0 || availableLecturesTr.Count > 0)
                        {
                            lectureHint = $"\n\nKnown lectures EN: [{string.Join(", ", availableLecturesEn)}]\nBilinen dersler TR: [{string.Join(", ", availableLecturesTr)}]";
                        }

                        var client = CreateClient(currentKey);
                        var selectedModel = SettingsService.GetSelectedModel();

                        var response = await client.Models.GenerateContentAsync(
                            model: selectedModel,
                            contents: LocalizationService.Get("Prompts.Solve") + lectureHint + "\n\n" + (LocalizationService.IsTurkish ? "Metin:" : "Text:") + "\n" + questionText,
                            config: new GenerateContentConfig
                            {
                                ResponseMimeType = "application/json",
                                ResponseSchema = GetSolutionSchema(availableLecturesEn, availableLecturesTr)
                            }
                        );

                        string? structuredJson = ExtractText(response);
                        LogHelper.LogDebug($"Gemini Solver yanıtı: {structuredJson}");

                        if (structuredJson != null)
                        {
                            var solutionResult = JsonSerializer.Deserialize<SolutionResponse>(structuredJson);

                            if (solutionResult != null)
                            {
                                string fullResponse = solutionResult.explanation;
                                string answer = solutionResult.solved ? solutionResult.answers.ToUpper() : (LocalizationService.IsTurkish ? "Çözülemedi" : "Unsolved");
                                string lectureEn = solutionResult.lecture_en ?? "";
                                string lectureTr = solutionResult.lecture_tr ?? "";
                                LogHelper.LogInfo($"Soru çözüm sonucu: {fullResponse}");
                                LogHelper.LogInfo($"Çıkarılan cevap: {answer}");
                                LogHelper.LogInfo($"Ders EN: {lectureEn}, TR: {lectureTr}");
                                return (fullResponse, answer, lectureEn, lectureTr);
                            }
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return (LocalizationService.IsTurkish ? "Yanıt işlenemedi." : "Response could not be processed.", LocalizationService.IsTurkish ? "Hata" : "Error", "", "");
                    }
                    catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                    {
                        triedKeys.Add(currentKey);
                        lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                        LogHelper.LogError($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                        continue;
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

        public async Task<(string fullResponse, string answer, string title, string lectureEn, string lectureTr)> SolveQuestionDirectly(string base64Image)
        {
            try
            {
                LogHelper.LogInfo("Soru görsel üzerinden doğrudan çözülüyor...");
                List<string> triedKeys = new List<string>();
                Exception? lastException = null;

                // Mevcut dersleri al (her iki dilde)
                var availableLecturesEn = SolutionHistoryService.GetAvailableLecturesEn();
                var availableLecturesTr = SolutionHistoryService.GetAvailableLecturesTr();

                // Tüm API anahtarlarını al
                var allApiKeys = ApiKeyManager.GetAllApiKeys();
                if (allApiKeys.Count == 0)
                {
                    throw new Exception("Kullanılabilir API anahtarı bulunamadı");
                }

                byte[] imageBytes = Convert.FromBase64String(base64Image);

                // Her API anahtarı için deneme yap
                foreach (var currentKey in allApiKeys)
                {
                    if (triedKeys.Contains(currentKey)) continue;

                    try
                    {
                        apiKey = currentKey;
                        LogHelper.LogDebug($"API anahtarı deneniyor: {currentKey.Substring(0, 5)}...");

                        // Prompt'a mevcut dersleri ekle
                        string lectureHint = "";
                        if (availableLecturesEn.Count > 0 || availableLecturesTr.Count > 0)
                        {
                            lectureHint = $"\n- lecture_en: EN [{string.Join(", ", availableLecturesEn)}]\n- lecture_tr: TR [{string.Join(", ", availableLecturesTr)}]";
                        }
                        else
                        {
                            lectureHint = "\n- lecture_en: Lecture in English (Mathematics, Physics etc.)\n- lecture_tr: Ders adı Türkçe (Matematik, Fizik vb.)";
                        }

                        var client = CreateClient(currentKey);
                        var selectedModel = SettingsService.GetSelectedModel();

                        var response = await client.Models.GenerateContentAsync(
                            model: selectedModel,
                            contents: new List<GContent>
                            {
                                new GContent
                                {
                                    Role = "user",
                                    Parts = new List<GPart>
                                    {
                                        new GPart
                                        {
                                            Text = LocalizationService.Get("Prompts.DirectSolve") + lectureHint
                                        },
                                        new GPart
                                        {
                                            InlineData = new Blob
                                            {
                                                Data = imageBytes,
                                                MimeType = "image/png"
                                            }
                                        }
                                    }
                                }
                            },
                            config: new GenerateContentConfig
                            {
                                ResponseMimeType = "application/json",
                                ResponseSchema = GetTurboSchema(availableLecturesEn, availableLecturesTr)
                            }
                        );

                        string? structuredJson = ExtractText(response);
                        LogHelper.LogDebug($"Gemini Turbo yanıtı: {structuredJson}");

                        if (structuredJson != null)
                        {
                            var turboResult = JsonSerializer.Deserialize<TurboResponse>(structuredJson);

                            if (turboResult != null)
                            {
                                // Turbo modda explanation yok, sadece "Turbo Mode" yazılır
                                string fullResponse = LocalizationService.IsTurkish ? "Turbo Mode - Hızlı çözüm" : "Turbo Mode - Fast solution";
                                string answer = turboResult.solved ? turboResult.answers.ToUpper() : (LocalizationService.IsTurkish ? "Çözülemedi" : "Unsolved");
                                string title = !string.IsNullOrEmpty(turboResult.title)
                                    ? (turboResult.title.Length > 50 ? turboResult.title.Substring(0, 47) + "..." : turboResult.title)
                                    : $"Turbo - {DateTime.Now:HH:mm}";
                                string lectureEn = turboResult.lecture_en ?? "";
                                string lectureTr = turboResult.lecture_tr ?? "";
                                LogHelper.LogInfo($"Turbo çözüm: cevap={answer}, başlık={title}, ders EN={lectureEn}, TR={lectureTr}");
                                return (fullResponse, answer, title, lectureEn, lectureTr);
                            }
                        }

                        LogHelper.LogWarning("API yanıtı beklenen formatta değil");
                        return (LocalizationService.IsTurkish ? "Yanıt işlenemedi." : "Response could not be processed.", LocalizationService.IsTurkish ? "Hata" : "Error", LocalizationService.IsTurkish ? "Çözülemeyen Soru" : "Unsolved Question", "", "");
                    }
                    catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                    {
                        triedKeys.Add(currentKey);
                        lastException = new Exception($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                        LogHelper.LogError($"API kotası aşıldı: {currentKey.Substring(0, 5)}...");
                        continue;
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

                var client = CreateClient(apiKey);
                var selectedModel = SettingsService.GetSelectedModel();

                var response = await client.Models.GenerateContentAsync(
                    model: selectedModel,
                    contents: LocalizationService.Get("Prompts.TitleGen", questionText)
                );

                string? rawText = ExtractText(response);
                LogHelper.LogDebug($"Gemini Title yanıtı: {rawText}");

                if (rawText != null)
                {
                    var cleanTitle = CleanTitle(rawText);

                    // Başlık çok uzunsa kısalt
                    if (cleanTitle.Length > 50)
                    {
                        cleanTitle = cleanTitle.Substring(0, 47) + "...";
                    }

                    LogHelper.LogInfo($"Üretilen başlık: {cleanTitle}");
                    return cleanTitle;
                }

                return $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Title generation error: {ex.Message}");
                // Hata durumunda varsayılan başlık döndür
                return $"Soru - {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
        }

        private string CleanTitle(string content)
        {
            try
            {
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

        /// <summary>
        /// API'den kullanılabilir modelleri listeler (generateContent destekleyenler)
        /// </summary>
        public static async Task<List<string>> FetchAvailableModelsAsync(string apiKey)
        {
            var models = new List<string>();
            try
            {
                var client = new Client(apiKey: apiKey);
                var pager = await client.Models.ListAsync();

                await foreach (var model in pager)
                {
                    if (model.SupportedActions?.Contains("generateContent") == true &&
                        model.Name != null)
                    {
                        // "models/" prefix'ini kaldır
                        string modelName = model.Name.StartsWith("models/")
                            ? model.Name.Substring(7)
                            : model.Name;

                        // Sadece Gemini modellerini dahil et
                        if (!modelName.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Soru çözmeyle alakasız modelleri filtrele
                        string lower = modelName.ToLowerInvariant();
                        if (lower.Contains("tts") ||
                            lower.Contains("image") ||
                            lower.Contains("robotics") ||
                            lower.Contains("computer-use") ||
                            lower.Contains("embedding") ||
                            lower.Contains("-latest") ||
                            lower.Contains("-001") ||
                            lower.Contains("customtc"))
                            continue;

                        models.Add(modelName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Model listesi alınırken hata oluştu", ex);
            }
            return models;
        }
    }
}
