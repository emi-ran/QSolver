using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QSolver.Helpers
{
    /// <summary>
    /// Common UI operations for WinForms controls.
    /// </summary>
    public static class UIHelper
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        /// <summary>
        /// Creates a rounded region for a control.
        /// </summary>
        public static Region CreateRoundedRegion(int width, int height, int radius = 10)
        {
            return Region.FromHrgn(CreateRoundRectRgn(0, 0, width, height, radius, radius));
        }

        /// <summary>
        /// Applies rounded region to a control.
        /// </summary>
        public static void ApplyRoundedCorners(Control control, int radius = 10)
        {
            control.Region = CreateRoundedRegion(control.Width, control.Height, radius);
        }

        /// <summary>
        /// Safely invokes an action on the UI thread.
        /// </summary>
        public static void InvokeOnUI(Control control, Action action)
        {
            if (control == null || control.IsDisposed) return;

            if (!control.IsHandleCreated) return;

            if (control.InvokeRequired)
            {
                control.Invoke((MethodInvoker)delegate { action(); });
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Applies standard flat button style with rounded corners.
        /// </summary>
        public static void ApplyButtonStyle(Button button, Color backgroundColor, int radius = 10)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = backgroundColor;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            ApplyRoundedCorners(button, radius);
        }

        /// <summary>
        /// Darkens a color by the specified amount.
        /// </summary>
        public static Color DarkenColor(Color color, int amount)
        {
            return Color.FromArgb(
                color.A,
                Math.Max(color.R - amount, 0),
                Math.Max(color.G - amount, 0),
                Math.Max(color.B - amount, 0));
        }

        /// <summary>
        /// Lightens a color by the specified amount.
        /// </summary>
        public static Color LightenColor(Color color, int amount)
        {
            return Color.FromArgb(
                color.A,
                Math.Min(color.R + amount, 255),
                Math.Min(color.G + amount, 255),
                Math.Min(color.B + amount, 255));
        }

        /// <summary>
        /// Adds hover effect to a button (darken on enter, lighten on leave).
        /// </summary>
        public static void AddHoverEffect(Button button, int amount = 20)
        {
            button.MouseEnter += (s, e) =>
            {
                if (s is Button btn)
                {
                    btn.BackColor = DarkenColor(btn.BackColor, amount);
                }
            };

            button.MouseLeave += (s, e) =>
            {
                if (s is Button btn)
                {
                    btn.BackColor = LightenColor(btn.BackColor, amount);
                }
            };
        }
    }
}
