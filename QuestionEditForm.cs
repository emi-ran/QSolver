using System;
using System.Drawing;
using System.Windows.Forms;

namespace QSolver
{
    public class QuestionEditForm : Form
    {
        private readonly TextBox questionTextBox;
        private string questionText;

        public string EditedQuestionText => questionTextBox.Text;
        public bool IsEdited { get; private set; } = false;

        public QuestionEditForm(string initialText)
        {
            this.questionText = initialText;
            this.Text = "Soruyu Düzenle";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true; // Formu en üstte tut

            // Soru metin kutusu
            questionTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = initialText,
                Font = new Font("Segoe UI", 10)
            };

            // Butonlar için panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            // Kaydet butonu
            var saveButton = new Button
            {
                Text = "Kaydet",
                Width = 100,
                Height = 30,
                Location = new Point(this.Width - 230, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            saveButton.Click += SaveButton_Click;

            // İptal butonu
            var cancelButton = new Button
            {
                Text = "İptal",
                Width = 100,
                Height = 30,
                Location = new Point(this.Width - 120, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            cancelButton.Click += CancelButton_Click;

            // Kontrolleri forma ekle
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            this.Controls.Add(questionTextBox);
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
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
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