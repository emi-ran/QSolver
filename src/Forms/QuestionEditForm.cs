using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace QSolver
{
    public class QuestionEditForm : Form
    {
        private readonly RichTextBox questionTextBox;
        private string questionText;

        public string EditedQuestionText => questionTextBox.Text;
        public bool IsEdited { get; private set; } = false;

        public QuestionEditForm(string initialText)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.SupportsTransparentBackColor, true);

            UpdateStyles();

            this.questionText = initialText;
            this.Text = QSolver.Services.LocalizationService.Get("QuestionEdit.Title");
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Padding = new Padding(2);

            // Form yuvarlak köşeli olsun
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 15, 15));

            // Soru metin kutusu
            questionTextBox = new RichTextBox
            {
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = initialText,
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
                WordWrap = false,
                AutoWordSelection = false,
                DetectUrls = false,
                HideSelection = false
            };

            // Tab tuşunu yakala
            questionTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    questionTextBox.SelectedText = "    ";
                }
            };

            // Butonlar için panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(48, 51, 107),
                Padding = new Padding(10)
            };

            // Kaydet butonu
            var saveButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Common.Save"),
                Width = 100,
                Height = 35,
                Location = new Point(buttonPanel.Width - 230, 12),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 213, 115),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, saveButton.Width, saveButton.Height, 10, 10));
            saveButton.Click += SaveButton_Click;

            // İptal butonu
            var cancelButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Common.Cancel"),
                Width = 100,
                Height = 35,
                Location = new Point(buttonPanel.Width - 120, 12),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 159, 67),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, cancelButton.Width, cancelButton.Height, 10, 10));
            cancelButton.Click += CancelButton_Click;

            // Buton hover efektleri
            foreach (Button btn in new[] { saveButton, cancelButton })
            {
                btn.MouseEnter += (s, e) =>
                {
                    if (s is Button button)
                    {
                        button.BackColor = DarkenColor(button.BackColor, 20);
                    }
                };

                btn.MouseLeave += (s, e) =>
                {
                    if (s is Button button)
                    {
                        button.BackColor = LightenColor(button.BackColor, 20);
                    }
                };
            }

            // Kontrolleri forma ekle
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            mainPanel.Controls.Add(questionTextBox);

            this.Controls.Add(mainPanel);
            this.Controls.Add(buttonPanel);

            // Escape tuşu ile formu kapat
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };

            // Form sürükleme için mouse olayları
            bool isDragging = false;
            Point dragStart = Point.Empty;

            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    dragStart = new Point(e.X, e.Y);
                }
            };

            this.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    Point p = PointToScreen(e.Location);
                    Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y);
                }
            };

            this.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = false;
                }
            };
        }

        private Color DarkenColor(Color color, int amount)
        {
            return Color.FromArgb(color.A,
                Math.Max(color.R - amount, 0),
                Math.Max(color.G - amount, 0),
                Math.Max(color.B - amount, 0));
        }

        private Color LightenColor(Color color, int amount)
        {
            return Color.FromArgb(color.A,
                Math.Min(color.R + amount, 255),
                Math.Min(color.G + amount, 255),
                Math.Min(color.B + amount, 255));
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Form kenarları için gölge efekti
            using (Pen pen = new Pen(Color.FromArgb(40, Color.White), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // WS_EX_COMPOSITED
                return cp;
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(questionTextBox.Text))
            {
                MessageBox.Show(QSolver.Services.LocalizationService.Get("QuestionEdit.EmptyError"), QSolver.Services.LocalizationService.Get("Common.Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (questionTextBox.Text != questionText)
            {
                IsEdited = true;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}