using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        private DoubleBufferedForm? captureForm;
        private Point startPoint;
        private Rectangle selectionRect;
        private bool isSelecting;
        private readonly GeminiService geminiService;
        private HotkeyWindow? hotkeyWindow;

        // Statik gemini servisi referansı
        private static GeminiService? staticGeminiService;

        // Mutex referansı (restart için)
        private static Mutex? _instanceMutex;

        public Program()
        {
            // Localization servisini başlat
            QSolver.Services.LocalizationService.Initialize();

            LogHelper.LogInfo(QSolver.Services.LocalizationService.Get("App.Started"));

            // API anahtarı yöneticisinden rastgele bir anahtar al
            string apiKey = ApiKeyManager.GetRandomApiKey();

            // Eğer hiç API anahtarı yoksa, kullanıcıya bildir
            if (string.IsNullOrEmpty(apiKey))
            {
                LogHelper.LogWarning("API anahtarı bulunamadı");
                MessageBox.Show(
                    QSolver.Services.LocalizationService.Get("App.NoApiKey"),
                    QSolver.Services.LocalizationService.Get("Common.Info"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            geminiService = new GeminiService(apiKey);
            staticGeminiService = geminiService;

            // Global hotkey'i kaydet
            hotkeyWindow = new HotkeyWindow(CaptureScreen);

            // Tray icon servisini oluştur
            trayIconService = new TrayIconService(CaptureScreen, Exit, ShowApiKeyForm, ShowSettingsForm, ShowHistoryForm);

            // Başlangıçta güncelleme kontrolü
            _ = StartupUpdateCheckAsync();
        }

        // Statik gemini servisi erişimi için metot
        public static GeminiService GetGeminiService()
        {
            if (staticGeminiService == null)
            {
                string? apiKey = ApiKeyManager.GetRandomApiKey();
                staticGeminiService = new GeminiService(apiKey);
            }
            return staticGeminiService;
        }

        /// <summary>
        /// Uygulamayı yeniden başlatır (mutex'i serbest bırakarak)
        /// </summary>
        public static void RestartApplication()
        {
            // Mutex'i serbest bırak
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }

            // Uygulamayı yeniden başlat
            Application.Restart();
            Environment.Exit(0);
        }

        /// <summary>
        /// Başlangıçta güncelleme kontrolü yapar
        /// </summary>
        private async Task StartupUpdateCheckAsync()
        {
            try
            {
                var updateInfo = await UpdateService.CheckForUpdatesAsync();
                trayIconService?.RefreshUpdateMenuItem();

                if (updateInfo != null && updateInfo.IsNewerVersion)
                {
                    // Bu sürüm daha önce reddedilmiş mi kontrol et
                    if (!UpdateService.IsUpdateDismissed(updateInfo.LatestVersion))
                    {
                        // Kullanıcıya güncelleme olduğunu bildir
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
            LogHelper.LogInfo("Ekran yakalama işlemi başlatılıyor");

            // API anahtarı kontrolü
            if (string.IsNullOrEmpty(ApiKeyManager.GetRandomApiKey()))
            {
                LogHelper.LogWarning("API anahtarı bulunamadı");
                MessageBox.Show(
                    QSolver.Services.LocalizationService.Get("App.NoApiKey"),
                    QSolver.Services.LocalizationService.Get("Common.Warning"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                // API anahtarı formunu göster
                ShowApiKeyForm();
                return;
            }

            // Seçim değişkenlerini sıfırla
            isSelecting = false;
            selectionRect = new Rectangle();
            startPoint = Point.Empty;

            // Ekran yakalama formunu oluştur
            captureForm = new DoubleBufferedForm
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                BackColor = Color.Black,
                Opacity = 0,
                Cursor = Cursors.Cross,
                TopMost = true,
                ShowInTaskbar = false
            };

            // Form kapatıldığında seçim değişkenlerini sıfırla
            captureForm.FormClosed += (s, e) =>
            {
                isSelecting = false;
                selectionRect = new Rectangle();
                startPoint = Point.Empty;
                captureForm = null;
                LogHelper.LogInfo("Ekran yakalama formu kapatıldı");
            };

            // Mouse olaylarını ekle
            captureForm.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    startPoint = e.Location;
                    isSelecting = true;
                    selectionRect = new Rectangle();
                    LogHelper.LogDebug($"Seçim başladı: X={e.X}, Y={e.Y}");
                }
            };

            captureForm.MouseMove += (s, e) =>
            {
                if (isSelecting)
                {
                    int x = Math.Min(startPoint.X, e.X);
                    int y = Math.Min(startPoint.Y, e.Y);
                    int width = Math.Abs(startPoint.X - e.X);
                    int height = Math.Abs(startPoint.Y - e.Y);

                    selectionRect = new Rectangle(x, y, width, height);
                    captureForm?.Invalidate();
                }
            };

            captureForm.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && isSelecting)
                {
                    isSelecting = false;
                    captureForm?.Hide();
                    LogHelper.LogDebug($"Seçim tamamlandı: X={e.X}, Y={e.Y}, Genişlik={selectionRect.Width}, Yükseklik={selectionRect.Height}");
                    CaptureRegion();
                }
            };

            // Escape tuşu ile iptal etme
            captureForm.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    LogHelper.LogInfo("Ekran yakalama işlemi kullanıcı tarafından iptal edildi");
                    captureForm?.Close();
                }
            };

            // Seçim alanını çiz
            captureForm.Paint += (s, ev) =>
            {
                if (isSelecting && selectionRect.Width > 0 && selectionRect.Height > 0)
                {
                    using (Pen pen = new Pen(Color.White, 1))
                    {
                        ev.Graphics.DrawRectangle(pen, selectionRect);
                    }

                    // Seçim alanının boyutlarını göster
                    string dimensions = $"{selectionRect.Width} x {selectionRect.Height}";
                    using (Font font = new Font("Arial", 10))
                    {
                        SizeF textSize = ev.Graphics.MeasureString(dimensions, font);
                        int textX = selectionRect.Right - (int)textSize.Width - 10;
                        int textY = selectionRect.Bottom + 5;

                        // Metin arka planı
                        using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(64, 0, 0, 0)))
                        {
                            ev.Graphics.FillRectangle(backBrush,
                                textX - 5, textY - 2,
                                textSize.Width + 10, textSize.Height + 4);
                        }

                        // Metin
                        using (SolidBrush textBrush = new SolidBrush(Color.White))
                        {
                            ev.Graphics.DrawString(dimensions, font, textBrush, textX, textY);
                        }
                    }
                }
            };

            // Formu göster ve yavaşça opaklığı artır
            System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 1;
            double opacity = 0;
            fadeTimer.Tick += (s, e) =>
            {
                opacity += 0.04;
                if (opacity >= 0.5)
                {
                    opacity = 0.5;
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                }
                if (captureForm != null && !captureForm.IsDisposed)
                {
                    captureForm.Opacity = opacity;
                }
            };

            captureForm.Show();
            fadeTimer.Start();
            LogHelper.LogInfo("Ekran yakalama formu gösterildi");

            // Form kapatıldığında timer'ı temizle
            captureForm.FormClosed += (s, e) =>
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();
            };
        }

        private void CaptureRegion()
        {
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0) return;

            LogHelper.LogInfo($"Bölge yakalanıyor: Genişlik={selectionRect.Width}, Yükseklik={selectionRect.Height}");

            using (Bitmap bitmap = new Bitmap(selectionRect.Width, selectionRect.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(selectionRect.Location, Point.Empty, selectionRect.Size);
                }

                // Görseli bellekte tutarak base64'e çevir
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    byte[] imageBytes = ms.ToArray();
                    string base64Image = Convert.ToBase64String(imageBytes);

                    // Screenshot'ı geçmiş için sakla
                    byte[] screenshotData = imageBytes;

                    try
                    {
                        // Sonuç formunun konumunu belirle
                        Control? controlForScreen = captureForm ?? Form.ActiveForm;
                        if (controlForScreen == null && Application.OpenForms.Count > 0)
                        {
                            controlForScreen = Application.OpenForms[0];
                        }

                        // Eğer hala null ise, varsayılan ekranı kullan
                        Screen currentScreen = controlForScreen != null
                            ? Screen.FromControl(controlForScreen)
                            : Screen.PrimaryScreen ?? Screen.AllScreens[0];

                        Rectangle screenBounds = currentScreen.WorkingArea;

                        // Sağ kenarın taşmadığını kontrol et
                        int x = screenBounds.Right - 250; // Form genişliği 250
                        if (selectionRect.Right + 250 <= screenBounds.Right)
                        {
                            x = selectionRect.Right - 125; // Sağ kenarın ortasında
                        }

                        int y = screenBounds.Height / 2 - 60; // Form yüksekliği 120 olduğu için yarısı

                        Point resultLocation = new Point(x, y);

                        // Turbo modu kontrolü
                        bool turboMode = SettingsService.GetTurboMode();

                        if (turboMode)
                        {
                            // Turbo mod: Doğrudan soru çözme
                            LogHelper.LogInfo("Turbo mod aktif - Soru doğrudan çözülüyor...");
                            var directSolveTask = geminiService.SolveQuestionDirectly(base64Image);

                            // Sonuç formunu göster ve task'ı ilet
                            ResultForm resultForm = new ResultForm(resultLocation, directSolveTask, screenshotData, true);
                            resultForm.Show();
                            LogHelper.LogInfo("Sonuç formu (turbo mod) gösterildi");
                        }
                        else
                        {
                            // Normal mod: Önce görsel analiz, sonra soru çözme
                            LogHelper.LogInfo("Görsel analiz ediliyor...");
                            var analysisTask = geminiService.AnalyzeImage(base64Image);

                            // Sonuç formunu göster ve analiz task'ını ilet
                            ResultForm resultForm = new ResultForm(resultLocation, analysisTask, screenshotData, false);
                            resultForm.Show();
                            LogHelper.LogInfo("Sonuç formu gösterildi");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError("API isteği sırasında hata oluştu", ex);
                        MessageBox.Show($"{QSolver.Services.LocalizationService.Get("Result.ApiRequestError")}: {ex.Message}",
                            QSolver.Services.LocalizationService.Get("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void Exit()
        {
            LogHelper.LogInfo(QSolver.Services.LocalizationService.Get("App.Exit"));
            hotkeyWindow?.Dispose();
            trayIconService?.Dispose();
            Application.Exit();
        }

        [STAThread]
        static void Main()
        {
            // DPI ayarlarını en başta etkinleştir (MessageBox'lar için de geçerli olsun)
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Mutex ile çoklu uygulama açılmasını engelle
            const string mutexName = "QSolver_SingleInstance_Mutex";
            _instanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Uygulama zaten çalışıyor
                MessageBox.Show(
                    "QSolver zaten çalışıyor!\nSistem tepsisinde (sağ alt köşede) QSolver simgesine tıklayarak erişebilirsiniz.",
                    "QSolver",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Program nesnesini oluştur ve uygulamayı çalıştır
            Program program = new Program();
            Application.Run();
        }
    }
}
