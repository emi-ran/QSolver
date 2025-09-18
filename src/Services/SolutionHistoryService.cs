using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Linq;

namespace QSolver
{
    public class SolutionHistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string QuestionTitle { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string SolutionSteps { get; set; } = string.Empty;
        public string ScreenshotPath { get; set; } = string.Empty;
        public string UsedModel { get; set; } = string.Empty;
        public bool WasTurboMode { get; set; } = false;
    }

    public class SolutionHistoryService
    {
        private static readonly string HistoryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QSolver",
            "History");

        private static readonly string HistoryFilePath = Path.Combine(HistoryDirectory, "history.json");
        private static readonly string ScreenshotsDirectory = Path.Combine(HistoryDirectory, "Screenshots");

        private static List<SolutionHistoryItem> historyItems = new List<SolutionHistoryItem>();

        static SolutionHistoryService()
        {
            EnsureDirectoriesExist();
            LoadHistory();
        }

        private static void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(HistoryDirectory))
                    Directory.CreateDirectory(HistoryDirectory);

                if (!Directory.Exists(ScreenshotsDirectory))
                    Directory.CreateDirectory(ScreenshotsDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçmiş dizinleri oluşturulurken hata: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static async Task AddSolutionToHistory(string questionText, string answer, string solutionSteps,
            byte[] screenshotData, bool wasTurboMode)
        {
            try
            {
                // Turbo modda questionText zaten başlık, normal modda AI ile başlık üret
                string title = wasTurboMode ? questionText : await GenerateQuestionTitle(questionText);

                var historyItem = new SolutionHistoryItem
                {
                    QuestionTitle = title,
                    QuestionText = wasTurboMode ? "Turbo Mode - Doğrudan çözüm" : questionText,
                    Answer = answer,
                    SolutionSteps = solutionSteps,
                    UsedModel = SettingsService.GetSelectedModel(),
                    WasTurboMode = wasTurboMode
                };

                // Screenshot'ı kaydet
                if (screenshotData != null && screenshotData.Length > 0)
                {
                    var screenshotFileName = $"screenshot_{historyItem.Id}.png";
                    var screenshotPath = Path.Combine(ScreenshotsDirectory, screenshotFileName);
                    File.WriteAllBytes(screenshotPath, screenshotData);
                    historyItem.ScreenshotPath = screenshotPath;
                }

                historyItems.Add(historyItem);
                SaveHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Çözüm geçmişe eklenirken hata: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static async Task<string> GenerateQuestionTitle(string questionText)
        {
            if (string.IsNullOrEmpty(questionText))
                return "Başlıksız Soru";

            try
            {
                // GeminiService kullanarak AI ile başlık üret
                var geminiService = new GeminiService(null);
                return await geminiService.GenerateQuestionTitle(questionText);
            }
            catch (Exception ex)
            {
                // AI başlık üretimi başarısız, basit başlık kullanılıyor
                System.Diagnostics.Debug.WriteLine($"AI başlık üretimi başarısız, basit başlık kullanılıyor: {ex.Message}");

                // Hata durumunda basit başlık üretimi
                var title = questionText.Trim();
                if (title.Length > 50)
                    title = title.Substring(0, 50) + "...";

                // Özel karakterleri temizle
                title = title.Replace("\n", " ").Replace("\r", " ");
                while (title.Contains("  "))
                    title = title.Replace("  ", " ");

                return title.Trim();
            }
        }

        public static List<SolutionHistoryItem> GetHistory()
        {
            return historyItems.OrderByDescending(h => h.Timestamp).ToList();
        }

        public static List<SolutionHistoryItem> SearchHistory(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return GetHistory();

            var lowerSearchTerm = searchTerm.ToLower();
            return historyItems
                .Where(h => h.QuestionTitle.ToLower().Contains(lowerSearchTerm) ||
                           h.QuestionText.ToLower().Contains(lowerSearchTerm) ||
                           h.Answer.ToLower().Contains(lowerSearchTerm))
                .OrderByDescending(h => h.Timestamp)
                .ToList();
        }

        public static void DeleteHistoryItem(string id)
        {
            try
            {
                var item = historyItems.FirstOrDefault(h => h.Id == id);
                if (item != null)
                {
                    // Screenshot dosyasını da sil
                    if (!string.IsNullOrEmpty(item.ScreenshotPath) && File.Exists(item.ScreenshotPath))
                    {
                        File.Delete(item.ScreenshotPath);
                    }

                    historyItems.Remove(item);
                    SaveHistory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçmiş öğesi silinirken hata: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void ClearHistory()
        {
            try
            {
                // Tüm screenshot'ları sil
                foreach (var item in historyItems)
                {
                    if (!string.IsNullOrEmpty(item.ScreenshotPath) && File.Exists(item.ScreenshotPath))
                    {
                        File.Delete(item.ScreenshotPath);
                    }
                }

                historyItems.Clear();
                SaveHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçmiş temizlenirken hata: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static int GetHistoryCount()
        {
            return historyItems.Count;
        }

        private static void LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    var loadedItems = JsonSerializer.Deserialize<List<SolutionHistoryItem>>(json);
                    if (loadedItems != null)
                    {
                        historyItems = loadedItems;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçmiş yüklenirken hata: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                historyItems = new List<SolutionHistoryItem>();
            }
        }

        private static void SaveHistory()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                string json = JsonSerializer.Serialize(historyItems, options);
                File.WriteAllText(HistoryFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geçmiş kaydedilirken hata: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
