using System;
using System.Drawing;
using System.Windows.Forms;

namespace QSolver.Forms
{
    public partial class SettingsForm : Form
    {
        private ComboBox modelComboBox = null!;
        private Button saveButton = null!;
        private Button cancelButton = null!;
        private Label modelLabel = null!;
        private CheckBox turboModeCheckBox = null!;

        public SettingsForm()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "QSolver - Ayarlar";
            this.Size = new Size(420, 230);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoScaleDimensions = new SizeF(7F, 15F);

            // Model label
            modelLabel = new Label
            {
                Text = "Model Seçimi:",
                Location = new Point(20, 30),
                Size = new Size(100, 23),
                ForeColor = Color.FromArgb(241, 241, 241),
                Font = new Font("Segoe UI", 9F)
            };

            // Model combo box
            modelComboBox = new ComboBox
            {
                Location = new Point(130, 27),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(62, 62, 66),
                ForeColor = Color.FromArgb(241, 241, 241),
                FlatStyle = FlatStyle.Flat
            };

            // Populate model combo box
            var availableModels = SettingsService.GetAvailableModels();
            foreach (var model in availableModels)
            {
                modelComboBox.Items.Add(model);
            }

            // Turbo Mode checkbox - özel çizim için
            turboModeCheckBox = new CheckBox
            {
                Text = "",
                Location = new Point(20, 80),
                Size = new Size(370, 30),
                MinimumSize = new Size(370, 30),
                ForeColor = Color.FromArgb(241, 241, 241),
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Appearance = Appearance.Normal,
                CheckAlign = ContentAlignment.TopLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            // Checkbox'ın özel çizimi için event handler
            turboModeCheckBox.Paint += (sender, e) =>
            {
                // Tüm alanı temizle - arka plan rengini uygula
                e.Graphics.Clear(Color.FromArgb(45, 45, 48));

                // Checkbox kutusunu çiz - biraz daha büyük
                Rectangle checkBoxRect = new Rectangle(2, 6, 18, 18);

                // Arka plan - işaretliyse yeşil, değilse gri
                Color bgColor = turboModeCheckBox.Checked ? Color.FromArgb(46, 213, 115) : Color.FromArgb(62, 62, 66);
                using (SolidBrush bgBrush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillRectangle(bgBrush, checkBoxRect);
                }

                // Kenar çizgisi - işaretliyse yeşil, değilse gri
                Color borderColor = turboModeCheckBox.Checked ? Color.FromArgb(46, 213, 115) : Color.FromArgb(120, 120, 120);
                using (Pen borderPen = new Pen(borderColor, 1))
                {
                    e.Graphics.DrawRectangle(borderPen, checkBoxRect);
                }

                // Eğer işaretliyse beyaz tik işareti çiz
                if (turboModeCheckBox.Checked)
                {
                    using (Pen checkPen = new Pen(Color.White, 2))
                    {
                        checkPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        checkPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                        // Tik işareti çiz - koordinatları ayarlandı
                        e.Graphics.DrawLine(checkPen, 5, 12, 9, 16);
                        e.Graphics.DrawLine(checkPen, 9, 16, 16, 9);
                    }
                }

                // Metni çiz - TextRenderer kullanarak daha iyi sarmalama
                Rectangle textRect = new Rectangle(28, 3, turboModeCheckBox.Width - 30, turboModeCheckBox.Height - 6);
                TextRenderer.DrawText(e.Graphics,
                    "Turbo Mod (Soru düzenleme adımını atla, direk çöz)",
                    turboModeCheckBox.Font,
                    textRect,
                    Color.FromArgb(241, 241, 241),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            };

            // Checkbox'a tıklanınca yeniden çiz
            turboModeCheckBox.CheckedChanged += (sender, e) =>
            {
                turboModeCheckBox.Invalidate();
            };

            // Save button
            saveButton = new Button
            {
                Text = "Kaydet",
                Location = new Point(220, 140),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;

            // Cancel button
            cancelButton = new Button
            {
                Text = "İptal",
                Location = new Point(305, 140),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(62, 62, 66),
                ForeColor = Color.FromArgb(241, 241, 241),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Click += CancelButton_Click;

            // Add controls to form
            this.Controls.Add(modelLabel);
            this.Controls.Add(modelComboBox);
            this.Controls.Add(turboModeCheckBox);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);

            this.ResumeLayout(false);
        }

        private void LoadSettings()
        {
            var currentModel = SettingsService.GetSelectedModel();
            modelComboBox.SelectedItem = currentModel;

            var turboMode = SettingsService.GetTurboMode();
            turboModeCheckBox.Checked = turboMode;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (modelComboBox.SelectedItem != null)
            {
                var selectedModel = modelComboBox.SelectedItem.ToString()!;
                SettingsService.SetSelectedModel(selectedModel);

                var turboMode = turboModeCheckBox.Checked;
                SettingsService.SetTurboMode(turboMode);

                MessageBox.Show("Ayarlar başarıyla kaydedildi!", "Bilgi",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Lütfen bir model seçin!", "Uyarı",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
