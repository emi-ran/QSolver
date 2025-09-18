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
            this.Size = new Size(400, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);

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
                Location = new Point(20, 70),
                Size = new Size(350, 23),
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

                // Checkbox kutusunu çiz
                Rectangle checkBoxRect = new Rectangle(1, 5, 16, 16);

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
                        e.Graphics.DrawLine(checkPen, 4, 10, 8, 14);
                        e.Graphics.DrawLine(checkPen, 8, 14, 14, 8);
                    }
                }

                // Metni çiz
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(241, 241, 241)))
                {
                    e.Graphics.DrawString("Turbo Mod (Soru düzenleme adımını atla, direk çöz)",
                        turboModeCheckBox.Font, textBrush, 25, 5);
                }
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
                Location = new Point(200, 120),
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
                Location = new Point(285, 120),
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
