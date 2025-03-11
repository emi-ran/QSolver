﻿using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

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

        // Statik gemini servisi referansı
        private static GeminiService? staticGeminiService;

        public Program()
        {
            // API anahtarı yöneticisinden rastgele bir anahtar al
            string apiKey = ApiKeyManager.GetRandomApiKey();

            // Eğer hiç API anahtarı yoksa, kullanıcıya bildir
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show(
                    "Henüz hiç API anahtarı eklenmemiş. Lütfen API Anahtarları menüsünden bir anahtar ekleyin.",
                    "Bilgi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            geminiService = new GeminiService(apiKey);
            staticGeminiService = geminiService;

            // Tray icon servisini oluştur
            trayIconService = new TrayIconService(CaptureScreen, Exit, ShowApiKeyForm);
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

        private void ShowApiKeyForm()
        {
            var apiKeyForm = new ApiKeyForm();
            apiKeyForm.ShowDialog();
        }

        private void CaptureScreen()
        {
            // API anahtarı kontrolü
            if (string.IsNullOrEmpty(ApiKeyManager.GetRandomApiKey()))
            {
                MessageBox.Show(
                    "Henüz hiç API anahtarı eklenmemiş. Lütfen API Anahtarları menüsünden bir anahtar ekleyin.",
                    "Uyarı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                // API anahtarı formunu göster
                ShowApiKeyForm();
                return;
            }

            // Ekran yakalama formunu oluştur
            captureForm = new DoubleBufferedForm();
            captureForm.FormBorderStyle = FormBorderStyle.None;
            captureForm.WindowState = FormWindowState.Maximized;
            captureForm.BackColor = Color.Black;
            captureForm.Opacity = 0.5;
            captureForm.Cursor = Cursors.Cross;
            captureForm.TopMost = true;

            // Mouse olaylarını ekle
            captureForm.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    startPoint = e.Location;
                    isSelecting = true;
                    selectionRect = new Rectangle();
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
                    CaptureRegion();
                }
            };

            // Escape tuşu ile iptal etme
            captureForm.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    captureForm?.Close();
                }
            };

            // Seçim alanını çiz
            captureForm.Paint += (s, ev) =>
            {
                if (isSelecting && selectionRect.Width > 0 && selectionRect.Height > 0)
                {
                    using (Pen pen = new Pen(Color.Red, 2))
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

            captureForm.Show();
        }

        private void CaptureRegion()
        {
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0) return;

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

                    // Her istek için yeni bir API anahtarı al
                    string apiKey = ApiKeyManager.GetRandomApiKey();
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        // Yeni bir GeminiService örneği oluştur
                        var requestGeminiService = new GeminiService(apiKey);

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

                            // API çağrısını başlat
                            var analysisTask = requestGeminiService.AnalyzeImage(base64Image);

                            // Sonuç formunu göster ve analiz task'ını ilet
                            ResultForm resultForm = new ResultForm(resultLocation, analysisTask);
                            resultForm.Show();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"API isteği sırasında hata oluştu: {ex.Message}",
                                "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("API anahtarı bulunamadı. Lütfen API Anahtarları menüsünden bir anahtar ekleyin.",
                            "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        // API anahtarı formunu göster
                        ShowApiKeyForm();
                    }
                }
            }
        }

        private void Exit()
        {
            trayIconService?.Dispose();
            Application.Exit();
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // DPI ayarlarını etkinleştir
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // Program nesnesini oluştur ve uygulamayı çalıştır
            Program program = new Program();
            Application.Run();
        }
    }
}
