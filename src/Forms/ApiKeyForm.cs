using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QSolver
{
    public class ApiKeyForm : Form
    {
        private readonly ListView apiKeyListView;
        private readonly Button addButton;
        private readonly Button editButton;
        private readonly Button removeButton;
        private readonly Button validateButton;
        private readonly Button closeButton;
        private readonly Panel buttonPanel;
        private readonly Dictionary<string, ApiKeyValidationResult> validationResults = new Dictionary<string, ApiKeyValidationResult>();

        public ApiKeyForm()
        {
            this.Text = "API Anahtarları";
            this.Size = new Size(550, 350);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.TopMost = true;

            // Butonlar için panel
            buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            // Ekle butonu
            addButton = new Button
            {
                Text = "Ekle",
                Width = 80,
                Height = 30,
                Location = new Point(10, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            addButton.Click += AddButton_Click;

            // Düzenle butonu
            editButton = new Button
            {
                Text = "Düzenle",
                Width = 80,
                Height = 30,
                Location = new Point(100, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = false // Başlangıçta devre dışı
            };
            editButton.Click += EditButton_Click;

            // Kaldır butonu
            removeButton = new Button
            {
                Text = "Kaldır",
                Width = 80,
                Height = 30,
                Location = new Point(190, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = false // Başlangıçta devre dışı
            };
            removeButton.Click += RemoveButton_Click;

            // Kontrol Et butonu
            validateButton = new Button
            {
                Text = "Kontrol Et",
                Width = 100,
                Height = 30,
                Location = new Point(280, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            validateButton.Click += ValidateButton_Click;

            // Kapat butonu
            closeButton = new Button
            {
                Text = "Kapat",
                Width = 80,
                Height = 30,
                Location = new Point(this.ClientSize.Width - 90, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            closeButton.Click += (s, e) => this.Close();

            // Kontrolleri panele ekle
            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(editButton);
            buttonPanel.Controls.Add(removeButton);
            buttonPanel.Controls.Add(validateButton);
            buttonPanel.Controls.Add(closeButton);

            // ListView oluştur
            apiKeyListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                HideSelection = false, // Seçim kaybolduktan sonra bile seçili göster
                OwnerDraw = true // Özel çizim için
            };

            // ListView olaylarını ekle
            apiKeyListView.SelectedIndexChanged += ApiKeyListView_SelectedIndexChanged;
            apiKeyListView.Click += ApiKeyListView_Click;
            apiKeyListView.DrawItem += ApiKeyListView_DrawItem;
            apiKeyListView.DrawSubItem += ApiKeyListView_DrawSubItem;
            apiKeyListView.DrawColumnHeader += ApiKeyListView_DrawColumnHeader;

            apiKeyListView.Columns.Add("API Anahtarı", 180);
            apiKeyListView.Columns.Add("Açıklama", 150);
            apiKeyListView.Columns.Add("Durum", 150);

            // Kontrolleri forma ekle - Önce panel, sonra ListView ekleyin
            this.Controls.Add(buttonPanel);
            this.Controls.Add(apiKeyListView);

            // API anahtarlarını yükle
            LoadApiKeys();

            // Escape tuşu ile formu kapat
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };
        }

        private void ApiKeyListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void ApiKeyListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        private void ApiKeyListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null) return;

            var apiKey = e.Item.Tag as ApiKey;
            if (apiKey == null) return;

            // Arka plan rengini belirle
            Color backgroundColor = e.Item.Selected ? SystemColors.Highlight : SystemColors.Window;

            // Validation sonucuna göre renk ayarla (sadece geçersiz veya rate limit durumunda)
            if (validationResults.ContainsKey(apiKey.Key))
            {
                var result = validationResults[apiKey.Key];
                if (!e.Item.Selected)
                {
                    switch (result.Status)
                    {
                        case ApiKeyStatus.Valid:
                            backgroundColor = Color.FromArgb(220, 255, 220); // Açık yeşil
                            break;
                        case ApiKeyStatus.Invalid:
                            backgroundColor = Color.FromArgb(255, 220, 220); // Açık kırmızı
                            break;
                        case ApiKeyStatus.RateLimit:
                            backgroundColor = Color.FromArgb(255, 255, 200); // Açık sarı
                            break;
                    }
                }
            }

            // Arka planı çiz
            using (SolidBrush brush = new SolidBrush(backgroundColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // Metin rengini belirle
            Color textColor = e.Item.Selected ? SystemColors.HighlightText : SystemColors.WindowText;

            // Metni çiz
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem?.Text ?? "",
                e.Item.Font,
                e.Bounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );

            // Seçili öğe için kenarlık çiz
            if (e.Item.Selected)
            {
                e.Graphics.DrawRectangle(SystemPens.Highlight, e.Bounds);
            }
        }

        private async void ValidateButton_Click(object? sender, EventArgs e)
        {
            validateButton.Enabled = false;
            validateButton.Text = "Kontrol ediliyor...";
            validationResults.Clear();

            try
            {
                var results = await ApiKeyValidator.ValidateAllApiKeysAsync();
                var apiKeys = ApiKeyManager.GetApiKeys();

                for (int i = 0; i < results.Length && i < apiKeys.Count; i++)
                {
                    validationResults[apiKeys[i].Key] = results[i];

                    // ListView'deki ilgili öğeyi güncelle
                    if (i < apiKeyListView.Items.Count)
                    {
                        var item = apiKeyListView.Items[i];
                        if (item.SubItems.Count > 2)
                        {
                            item.SubItems[2].Text = results[i].Message;
                        }
                        else
                        {
                            item.SubItems.Add(results[i].Message);
                        }
                    }
                }

                // ListView'i yeniden çiz
                apiKeyListView.Invalidate();

                MessageBox.Show(
                    "API anahtarları kontrol edildi.",
                    "Bilgi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"API anahtarları kontrol edilirken hata oluştu: {ex.Message}",
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                validateButton.Enabled = true;
                validateButton.Text = "Kontrol Et";
            }
        }

        private void ApiKeyListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        private void ApiKeyListView_Click(object? sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        private void LoadApiKeys()
        {
            apiKeyListView.Items.Clear();
            validationResults.Clear();
            var apiKeys = ApiKeyManager.GetApiKeys();

            foreach (var apiKey in apiKeys)
            {
                var item = new ListViewItem(new[] { MaskApiKey(apiKey.Key), apiKey.Description, "" });
                item.Tag = apiKey;
                apiKeyListView.Items.Add(item);
            }

            UpdateButtonStates();
        }

        private string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
            {
                return apiKey;
            }

            // İlk 4 ve son 4 karakteri göster, arasını maskele
            return apiKey.Substring(0, 4) + "..." + apiKey.Substring(apiKey.Length - 4);
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = apiKeyListView.SelectedItems.Count > 0;
            Console.WriteLine($"Seçili öğe sayısı: {apiKeyListView.SelectedItems.Count}");
            editButton.Enabled = hasSelection;
            removeButton.Enabled = hasSelection;
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            using (var form = new ApiKeyEditForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    ApiKeyManager.AddApiKey(form.ApiKey);
                    LoadApiKeys();
                }
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            if (apiKeyListView.SelectedItems.Count > 0)
            {
                var selectedItem = apiKeyListView.SelectedItems[0];
                ApiKey? apiKey = selectedItem.Tag as ApiKey;
                int index = apiKeyListView.Items.IndexOf(selectedItem);

                if (apiKey != null)
                {
                    using (var form = new ApiKeyEditForm(apiKey))
                    {
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            ApiKeyManager.UpdateApiKey(index, form.ApiKey);
                            LoadApiKeys();
                        }
                    }
                }
            }
        }

        private void RemoveButton_Click(object? sender, EventArgs e)
        {
            if (apiKeyListView.SelectedItems.Count > 0)
            {
                var selectedItem = apiKeyListView.SelectedItems[0];
                ApiKey? apiKey = selectedItem.Tag as ApiKey;

                if (apiKey != null)
                {
                    var result = MessageBox.Show(
                        "Seçili API anahtarını silmek istediğinizden emin misiniz?",
                        "Onay",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        ApiKeyManager.RemoveApiKey(apiKey);
                        LoadApiKeys();
                    }
                }
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            UpdateButtonStates();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (closeButton != null && buttonPanel != null && this.ClientSize.Width > 0)
            {
                closeButton.Location = new Point(this.ClientSize.Width - 90, 10);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Form yüklendiğinde sütun genişliklerini ayarla
            int totalWidth = apiKeyListView.ClientSize.Width - 4;
            apiKeyListView.Columns[0].Width = (int)(totalWidth * 0.35);
            apiKeyListView.Columns[1].Width = (int)(totalWidth * 0.35);
            apiKeyListView.Columns[2].Width = (int)(totalWidth * 0.30);
        }
    }

    public class ApiKeyEditForm : Form
    {
        private readonly TextBox keyTextBox;
        private readonly TextBox descriptionTextBox;
        private readonly Button saveButton;
        private readonly Button cancelButton;

        public ApiKey ApiKey { get; private set; }

        public ApiKeyEditForm(ApiKey? apiKey = null)
        {
            ApiKey = apiKey ?? new ApiKey();

            this.Text = apiKey == null ? "API Anahtarı Ekle" : "API Anahtarı Düzenle";
            this.Size = new Size(450, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.TopMost = true;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // API Anahtarı etiketi
            var keyLabel = new Label
            {
                Text = "API Anahtarı:",
                Location = new Point(20, 25),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            // API Anahtarı metin kutusu
            keyTextBox = new TextBox
            {
                Location = new Point(120, 25),
                Width = 300,
                Height = 25,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Text = ApiKey.Key
            };

            // Açıklama etiketi
            var descriptionLabel = new Label
            {
                Text = "Açıklama:",
                Location = new Point(20, 65),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            // Açıklama metin kutusu
            descriptionTextBox = new TextBox
            {
                Location = new Point(120, 65),
                Width = 300,
                Height = 25,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Text = ApiKey.Description
            };

            // Kaydet butonu
            saveButton = new Button
            {
                Text = "Kaydet",
                DialogResult = DialogResult.OK,
                Location = new Point(230, 120),
                Width = 90,
                Height = 30,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            saveButton.Click += SaveButton_Click;

            // İptal butonu
            cancelButton = new Button
            {
                Text = "İptal",
                DialogResult = DialogResult.Cancel,
                Location = new Point(330, 120),
                Width = 90,
                Height = 30,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            // Kontrolleri forma ekle
            this.Controls.Add(keyLabel);
            this.Controls.Add(keyTextBox);
            this.Controls.Add(descriptionLabel);
            this.Controls.Add(descriptionTextBox);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);

            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(keyTextBox.Text))
            {
                MessageBox.Show("API anahtarı boş olamaz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
                return;
            }

            ApiKey.Key = keyTextBox.Text.Trim();
            ApiKey.Description = descriptionTextBox.Text.Trim();
        }
    }
}