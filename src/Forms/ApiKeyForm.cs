using System;
using System.Drawing;
using System.Windows.Forms;

namespace QSolver
{
    public class ApiKeyForm : Form
    {
        private readonly ListView apiKeyListView;
        private readonly Button addButton;
        private readonly Button editButton;
        private readonly Button removeButton;
        private readonly Button closeButton;
        private readonly Panel buttonPanel;

        public ApiKeyForm()
        {
            this.Text = "API Anahtarları";
            this.Size = new Size(500, 350);
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
            buttonPanel.Controls.Add(closeButton);

            // ListView oluştur
            apiKeyListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                HideSelection = false // Seçim kaybolduktan sonra bile seçili göster
            };

            // ListView olaylarını ekle
            apiKeyListView.SelectedIndexChanged += ApiKeyListView_SelectedIndexChanged;
            apiKeyListView.Click += ApiKeyListView_Click;

            apiKeyListView.Columns.Add("API Anahtarı", 200);
            apiKeyListView.Columns.Add("Açıklama", 200);

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
            var apiKeys = ApiKeyManager.GetApiKeys();

            foreach (var apiKey in apiKeys)
            {
                var item = new ListViewItem(new[] { MaskApiKey(apiKey.Key), apiKey.Description });
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
            int columnWidth = (apiKeyListView.ClientSize.Width - 4) / 2;
            apiKeyListView.Columns[0].Width = columnWidth;
            apiKeyListView.Columns[1].Width = columnWidth;
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