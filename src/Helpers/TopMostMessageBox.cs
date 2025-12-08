using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QSolver.Helpers
{
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
