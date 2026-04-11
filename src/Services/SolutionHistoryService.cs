using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Linq;
using System.Threading.Tasks;
using QSolver.Services;

namespace QSolver
{
    public class SolutionHistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Lecture { get; set; } = string.Empty;
        public string LectureEn { get; set; } = string.Empty;
        public string LectureTr { get; set; } = string.Empty;
        public string QuestionTitle { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string SolutionSteps { get; set; } = string.Empty;
        public string ScreenshotPath { get; set; } = string.Empty;
        public string UsedModel { get; set; } = string.Empty;
        public bool WasTurboMode { get; set; } = false;

        [JsonIgnore]
        public string LocalizedLecture
        {
            get
            {
                if (LocalizationService.IsTurkish)
                {
                    return !string.IsNullOrEmpty(LectureTr) ? LectureTr : Lecture;
                }
                else
                {
                    return !string.IsNullOrEmpty(LectureEn) ? LectureEn : Lecture;
                }
            }
        }
    }

    public class SolutionHistoryService : JsonConfigService<List<SolutionHistoryItem>>
    {
        private static readonly SolutionHistoryService Instance = new();

        private static readonly string HistoryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QSolver",
            "History");

        private static readonly string ScreenshotsDirectory = Path.Combine(HistoryDirectory, "Screenshots");

        protected override string ConfigFilePath => Path.Combine(HistoryDirectory, "history.json");
        protected override string LoadErrorKey => "History.LoadError";
        protected override string SaveErrorKey => "History.SaveError";

        static SolutionHistoryService()
        {
            EnsureDirectoriesExist();
            Instance.LoadData();
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
                MessageBox.Show(
                    $"{LocalizationService.Get("History.DirCreateError")}: {ex.Message}",
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static async Task AddSolutionToHistory(string questionText, string answer, string solutionSteps,
            byte[] screenshotData, bool wasTurboMode, string lectureEn = "", string lectureTr = "", string title = "")
        {
            try
            {
                if (string.IsNullOrEmpty(title))
                {
                    title = wasTurboMode ? questionText : await GenerateQuestionTitle(questionText);
                }

                var historyItem = new SolutionHistoryItem
                {
                    LectureEn = lectureEn,
                    LectureTr = lectureTr,
                    Lecture = LocalizationService.IsTurkish ? lectureTr : lectureEn,
                    QuestionTitle = title,
                    QuestionText = wasTurboMode ? (LocalizationService.IsTurkish ? "Turbo Mode - Doğrudan çözüm" : "Turbo Mode - Direct solution") : questionText,
                    Answer = answer,
                    SolutionSteps = solutionSteps,
                    UsedModel = SettingsService.GetSelectedModel(),
                    WasTurboMode = wasTurboMode
                };

                if (screenshotData != null && screenshotData.Length > 0)
                {
                    var screenshotFileName = $"screenshot_{historyItem.Id}.png";
                    var screenshotPath = Path.Combine(ScreenshotsDirectory, screenshotFileName);
                    File.WriteAllBytes(screenshotPath, screenshotData);
                    historyItem.ScreenshotPath = screenshotPath;
                }

                Instance.Data.Add(historyItem);
                Instance.SaveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{LocalizationService.Get("History.AddError")}: {ex.Message}",
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static List<string> GetAvailableLecturesEn()
        {
            return Instance.Data
                .Where(h => !string.IsNullOrEmpty(h.LectureEn))
                .Select(h => h.LectureEn)
                .Distinct()
                .OrderBy(l => l)
                .ToList();
        }

        public static List<string> GetAvailableLecturesTr()
        {
            return Instance.Data
                .Where(h => !string.IsNullOrEmpty(h.LectureTr))
                .Select(h => h.LectureTr)
                .Distinct()
                .OrderBy(l => l)
                .ToList();
        }

        public static List<string> GetAvailableLectures()
        {
            return Instance.Data
                .Where(h => !string.IsNullOrEmpty(h.Lecture))
                .Select(h => h.Lecture)
                .Distinct()
                .OrderBy(l => l)
                .ToList();
        }

        private static async Task<string> GenerateQuestionTitle(string questionText)
        {
            if (string.IsNullOrEmpty(questionText))
                return "Başlıksız Soru";

            try
            {
                var geminiService = new GeminiService(null);
                return await geminiService.GenerateQuestionTitle(questionText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI başlık üretimi başarısız: {ex.Message}");

                var title = questionText.Trim();
                if (title.Length > 50)
                    title = title.Substring(0, 50) + "...";

                title = title.Replace("\n", " ").Replace("\r", " ");
                while (title.Contains("  "))
                    title = title.Replace("  ", " ");

                return title.Trim();
            }
        }

        public static List<SolutionHistoryItem> GetHistory()
        {
            return Instance.Data.OrderByDescending(h => h.Timestamp).ToList();
        }

        public static List<SolutionHistoryItem> SearchHistory(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return GetHistory();

            var normalizedSearchTerm = NormalizeForSearch(searchTerm);

            return Instance.Data
                .Where(h => NormalizeForSearch(h.Lecture).Contains(normalizedSearchTerm) ||
                           NormalizeForSearch(h.QuestionTitle).Contains(normalizedSearchTerm) ||
                           NormalizeForSearch(h.QuestionText).Contains(normalizedSearchTerm) ||
                           NormalizeForSearch(h.Answer).Contains(normalizedSearchTerm))
                .OrderByDescending(h => h.Timestamp)
                .ToList();
        }

        private static string NormalizeForSearch(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            text = text.ToLower();

            var result = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        public static void DeleteHistoryItem(string id)
        {
            try
            {
                var item = Instance.Data.FirstOrDefault(h => h.Id == id);
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(item.ScreenshotPath) && File.Exists(item.ScreenshotPath))
                    {
                        File.Delete(item.ScreenshotPath);
                    }

                    Instance.Data.Remove(item);
                    Instance.SaveData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{LocalizationService.Get("History.DeleteError")}: {ex.Message}",
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void ClearHistory()
        {
            try
            {
                foreach (var item in Instance.Data)
                {
                    if (!string.IsNullOrEmpty(item.ScreenshotPath) && File.Exists(item.ScreenshotPath))
                    {
                        File.Delete(item.ScreenshotPath);
                    }
                }

                Instance.Data.Clear();
                Instance.SaveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{LocalizationService.Get("History.ClearError")}: {ex.Message}",
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static int GetHistoryCount() => Instance.Data.Count;
    }
}
