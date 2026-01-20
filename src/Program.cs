using System;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using QSolver.Helpers;
using QSolver.Forms;
using QSolver.Services;

namespace QSolver
{
    public class Program
    {
        private TrayIconService? trayIconService;
        private readonly GeminiService geminiService;
        private readonly ScreenCaptureService screenCaptureService;
        private HotkeyWindow? hotkeyWindow;

        private static GeminiService? staticGeminiService;
        private static Mutex? _instanceMutex;

        public Program()
        {
            LocalizationService.Initialize();
            LogHelper.LogInfo(LocalizationService.Get("App.Started"));

            string apiKey = ApiKeyManager.GetRandomApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                LogHelper.LogWarning("API anahtarı bulunamadı");
                MessageBox.Show(
                    LocalizationService.Get("App.NoApiKey"),
                    LocalizationService.Get("Common.Info"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            geminiService = new GeminiService(apiKey);
            staticGeminiService = geminiService;

            screenCaptureService = new ScreenCaptureService(geminiService, ShowApiKeyForm);

            hotkeyWindow = new HotkeyWindow(CaptureScreen);
            trayIconService = new TrayIconService(CaptureScreen, Exit, ShowApiKeyForm, ShowSettingsForm, ShowHistoryForm);

            _ = StartupUpdateCheckAsync();
        }

        public static GeminiService GetGeminiService()
        {
            if (staticGeminiService == null)
            {
                string? apiKey = ApiKeyManager.GetRandomApiKey();
                staticGeminiService = new GeminiService(apiKey);
            }
            return staticGeminiService;
        }

        public static void RestartApplication()
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }

            Application.Restart();
            Environment.Exit(0);
        }

        private async Task StartupUpdateCheckAsync()
        {
            try
            {
                var updateInfo = await UpdateService.CheckForUpdatesAsync();
                trayIconService?.RefreshUpdateMenuItem();

                if (updateInfo != null && updateInfo.IsNewerVersion)
                {
                    if (!UpdateService.IsUpdateDismissed(updateInfo.LatestVersion))
                    {
                        trayIconService?.ShowUpdateDialog(updateInfo, isManualCheck: false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"Güncelleme kontrolü başarısız: {ex.Message}");
            }
        }

        private void ShowApiKeyForm()
        {
            LogHelper.LogInfo("API Anahtarları formu açılıyor");
            var apiKeyForm = new ApiKeyForm();
            apiKeyForm.ShowDialog();
            LogHelper.LogInfo("API Anahtarları formu kapatıldı");
        }

        private void ShowSettingsForm()
        {
            LogHelper.LogInfo("Ayarlar formu açılıyor");
            var settingsForm = new SettingsForm();
            settingsForm.ShowDialog();
            LogHelper.LogInfo("Ayarlar formu kapatıldı");
        }

        private void ShowHistoryForm()
        {
            LogHelper.LogInfo("Çözüm geçmişi formu açılıyor");
            var historyForm = new SolutionHistoryForm();
            historyForm.ShowDialog();
            LogHelper.LogInfo("Çözüm geçmişi formu kapatıldı");
        }

        private void CaptureScreen()
        {
            screenCaptureService.CaptureScreen();
        }

        private void Exit()
        {
            LogHelper.LogInfo(LocalizationService.Get("App.Exit"));
            hotkeyWindow?.Dispose();
            trayIconService?.Dispose();
            Application.Exit();
        }

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            const string mutexName = "QSolver_SingleInstance_Mutex";
            _instanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    LocalizationService.Get("App.AlreadyRunning"),
                    "QSolver",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Program program = new Program();
            Application.Run();
        }
    }
}
