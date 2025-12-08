using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using QSolver.Forms;
using QSolver.Services;
using QSolver.Helpers;

namespace QSolver
{
    public class ModernContextMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color menuBackColor = Color.FromArgb(45, 45, 48);
        private readonly Color menuBorderColor = Color.FromArgb(51, 51, 55);
        private readonly Color itemHoverColor = Color.FromArgb(62, 62, 66);
        private readonly Color textColor = Color.FromArgb(241, 241, 241);

        public ModernContextMenuRenderer() : base(new ModernColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(menuBackColor))
            {
                e.Graphics.FillRectangle(brush, e.ConnectedArea);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = textColor;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            using (var brush = new SolidBrush(itemHoverColor))
            {
                e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(menuBorderColor))
            {
                var rect = new Rectangle(e.AffectedBounds.Location, new Size(e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1));
                using (var path = CreateRoundedRectangle(rect, 6))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            var size = new Size(diameter, diameter);
            var arc = new Rectangle(rect.Location, size);

            // Sol üst köşe
            path.AddArc(arc, 180, 90);

            // Üst kenar
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Sağ kenar
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Alt kenar
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }

    public class ModernColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 48);
    }

    public class TrayIconService
    {
        private readonly NotifyIcon trayIcon;
        private readonly Action captureScreenAction;
        private readonly Action exitAction;
        private readonly Action apiKeysAction;
        private readonly Action settingsAction;
        private readonly Action historyAction;
        private ToolStripMenuItem updateMenuItem;

        public TrayIconService(Action captureScreenAction, Action exitAction, Action apiKeysAction, Action settingsAction, Action historyAction)
        {
            this.captureScreenAction = captureScreenAction;
            this.exitAction = exitAction;
            this.apiKeysAction = apiKeysAction;
            this.settingsAction = settingsAction;
            this.historyAction = historyAction;

            // Icon dosyasının yolunu al
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "qsolver.ico");
            Icon appIcon = new Icon(iconPath);

            var contextMenu = new ContextMenuStrip
            {
                Renderer = new ModernContextMenuRenderer(),
                Font = new Font("Segoe UI", 9F),
                ShowImageMargin = false,
                Padding = new Padding(3, 2, 3, 2)
            };

            // Ana işlemler
            var captureItem = new ToolStripMenuItem(QSolver.Services.LocalizationService.Get("Tray.Capture"))
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ShortcutKeyDisplayString = "Ctrl+Shift+Q"
            };
            captureItem.Click += CaptureScreen_Click;

            var historyItem = new ToolStripMenuItem(QSolver.Services.LocalizationService.Get("Tray.History"))
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4)
            };
            historyItem.Click += History_Click;

            var separator1 = new ToolStripSeparator();

            // Ayarlar ve Konfigürasyon
            var settingsItem = new ToolStripMenuItem(QSolver.Services.LocalizationService.Get("Tray.Settings"))
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4)
            };
            settingsItem.Click += Settings_Click;

            var apiKeysItem = new ToolStripMenuItem(QSolver.Services.LocalizationService.Get("Tray.ApiKeys"))
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4)
            };
            apiKeysItem.Click += ApiKeys_Click;

            var separator2 = new ToolStripSeparator();

            // Güncelleme kontrolü
            updateMenuItem = new ToolStripMenuItem(GetUpdateMenuText())
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4)
            };
            updateMenuItem.Click += Update_Click;

            // Araçlar ve Yardım
            var logsItem = new ToolStripMenuItem(QSolver.Services.LocalizationService.Get("Tray.Logs"))
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4)
            };
            logsItem.Click += Logs_Click;

            var separator3 = new ToolStripSeparator();

            var exitItem = new ToolStripMenuItem(QSolver.Services.LocalizationService.Get("Tray.Exit"))
            {
                ForeColor = Color.FromArgb(241, 241, 241),
                Padding = new Padding(8, 4, 8, 4)
            };
            exitItem.Click += Exit_Click;

            // Menü öğelerini kategorilere göre ekle
            contextMenu.Items.Add(captureItem);
            contextMenu.Items.Add(historyItem);
            contextMenu.Items.Add(separator1);
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(apiKeysItem);
            contextMenu.Items.Add(separator2);
            contextMenu.Items.Add(updateMenuItem);
            contextMenu.Items.Add(logsItem);
            contextMenu.Items.Add(separator3);
            contextMenu.Items.Add(exitItem);

            trayIcon = new NotifyIcon()
            {
                Icon = appIcon,
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "QSolver"
            };

            // İkona tıklandığında ekran yakalama işlemini başlat
            trayIcon.Click += (s, e) =>
            {
                if (e is MouseEventArgs mouseArgs && mouseArgs.Button == MouseButtons.Left)
                {
                    captureScreenAction?.Invoke();
                }
            };
        }

        private void CaptureScreen_Click(object? sender, EventArgs e)
        {
            captureScreenAction?.Invoke();
        }

        private void ApiKeys_Click(object? sender, EventArgs e)
        {
            apiKeysAction?.Invoke();
        }

        private void Settings_Click(object? sender, EventArgs e)
        {
            settingsAction?.Invoke();
        }

        private void History_Click(object? sender, EventArgs e)
        {
            historyAction?.Invoke();
        }

        private void Logs_Click(object? sender, EventArgs e)
        {
            var logViewerForm = new LogViewerForm();
            logViewerForm.Show();
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            exitAction?.Invoke();
        }

        /// <summary>
        /// Güncelleme menü öğesinin metnini döndürür
        /// </summary>
        private string GetUpdateMenuText()
        {
            if (UpdateService.IsUpdateAvailable())
            {
                return LocalizationService.Get("Update.NewVersionAvailable");
            }
            return LocalizationService.Get("Update.CheckForUpdates");
        }

        /// <summary>
        /// Güncelleme menü öğesinin metnini günceller
        /// </summary>
        public void RefreshUpdateMenuItem()
        {
            if (updateMenuItem != null)
            {
                updateMenuItem.Text = GetUpdateMenuText();
            }
        }

        /// <summary>
        /// Güncelleme menü öğesine tıklandığında
        /// </summary>
        private async void Update_Click(object? sender, EventArgs e)
        {
            LogHelper.LogInfo("Manuel güncelleme kontrolü başlatıldı");
            // Her zaman son sürümü kontrol et
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            RefreshUpdateMenuItem();

            if (updateInfo == null)
            {
                LogHelper.LogWarning("Güncelleme bilgisi alınamadı");
                TopMostMessageBox.Show(
                    LocalizationService.Get("Update.Error"),
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (updateInfo.IsNewerVersion)
            {
                ShowUpdateDialog(updateInfo, isManualCheck: true);
            }
            else
            {
                LogHelper.LogInfo("Güncelleme bulunamadı, en güncel sürüm kullanılıyor");
                TopMostMessageBox.Show(
                    string.Format(LocalizationService.Get("Update.NoUpdateMessage"), UpdateService.GetCurrentVersion()),
                    LocalizationService.Get("Update.NoUpdate"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Güncelleme diyaloğunu gösterir
        /// </summary>
        public void ShowUpdateDialog(UpdateInfo updateInfo, bool isManualCheck = false)
        {
            LogHelper.LogInfo($"Güncelleme diyaloğu gösteriliyor: {updateInfo.LatestVersion} (Manuel: {isManualCheck})");
            var result = TopMostMessageBox.Show(
                string.Format(LocalizationService.Get("Update.Message"), updateInfo.LatestVersion),
                LocalizationService.Get("Update.Title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                LogHelper.LogInfo("Kullanıcı güncellemeyi kabul etti");
                UpdateService.OpenReleasePage(updateInfo.ReleaseUrl);
            }
            else if (!isManualCheck)
            {
                LogHelper.LogInfo("Kullanıcı güncellemeyi reddetti (otomatik kontrol)");
                // Otomatik kontrolde reddedildiyse kaydet
                UpdateService.DismissUpdate(updateInfo.LatestVersion);
            }
            else
            {
                LogHelper.LogInfo("Kullanıcı güncellemeyi reddetti (manuel kontrol)");
            }
        }

        public void Dispose()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }
    }

    /// <summary>
    /// TopMost MessageBox göstermek için yardımcı sınıf
    /// </summary>
    public static class TopMostMessageBox
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            // Geçici bir topmost form oluştur
            using var form = new Form
            {
                TopMost = true,
                StartPosition = FormStartPosition.CenterScreen,
                Width = 0,
                Height = 0,
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false
            };
            form.Show();
            form.BringToFront();
            form.Activate();

            return MessageBox.Show(form, text, caption, buttons, icon);
        }
    }
}