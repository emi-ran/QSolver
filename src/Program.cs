using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Media;

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
            };

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

            // Form kapatıldığında timer'ı temizle
            captureForm.FormClosed += (s, e) =>
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();
            };
        }

        private async void CaptureRegion()
        {
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
            {
                // Seçim yoksa formu kapat (opsiyonel, mevcut davranışta gizleniyor zaten)
                // captureForm?.Close();
                return;
            }

            string base64Image;
            try
            {
                using (Bitmap bitmap = new Bitmap(selectionRect.Width, selectionRect.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        // Ekran görüntüsünü alırken oluşabilecek hataları yakala
                        try
                        {
                            g.CopyFromScreen(selectionRect.Location, Point.Empty, selectionRect.Size);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ekran görüntüsü alma hatası: {ex.Message}");
                            SystemSounds.Beep.Play();
                            await Task.Delay(150);
                            SystemSounds.Beep.Play();
                            // captureForm?.Close(); // Formu kapatmayı düşünebiliriz
                            return;
                        }
                    }

                    // Görseli bellekte tutarak base64'e çevir
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Png);
                        byte[] imageBytes = ms.ToArray();
                        base64Image = Convert.ToBase64String(imageBytes);
                    }
                } // using Bitmap
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bitmap işleme hatası: {ex.Message}");
                SystemSounds.Beep.Play();
                await Task.Delay(150);
                SystemSounds.Beep.Play();
                // captureForm?.Close();
                return;
            }

            // Capture formunu hemen kapatabilir veya gizleyebiliriz, API yanıtını beklemeden.
            // captureForm?.Close(); // Veya captureForm?.Hide();

            try
            {
                // Gemini API'yi çağır (Program instance'ındaki geminiService kullanılır)
                string extractedText = await geminiService.AnalyzeImage(base64Image);

                // Başarı durumunu kontrol et
                if (string.IsNullOrEmpty(extractedText) ||
                    extractedText.StartsWith("Hata oluştu:", StringComparison.OrdinalIgnoreCase) ||
                    extractedText.StartsWith("API hatası:", StringComparison.OrdinalIgnoreCase) ||
                    extractedText.Contains("Yanıt işlenemedi"))
                {
                    // Hata durumu: 2 bip sesi çal
                    SystemSounds.Beep.Play();
                    await Task.Delay(150); // Bip sesleri arasına kısa bir bekleme
                    SystemSounds.Beep.Play();
                    Console.WriteLine($"API veya Analiz Hatası: {extractedText}"); // Loglama
                }
                else
                {
                    // Başarı durumu: Metni panoya kopyala ve 1 bip sesi çal
                    Thread staThread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText($"Aşağıdaki soruyu güzelce çözmek istiyorum. Soruyu bana nasıl çözdüğünü vs anlatmana gerek yok. Tek ihtiyacım sorunun cevabı. Tek demen gereken şey \"Sorunun cevabı: A) (a şıkkında ne yazıyorsa) şeklinde olmalı.\"\n\n{extractedText}");

                            // Başarılı kopyalama bildirimi göster
                            trayIconService?.ShowNotification(
                                "QSolver",
                                "Metin başarıyla panoya kopyalandı!"
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Pano hatası: {ex.Message}"); // Loglama
                            // Hata durumunda belki 2 bip çalınabilir?
                            // SystemSounds.Beep.Play();
                            // Task.Delay(150).Wait(); // Thread içinde await kullanılamaz, Wait kullan.
                            // SystemSounds.Beep.Play();
                        }
                    });
                    staThread.SetApartmentState(ApartmentState.STA);
                    staThread.Start();
                    staThread.Join(); // Ana thread'in beklemesini sağla

                    SystemSounds.Beep.Play();
                }
            }
            catch (Exception ex)
            {
                // Genel hata durumu: 2 bip sesi çal
                SystemSounds.Beep.Play();
                await Task.Delay(150);
                SystemSounds.Beep.Play();
                Console.WriteLine($"CaptureRegion Genel Hata: {ex.Message}"); // Loglama
            }
            finally
            {
                // İşlem bittikten sonra seçim formunu kapat (eğer hala açıksa)
                captureForm?.Close();
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
