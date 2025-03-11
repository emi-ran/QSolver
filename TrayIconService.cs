using System;
using System.Drawing;
using System.Windows.Forms;

namespace QSolver
{
    public class TrayIconService
    {
        private readonly NotifyIcon trayIcon;
        private readonly Action captureScreenAction;
        private readonly Action exitAction;
        private readonly Action apiKeysAction;

        public TrayIconService(Action captureScreenAction, Action exitAction, Action apiKeysAction)
        {
            this.captureScreenAction = captureScreenAction;
            this.exitAction = exitAction;
            this.apiKeysAction = apiKeysAction;

            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "QSolver"
            };

            // Context menu oluşturma
            trayIcon.ContextMenuStrip.Items.Add("Soru Seç", null, CaptureScreen_Click);
            trayIcon.ContextMenuStrip.Items.Add("API Anahtarları", null, ApiKeys_Click);
            trayIcon.ContextMenuStrip.Items.Add("Çıkış", null, Exit_Click);
        }

        private void CaptureScreen_Click(object? sender, EventArgs e)
        {
            captureScreenAction?.Invoke();
        }

        private void ApiKeys_Click(object? sender, EventArgs e)
        {
            apiKeysAction?.Invoke();
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            exitAction?.Invoke();
        }

        public void Dispose()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }
    }
}