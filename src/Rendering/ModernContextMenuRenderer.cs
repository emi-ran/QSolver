using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QSolver.Rendering
{
    /// <summary>
    /// Modern, koyu temalı context menu renderer
    /// </summary>
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

    /// <summary>
    /// Modern context menu için renk tablosu
    /// </summary>
    public class ModernColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 48);
    }
}
