using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QSolver.Helpers
{
    /// <summary>
    /// Global kısayol tuşlarını yakalamak için gizli pencere
    /// </summary>
    public class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 1;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int VK_Q = 0x51;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Action _captureAction;
        private bool _disposed = false;

        public HotkeyWindow(Action captureAction)
        {
            _captureAction = captureAction;

            // Gizli pencere oluştur
            CreateParams cp = new CreateParams();
            CreateHandle(cp);

            // Ctrl+Shift+Q kısayolunu kaydet
            bool success = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_Q);
            if (success)
            {
                LogHelper.LogInfo("Global kısayol tuşu kaydedildi: Ctrl+Shift+Q");
            }
            else
            {
                LogHelper.LogWarning("Global kısayol tuşu kaydedilemedi - başka bir uygulama kullanıyor olabilir");
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                LogHelper.LogInfo("Kısayol tuşu algılandı: Ctrl+Shift+Q");
                _captureAction?.Invoke();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                DestroyHandle();
                _disposed = true;
                LogHelper.LogInfo("Global kısayol tuşu kaldırıldı");
            }
        }
    }
}
