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

        public TrayIconService(Action captureScreenAction, Action exitAction)
        {
            this.captureScreenAction = captureScreenAction;
            this.exitAction = exitAction;

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
        }

        private void CaptureScreen_Click(object? sender, EventArgs e)
        {
            captureScreenAction?.Invoke();
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