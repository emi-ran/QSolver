using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace QSolver
{
    public class ResultForm : Form
    {
        private readonly int answer;
        private readonly Label thinkingLabel;
        private readonly Label resultLabel;
        private readonly Button confirmButton;
        private readonly System.Windows.Forms.Timer animationTimer;
        private int animationDots = 0;
        private int elapsedTime = 0;

        public ResultForm(Point location)
        {
            // Rastgele cevap üret
            Random rnd = new Random();
            answer = rnd.Next(1, 11);

            // Form özellikleri
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = location;
            this.Size = new Size(200, 100);
            this.TopMost = true;
            this.ShowInTaskbar = false;

            // Düşünme etiketi
            thinkingLabel = new Label
            {
                Text = "Düşünülüyor",
                AutoSize = true,
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Visible = true
            };

            // Sonuç etiketi (başlangıçta gizli)
            resultLabel = new Label
            {
                Text = $"Soru çözüldü! Cevap: {answer}",
                AutoSize = true,
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Visible = false
            };

            // Onay butonu (başlangıçta gizli)
            confirmButton = new Button
            {
                Text = "Onayla",
                Size = new Size(100, 30),
                Location = new Point(10, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.DeepSkyBlue,
                ForeColor = Color.White,
                Visible = false
            };

            confirmButton.Click += (s, e) => this.Close();

            // Animasyon için timer
            animationTimer = new System.Windows.Forms.Timer
            {
                Interval = 500 // Her 500ms'de bir güncelle
            };

            animationTimer.Tick += (s, e) =>
            {
                elapsedTime += animationTimer.Interval;

                // 3 saniye sonra sonucu göster
                if (elapsedTime >= 3000)
                {
                    animationTimer.Stop();
                    thinkingLabel.Visible = false;
                    resultLabel.Visible = true;
                    confirmButton.Visible = true;
                    return;
                }

                // Nokta animasyonu
                animationDots = (animationDots + 1) % 4;
                thinkingLabel.Text = "Düşünülüyor" + new string('.', animationDots);
            };

            // Kontrolleri forma ekle
            this.Controls.Add(thinkingLabel);
            this.Controls.Add(resultLabel);
            this.Controls.Add(confirmButton);

            // Form stil ayarları
            this.BackColor = Color.White;

            // Timer'ı başlat
            animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Form kenarına çerçeve çiz
            using (Pen pen = new Pen(Color.DeepSkyBlue, 2))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class DoubleBufferedForm : Form
    {
        public DoubleBufferedForm()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer, true);
        }
    }

    public class Program
    {
        private readonly NotifyIcon trayIcon;
        private DoubleBufferedForm? captureForm;
        private Point startPoint;
        private Rectangle selectionRect;
        private bool isSelecting;

        public Program()
        {
            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "QSolver"
            };

            // Context menu oluşturma
            trayIcon.ContextMenuStrip.Items.Add("Soru Seç", null, CaptureScreen_Click);
            trayIcon.ContextMenuStrip.Items.Add("Çıkış", null, Exit_Click);

            // Temp klasörünü oluştur
            string tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
        }

        private void CaptureScreen_Click(object? sender, EventArgs e)
        {
            captureForm = new DoubleBufferedForm
            {
                WindowState = FormWindowState.Maximized,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.Black,
                Opacity = 0.3,
                ShowInTaskbar = false,
                TopMost = true
            };

            captureForm.KeyPress += (s, ev) =>
            {
                if (ev.KeyChar == (char)Keys.Escape)
                {
                    captureForm.Close();
                }
            };

            captureForm.MouseDown += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    isSelecting = true;
                    startPoint = ev.Location;
                    selectionRect = new Rectangle();
                }
            };

            captureForm.MouseMove += (s, ev) =>
            {
                if (isSelecting)
                {
                    Rectangle oldRect = selectionRect;
                    int x = Math.Min(startPoint.X, ev.X);
                    int y = Math.Min(startPoint.Y, ev.Y);
                    int width = Math.Abs(ev.X - startPoint.X);
                    int height = Math.Abs(ev.Y - startPoint.Y);
                    selectionRect = new Rectangle(x, y, width, height);

                    Rectangle invalidateRect = Rectangle.Union(oldRect, selectionRect);
                    invalidateRect.Inflate(2, 2);
                    captureForm.Invalidate(invalidateRect);
                }
            };

            captureForm.MouseUp += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    isSelecting = false;
                    captureForm.Opacity = 0;
                    Application.DoEvents();
                    CaptureRegion();
                    captureForm.Close();
                }
            };

            captureForm.Paint += (s, ev) =>
            {
                if (isSelecting && selectionRect.Width > 0 && selectionRect.Height > 0)
                {
                    ev.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // Dış çerçeve (gölge efekti için)
                    using (Pen outerPen = new Pen(Color.Black, 4))
                    {
                        ev.Graphics.DrawRectangle(outerPen, selectionRect);
                    }

                    // İç çerçeve
                    using (Pen innerPen = new Pen(Color.DeepSkyBlue, 3))
                    {
                        ev.Graphics.DrawRectangle(innerPen, selectionRect);
                    }

                    // Boyut bilgisi
                    string dimensions = $"{selectionRect.Width} x {selectionRect.Height}";
                    using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
                    {
                        // Metin boyutunu ölç
                        SizeF textSize = ev.Graphics.MeasureString(dimensions, font);

                        // Metin için arka plan konumu
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

                string tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                string fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(tempPath, fileName);

                bitmap.Save(fullPath, ImageFormat.Png);

                // Sonuç formunu göster
                Point resultLocation = new Point(
                    selectionRect.Right - 200,  // Form genişliği 200
                    selectionRect.Bottom + 10
                );

                ResultForm resultForm = new ResultForm(resultLocation);
                resultForm.Show();
            }
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Program program = new Program();
            Application.Run();
        }
    }
}
