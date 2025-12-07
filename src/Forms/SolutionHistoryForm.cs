using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using QSolver.Forms;

namespace QSolver.Forms
{
    public partial class SolutionHistoryForm : Form
    {
        private static readonly string IconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "qsolver.ico");
        private ListView historyListView = null!;
        private Panel detailPanel = null!;
        private PictureBox screenshotPictureBox = null!;
        private Label questionTitleLabel = null!;
        private TextBox questionTextBox = null!;
        private Label answerLabel = null!;
        private Button viewStepsButton = null!;
        private Button deleteButton = null!;
        private Button clearAllButton = null!;
        private TextBox searchTextBox = null!;
        private Label statsLabel = null!;
        private ToolTip toolTip = null!;

        private SolutionHistoryItem? selectedItem;

        public SolutionHistoryForm()
        {
            InitializeComponent();
            LoadHistory();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "QSolver - Çözüm Geçmişi";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.MinimumSize = new Size(800, 600);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            // Form icon
            if (File.Exists(IconPath))
            {
                this.Icon = new Icon(IconPath);
            }

            // Search box
            var searchLabel = new Label
            {
                Text = "Arama:",
                Location = new Point(20, 20),
                Size = new Size(50, 23),
                ForeColor = Color.FromArgb(241, 241, 241),
                Font = new Font("Segoe UI", 9F)
            };

            searchTextBox = new TextBox
            {
                Location = new Point(80, 17),
                Size = new Size(200, 23),
                BackColor = Color.FromArgb(62, 62, 66),
                ForeColor = Color.FromArgb(241, 241, 241),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            searchTextBox.TextChanged += SearchTextBox_TextChanged;

            // Statistics label
            statsLabel = new Label
            {
                Location = new Point(300, 20),
                Size = new Size(200, 23),
                ForeColor = Color.FromArgb(241, 241, 241),
                Font = new Font("Segoe UI", 9F),
                Text = "Toplam: 0 çözüm"
            };

            // Clear all button
            clearAllButton = new Button
            {
                Text = "Tümünü Temizle",
                Location = new Point(520, 15),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            clearAllButton.FlatAppearance.BorderSize = 0;
            clearAllButton.Click += ClearAllButton_Click;

            // History ListView
            historyListView = new ListView
            {
                Location = new Point(20, 60),
                Size = new Size(500, 580),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(62, 62, 66),
                ForeColor = Color.FromArgb(241, 241, 241),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            // ListView columns - son sütun kalan alanı doldursun (-2 = fill)
            historyListView.Columns.Add("Ders", 70);
            historyListView.Columns.Add("Başlık", 180);
            historyListView.Columns.Add("Cevap", 50);
            historyListView.Columns.Add("Tarih", 110);
            historyListView.Columns.Add("Model", -2); // Kalan alanı doldur

            // Çift tıklamada otomatik genişleme olmasın
            historyListView.ColumnWidthChanging += (s, e) =>
            {
                // Çift tıklama ile genişletmeyi engelle
                if (e.ColumnIndex == historyListView.Columns.Count - 1)
                {
                    e.Cancel = true;
                    e.NewWidth = historyListView.Columns[e.ColumnIndex].Width;
                }
            };

            historyListView.SelectedIndexChanged += HistoryListView_SelectedIndexChanged;

            // Detail panel
            detailPanel = new Panel
            {
                Location = new Point(540, 60),
                Size = new Size(440, 580),
                BackColor = Color.FromArgb(55, 55, 58),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Screenshot
            screenshotPictureBox = new PictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(200, 150),
                BackColor = Color.FromArgb(70, 70, 73),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };
            screenshotPictureBox.Click += ScreenshotPictureBox_Click;

            // Tooltip
            toolTip = new ToolTip();
            toolTip.SetToolTip(screenshotPictureBox, "Görseli büyütmek için tıklayın");

            // Question title
            questionTitleLabel = new Label
            {
                Location = new Point(220, 10),
                Size = new Size(210, 60),
                ForeColor = Color.FromArgb(241, 241, 241),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = "Soru seçin...",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Answer label
            answerLabel = new Label
            {
                Location = new Point(220, 80),
                Size = new Size(210, 30),
                ForeColor = Color.FromArgb(46, 213, 115),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Text = "",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Question text
            questionTextBox = new TextBox
            {
                Location = new Point(10, 170),
                Size = new Size(420, 200),
                BackColor = Color.FromArgb(62, 62, 66),
                ForeColor = Color.FromArgb(241, 241, 241),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Multiline = true,
                WordWrap = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // View steps button
            viewStepsButton = new Button
            {
                Text = "Çözüm Adımlarını Görüntüle",
                Location = new Point(10, 380),
                Size = new Size(200, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Enabled = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            viewStepsButton.FlatAppearance.BorderSize = 0;
            viewStepsButton.Click += ViewStepsButton_Click;

            // Delete button
            deleteButton = new Button
            {
                Text = "Sil",
                Location = new Point(220, 380),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Enabled = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            deleteButton.FlatAppearance.BorderSize = 0;
            deleteButton.Click += DeleteButton_Click;

            // Add controls to detail panel
            detailPanel.Controls.Add(screenshotPictureBox);
            detailPanel.Controls.Add(questionTitleLabel);
            detailPanel.Controls.Add(answerLabel);
            detailPanel.Controls.Add(questionTextBox);
            detailPanel.Controls.Add(viewStepsButton);
            detailPanel.Controls.Add(deleteButton);

            // Add all controls to form
            this.Controls.Add(searchLabel);
            this.Controls.Add(searchTextBox);
            this.Controls.Add(statsLabel);
            this.Controls.Add(clearAllButton);
            this.Controls.Add(historyListView);
            this.Controls.Add(detailPanel);

            this.ResumeLayout(false);
        }

        private void LoadHistory()
        {
            historyListView.Items.Clear();
            var history = SolutionHistoryService.GetHistory();

            foreach (var item in history)
            {
                var listItem = new ListViewItem(item.Lecture);
                listItem.SubItems.Add(item.QuestionTitle);
                listItem.SubItems.Add(item.Answer);
                listItem.SubItems.Add(item.Timestamp.ToString("dd.MM.yyyy HH:mm"));
                listItem.SubItems.Add(item.UsedModel);
                listItem.Tag = item;

                historyListView.Items.Add(listItem);
            }

            statsLabel.Text = $"Toplam: {history.Count} çözüm";
        }

        private void SearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            historyListView.Items.Clear();
            var searchResults = SolutionHistoryService.SearchHistory(searchTextBox.Text);

            foreach (var item in searchResults)
            {
                var listItem = new ListViewItem(item.Lecture);
                listItem.SubItems.Add(item.QuestionTitle);
                listItem.SubItems.Add(item.Answer);
                listItem.SubItems.Add(item.Timestamp.ToString("dd.MM.yyyy HH:mm"));
                listItem.SubItems.Add(item.UsedModel);
                listItem.Tag = item;

                historyListView.Items.Add(listItem);
            }

            statsLabel.Text = $"Bulunan: {searchResults.Count} çözüm";
        }

        private void HistoryListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (historyListView.SelectedItems.Count > 0)
            {
                selectedItem = historyListView.SelectedItems[0].Tag as SolutionHistoryItem;
                DisplaySelectedItem();
            }
            else
            {
                selectedItem = null;
                ClearDetailPanel();
            }
        }

        private void DisplaySelectedItem()
        {
            if (selectedItem == null) return;

            // Başlıkta ders bilgisi varsa göster
            string titleWithLecture = !string.IsNullOrEmpty(selectedItem.Lecture)
                ? $"[{selectedItem.Lecture}] {selectedItem.QuestionTitle}"
                : selectedItem.QuestionTitle;
            questionTitleLabel.Text = titleWithLecture;
            answerLabel.Text = $"Cevap: {selectedItem.Answer}";

            // Newline karakterlerini Windows TextBox için düzenle
            var questionText = selectedItem.QuestionText;
            // Önce escape edilmiş \\n karakterlerini gerçek newline yap
            if (questionText.Contains("\\n"))
            {
                questionText = questionText.Replace("\\n", "\n");
            }
            // Sonra tüm \n'leri \r\n'e çevir (Windows TextBox için gerekli)
            questionText = questionText.Replace("\r\n", "\n").Replace("\n", "\r\n");
            questionTextBox.Text = questionText;

            // Mevcut resmi temizle
            if (screenshotPictureBox.Image != null)
            {
                screenshotPictureBox.Image.Dispose();
                screenshotPictureBox.Image = null;
            }

            // Screenshot yükle
            if (!string.IsNullOrEmpty(selectedItem.ScreenshotPath) && File.Exists(selectedItem.ScreenshotPath))
            {
                try
                {
                    // Dosyayı memory'ye kopyala, böylece dosya kilidi olmaz
                    using (var fileStream = new FileStream(selectedItem.ScreenshotPath, FileMode.Open, FileAccess.Read))
                    {
                        screenshotPictureBox.Image = Image.FromStream(fileStream);
                    }
                }
                catch
                {
                    screenshotPictureBox.Image = null;
                }
            }
            else
            {
                screenshotPictureBox.Image = null;
            }

            viewStepsButton.Enabled = !string.IsNullOrEmpty(selectedItem.SolutionSteps);
            deleteButton.Enabled = true;
        }

        private void ClearDetailPanel()
        {
            questionTitleLabel.Text = "Soru seçin...";
            answerLabel.Text = "";
            questionTextBox.Text = "";

            // Resmi güvenli şekilde temizle
            if (screenshotPictureBox.Image != null)
            {
                screenshotPictureBox.Image.Dispose();
                screenshotPictureBox.Image = null;
            }

            viewStepsButton.Enabled = false;
            deleteButton.Enabled = false;
        }

        private void ViewStepsButton_Click(object? sender, EventArgs e)
        {
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.SolutionSteps))
            {
                var stepsForm = new SolutionStepsForm(selectedItem.SolutionSteps);
                stepsForm.ShowDialog();
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (selectedItem != null)
            {
                var result = MessageBox.Show(
                    "Bu çözümü silmek istediğinizden emin misiniz?",
                    "Çözümü Sil",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // Seçili öğeyi temizle (resim dosyası kilitini kaldır)
                    ClearDetailPanel();

                    // Biraz bekle ki dosya kilidi tamamen kalksin
                    System.Threading.Thread.Sleep(100);

                    SolutionHistoryService.DeleteHistoryItem(selectedItem.Id);
                    LoadHistory();
                }
            }
        }

        private void ClearAllButton_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Tüm çözüm geçmişini silmek istediğinizden emin misiniz? Bu işlem geri alınamaz!",
                "Tüm Geçmişi Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // Tüm görselleri temizle
                ClearDetailPanel();

                // Biraz bekle ki dosya kilitleri tamamen kalksın
                System.Threading.Thread.Sleep(100);

                SolutionHistoryService.ClearHistory();
                LoadHistory();
            }
        }

        private void ScreenshotPictureBox_Click(object? sender, EventArgs e)
        {
            if (screenshotPictureBox.Image != null)
            {
                try
                {
                    // Resmin bir kopyasını oluştur ki dosya kilidi olmasın
                    using (var ms = new System.IO.MemoryStream())
                    {
                        screenshotPictureBox.Image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        var imageCopy = Image.FromStream(ms);

                        var imageViewer = new ImageViewerForm(imageCopy);
                        imageViewer.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Görsel görüntülenirken hata oluştu: {ex.Message}", "Hata",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // PictureBox'taki image'ı temizle
            if (screenshotPictureBox.Image != null)
            {
                screenshotPictureBox.Image.Dispose();
                screenshotPictureBox.Image = null;
            }

            // Tooltip'ı temizle
            toolTip?.Dispose();

            base.OnFormClosed(e);
        }
    }
}
