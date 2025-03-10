using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QSolver
{
    public class GeminiRequest
    {
        public required Content[] contents { get; set; }
    }

    public class Content
    {
        public required Part[] parts { get; set; }
    }

    public class Part
    {
        public string? text { get; set; }
        public InlineData? inline_data { get; set; }
    }

    public class InlineData
    {
        public required string mime_type { get; set; }
        public required string data { get; set; }
    }

    public class GeminiService
    {
        private readonly string apiKey;
        private readonly HttpClient httpClient;
        private const string API_URL_BASE = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string OCR_MODEL = "gemini-2.0-flash";
        private const string SOLVER_MODEL = "gemini-2.0-flash";

        public GeminiService(string apiKey)
        {
            this.apiKey = apiKey;
            this.httpClient = new HttpClient();
        }

        public async Task<string> AnalyzeImage(string base64Image)
        {
            try
            {
                // API isteği için JSON hazırla
                var request = new GeminiRequest
                {
                    contents = new[]
                    {
                        new Content
                        {
                            parts = new[]
                            {
                                new Part { text = "Bu görselde ne yazıyor? Lütfen sadece görseldeki metni aynen yaz. Görselde yazanlar dışında hiçbir ek açıklama ekleme. Eğer görselde hiçbir yazı yoksa sadece 'NO_TEXT_FOUND' yaz." },
                                new Part
                                {
                                    inline_data = new InlineData
                                    {
                                        mime_type = "image/png",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };

                string jsonRequest = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // API'ye istek gönder
                var response = await httpClient.PostAsync($"{API_URL_BASE}/{OCR_MODEL}:generateContent?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    return $"API hatası: HTTP {(int)response.StatusCode} - {errorContent}";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Yanıtı işle
                using JsonDocument document = JsonDocument.Parse(jsonResponse);
                var root = document.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentElement) &&
                    contentElement.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    string result = textElement.GetString() ?? "Yanıt alınamadı.";

                    // ``` işaretlerini kaldır
                    result = result.Replace("```", "");

                    // Başındaki "text" kelimesini kaldır
                    if (result.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                    {
                        result = result.Substring(4).TrimStart();
                    }

                    // Görselde yazı yoksa özel JSON yanıtı döndür
                    if (result.Contains("NO_TEXT_FOUND") ||
                        string.IsNullOrWhiteSpace(result) ||
                        result.Contains("görselde yazı yok", StringComparison.OrdinalIgnoreCase) ||
                        result.Contains("görselde metin yok", StringComparison.OrdinalIgnoreCase) ||
                        result.Contains("görselde hiçbir yazı yok", StringComparison.OrdinalIgnoreCase))
                    {
                        return "{\"question_not_found\":\"try_again\"}";
                    }

                    return string.IsNullOrWhiteSpace(result) ? "Görselde metin bulunamadı." : result;
                }

                return "Yanıt işlenemedi. API yanıtı beklenen formatta değil.";
            }
            catch (Exception ex)
            {
                return $"Hata oluştu: {ex.Message}";
            }
        }

        public async Task<(string fullResponse, string answer)> SolveQuestion(string questionText)
        {
            try
            {
                // API isteği için JSON hazırla
                var request = new GeminiRequest
                {
                    contents = new[]
                    {
                        new Content
                        {
                            parts = new[]
                            {
                                new Part
                                {
                                    text = @"Sen bir soru çözme yapay zekasısın. Aşağıdaki soruyu analiz edip adım adım detaylıca çöz. 
Çözümün sonunda hangi şıkkın doğru olduğunu belirt. 
Cevabını şu formatta bitir: { ""solved"":""true"", ""answer"":""X"" } (X yerine doğru şıkkı yaz, eğer çözemediysen solved kısmını false yap).

Soru:
" + questionText
                                }
                            }
                        }
                    }
                };

                string jsonRequest = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // API'ye istek gönder
                var response = await httpClient.PostAsync($"{API_URL_BASE}/{SOLVER_MODEL}:generateContent?key={apiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    return ("API hatası: " + response.StatusCode, "Hata");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Yanıtı işle
                using JsonDocument document = JsonDocument.Parse(jsonResponse);
                var root = document.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentElement) &&
                    contentElement.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    string fullResponse = textElement.GetString() ?? "Yanıt alınamadı.";
                    string answer = ExtractAnswer(fullResponse);
                    return (fullResponse, answer);
                }

                return ("Yanıt işlenemedi.", "Hata");
            }
            catch (Exception ex)
            {
                return ($"Hata oluştu: {ex.Message}", "Hata");
            }
        }

        private string ExtractAnswer(string response)
        {
            try
            {
                // JSON formatındaki cevabı bul
                int jsonStart = response.LastIndexOf('{');
                int jsonEnd = response.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string jsonPart = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    // JSON'ı parse et
                    using JsonDocument document = JsonDocument.Parse(jsonPart);
                    var root = document.RootElement;

                    if (root.TryGetProperty("answer", out var answerElement))
                    {
                        return answerElement.GetString() ?? "?";
                    }
                }

                return "?";
            }
            catch
            {
                return "?";
            }
        }
    }

    public class ResultForm : Form
    {
        private readonly Label thinkingLabel;
        private readonly Label resultLabel;
        private readonly Button confirmButton;
        private readonly Button editButton;
        private readonly Button solutionStepsButton;
        private readonly System.Windows.Forms.Timer animationTimer;
        private int animationDots = 0;

        private string questionText = string.Empty;
        private string solutionText = string.Empty;
        private string answerLetter = string.Empty;
        private bool isQuestionEdited = false;

        private enum FormState
        {
            Analyzing,
            Analyzed,
            Solving,
            Solved
        }

        private FormState currentState;

        public ResultForm(Point location, Task<string> analysisTask)
        {
            // Form özellikleri
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(250, 120);
            this.TopMost = true;
            this.ShowInTaskbar = false;

            // Formun ekran sınırları içinde kalmasını sağla
            Rectangle screenBounds = Screen.FromPoint(location).WorkingArea;
            int x = location.X;
            int y = location.Y;

            // Sağ kenar kontrolü
            if (x + this.Width > screenBounds.Right)
            {
                x = screenBounds.Right - this.Width;
            }

            // Alt kenar kontrolü
            if (y + this.Height > screenBounds.Bottom)
            {
                y = screenBounds.Bottom - this.Height;
            }

            this.Location = new Point(x, y);
            currentState = FormState.Analyzing;

            // Düşünme etiketi
            thinkingLabel = new Label
            {
                Text = "Soru Analiz Ediliyor",
                AutoSize = true,
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Visible = true
            };

            // Sonuç etiketi (başlangıçta gizli)
            resultLabel = new Label
            {
                Text = "Soru Analiz Edildi",
                AutoSize = true,
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Visible = false
            };

            // Onay butonu (başlangıçta gizli)
            confirmButton = new Button
            {
                Text = "Onayla",
                Size = new Size(100, 30),
                Location = new Point(10, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.DeepSkyBlue,
                ForeColor = Color.White,
                Visible = false
            };

            // Düzenleme butonu (başlangıçta gizli)
            editButton = new Button
            {
                Text = "Soruyu Düzenle",
                Size = new Size(120, 30),
                Location = new Point(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Orange,
                ForeColor = Color.White,
                Visible = false
            };

            // Çözüm adımları butonu (başlangıçta gizli)
            solutionStepsButton = new Button
            {
                Text = "Çözüm Adımları",
                Size = new Size(120, 30),
                Location = new Point(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Green,
                ForeColor = Color.White,
                Visible = false
            };

            confirmButton.Click += ConfirmButton_Click;
            editButton.Click += EditButton_Click;
            solutionStepsButton.Click += SolutionStepsButton_Click;

            // Animasyon için timer
            animationTimer = new System.Windows.Forms.Timer
            {
                Interval = 500 // Her 500ms'de bir güncelle
            };

            animationTimer.Tick += (s, e) =>
            {
                // Nokta animasyonu
                animationDots = (animationDots + 1) % 4;
                thinkingLabel.Text = "Soru Analiz Ediliyor" + new string('.', animationDots);
            };

            // Kontrolleri forma ekle
            this.Controls.Add(thinkingLabel);
            this.Controls.Add(resultLabel);
            this.Controls.Add(confirmButton);
            this.Controls.Add(editButton);
            this.Controls.Add(solutionStepsButton);

            // Form stil ayarları
            this.BackColor = Color.White;

            // Timer'ı başlat
            animationTimer.Start();

            // API yanıtını bekle
            WaitForAnalysis(analysisTask);
        }

        private void ConfirmButton_Click(object? sender, EventArgs e)
        {
            if (currentState == FormState.Analyzed)
            {
                // Soruyu çözmeye başla
                StartSolvingQuestion();
            }
            else if (currentState == FormState.Solved)
            {
                // İşlemi tamamla
                this.Close();
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Düzenleme formunu göstermeden önce mevcut formu geçici olarak gizle
                this.Visible = false;

                using (var editForm = new QuestionEditForm(questionText))
                {
                    if (editForm.ShowDialog() == DialogResult.OK)
                    {
                        questionText = editForm.EditedQuestionText;
                        isQuestionEdited = editForm.IsEdited;

                        // Düzenleme başarılı olduğunda bilgi ver
                        MessageBox.Show("Soru metni başarıyla düzenlendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                // Düzenleme işlemi bittikten sonra formu tekrar göster
                this.Visible = true;
                this.BringToFront();
            }
            catch (Exception ex)
            {
                this.Visible = true; // Hata durumunda da formu göster
                MessageBox.Show($"Soru düzenlenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SolutionStepsButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Çözüm adımları formunu göstermeden önce mevcut formu geçici olarak gizle
                this.Visible = false;

                var solutionForm = new SolutionStepsForm(solutionText);
                solutionForm.FormClosed += (s, args) =>
                {
                    // Çözüm adımları formu kapandığında ana formu tekrar göster
                    this.Visible = true;
                    this.BringToFront();
                };
                solutionForm.Show();
            }
            catch (Exception ex)
            {
                this.Visible = true; // Hata durumunda da formu göster
                MessageBox.Show($"Çözüm adımları gösterilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void WaitForAnalysis(Task<string> analysisTask)
        {
            try
            {
                questionText = await analysisTask;

                // Özel JSON yanıtını kontrol et
                if (questionText.Contains("\"question_not_found\""))
                {
                    questionText = "Görselde soru bulunamadı.";

                    if (this.IsHandleCreated)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            animationTimer.Stop();
                            thinkingLabel.Visible = false;
                            resultLabel.Text = questionText;
                            resultLabel.Visible = true;
                            confirmButton.Visible = false;
                            editButton.Visible = false;

                            // 2 saniye sonra formu kapat
                            var closeTimer = new System.Windows.Forms.Timer();
                            closeTimer.Interval = 2000;
                            closeTimer.Tick += (s, e) =>
                            {
                                closeTimer.Stop();
                                this.Close();
                            };
                            closeTimer.Start();
                        });
                    }
                    return;
                }

                // Eğer yanıt boş veya hata içeriyorsa
                if (string.IsNullOrEmpty(questionText) || questionText.StartsWith("Hata oluştu:") || questionText == "Yanıt işlenemedi.")
                {
                    questionText = "Görsel analiz edilemedi. Lütfen tekrar deneyin veya metni manuel olarak düzenleyin.";
                }

                // Analiz tamamlandığında UI'ı güncelle
                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;
                        resultLabel.Visible = true;
                        confirmButton.Visible = true;
                        editButton.Visible = true;

                        currentState = FormState.Analyzed;
                    });
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda UI'ı güncelle
                questionText = $"Hata oluştu: {ex.Message}";

                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;
                        resultLabel.Text = "Analiz sırasında hata oluştu";
                        resultLabel.Visible = true;
                        confirmButton.Visible = true;
                        editButton.Visible = true;

                        currentState = FormState.Analyzed;
                    });
                }
            }
        }

        private async void StartSolvingQuestion()
        {
            try
            {
                currentState = FormState.Solving;

                // UI'ı güncelle
                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        thinkingLabel.Text = "Soru çözülüyor...";
                        thinkingLabel.Visible = true;
                        resultLabel.Visible = false;
                        confirmButton.Visible = false;
                        editButton.Visible = false;
                        solutionStepsButton.Visible = false;
                        animationTimer.Start();
                    });
                }

                // Soruyu çöz
                var (fullResponse, answer) = await Program.GetGeminiService().SolveQuestion(questionText);

                // JSON formatında cevap var mı kontrol et
                if (answer == "?" || answer == "Hata")
                {
                    // JSON formatında cevap yoksa tekrar dene
                    if (this.IsHandleCreated)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            thinkingLabel.Text = "Geçersiz istek, tekrar deneniyor...";
                        });
                    }

                    // Tekrar dene
                    (fullResponse, answer) = await Program.GetGeminiService().SolveQuestion(questionText);

                    // Hala JSON formatında cevap yoksa
                    if (answer == "?" || answer == "Hata")
                    {
                        solutionText = fullResponse;
                        answerLetter = "Yapay zeka gerekli cevabı veremedi";

                        if (this.IsHandleCreated)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                animationTimer.Stop();
                                thinkingLabel.Visible = false;
                                resultLabel.Text = "Cevap: " + answerLetter;
                                resultLabel.Visible = true;
                                confirmButton.Visible = true;
                                solutionStepsButton.Visible = true;

                                currentState = FormState.Solved;
                            });
                        }
                        return;
                    }
                }

                solutionText = fullResponse;
                answerLetter = answer;

                // UI'ı güncelle
                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;
                        resultLabel.Text = "Cevap: " + answerLetter;
                        resultLabel.Visible = true;
                        confirmButton.Visible = true;
                        solutionStepsButton.Visible = true;

                        currentState = FormState.Solved;
                    });
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda UI'ı güncelle
                solutionText = $"Hata oluştu: {ex.Message}";
                answerLetter = "Hata";

                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;
                        resultLabel.Text = "Çözüm sırasında hata oluştu";
                        resultLabel.Visible = true;
                        confirmButton.Visible = true;

                        currentState = FormState.Solved;
                    });
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Form kenarına çerçeve çiz
            using (Pen pen = new Pen(Color.DeepSkyBlue, 2))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class DoubleBufferedForm : Form
    {
        public DoubleBufferedForm()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer, true);
        }
    }

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

    public class SolutionStepsForm : Form
    {
        public SolutionStepsForm(string solutionText)
        {
            this.Text = "Çözüm Adımları";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true; // Formu en üstte tut

            // JSON kısmını kaldır
            int jsonIndex = solutionText.LastIndexOf("json{");
            if (jsonIndex > 0)
            {
                solutionText = solutionText.Substring(0, jsonIndex);
            }

            // Doğrudan JSON formatındaki cevabı kaldır
            int braceIndex = solutionText.LastIndexOf("{");
            if (braceIndex > 0 && solutionText.Substring(braceIndex).Contains("\"solved\"") && solutionText.Substring(braceIndex).Contains("\"answer\""))
            {
                solutionText = solutionText.Substring(0, braceIndex).TrimEnd();
            }

            // WebBrowser kontrolü oluştur
            var webBrowser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = true
            };

            // Markdown'ı HTML'e dönüştür
            string htmlContent = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body {{ 
                        font-family: Arial, sans-serif; 
                        line-height: 1.6;
                        margin: 20px;
                        background-color: #f9f9f9;
                    }}
                    h1, h2, h3 {{ color: #333; }}
                    pre {{ 
                        background-color: #f0f0f0; 
                        padding: 10px; 
                        border-radius: 5px;
                        overflow-x: auto;
                    }}
                    code {{ 
                        font-family: Consolas, monospace; 
                        background-color: #f0f0f0;
                        padding: 2px 4px;
                        border-radius: 3px;
                    }}
                    blockquote {{
                        border-left: 4px solid #ddd;
                        padding-left: 10px;
                        color: #666;
                    }}
                    table {{
                        border-collapse: collapse;
                        width: 100%;
                    }}
                    th, td {{
                        border: 1px solid #ddd;
                        padding: 8px;
                    }}
                    th {{
                        background-color: #f2f2f2;
                    }}
                    img {{ max-width: 100%; }}
                    p {{ margin-bottom: 15px; }}
                </style>
            </head>
            <body>
                {ConvertMarkdownToHtml(solutionText)}
            </body>
            </html>";

            webBrowser.DocumentText = htmlContent;

            // Kapat butonu
            var closeButton = new Button
            {
                Text = "Kapat",
                Width = 100,
                Height = 30,
                Dock = DockStyle.Bottom,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            closeButton.Click += (s, e) => this.Close();

            // Kontrolleri forma ekle
            this.Controls.Add(webBrowser);
            this.Controls.Add(closeButton);

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

        // Basit bir Markdown to HTML dönüştürücü
        private string ConvertMarkdownToHtml(string markdown)
        {
            // Başlıklar
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"^# (.+)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"^## (.+)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"^### (.+)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Kalın
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

            // İtalik
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\*(.+?)\*", "<em>$1</em>");

            // Kod blokları
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"```(.+?)```", "<pre><code>$1</code></pre>", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Satır içi kod
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"`(.+?)`", "<code>$1</code>");

            // Listeler
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"^\* (.+)$", "<ul><li>$1</li></ul>", System.Text.RegularExpressions.RegexOptions.Multiline);
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"^(\d+)\. (.+)$", "<ol><li>$2</li></ol>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Paragraflar ve satır sonları
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"^\s*$\n", "</p><p>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Çift satır sonları paragraf olarak işle
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n\n", "</p><p>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Tek satır sonları <br> olarak işle
            markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n", "<br>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Tüm metni paragraf içine al
            markdown = "<p>" + markdown + "</p>";

            // Fazladan oluşan paragrafları temizle
            markdown = markdown.Replace("<p></p>", "");

            return markdown;
        }
    }

    public class Program
    {
        private readonly NotifyIcon trayIcon;
        private DoubleBufferedForm? captureForm;
        private Point startPoint;
        private Rectangle selectionRect;
        private bool isSelecting;
        private readonly GeminiService geminiService;

        // Statik gemini servisi referansı
        private static GeminiService? staticGeminiService;

        public Program()
        {
            geminiService = new GeminiService("AIzaSyAgRG_98cIwNlvtrtKVyZy3fCeZYmGW9Uo");
            staticGeminiService = geminiService;

            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "QSolver"
            };

            // Context menu oluşturma
            trayIcon.ContextMenuStrip.Items.Add("Soru Seç", null, CaptureScreen_Click);
            trayIcon.ContextMenuStrip.Items.Add("Çıkış", null, Exit_Click);
        }

        // Statik gemini servisi erişimi için metot
        public static GeminiService GetGeminiService()
        {
            if (staticGeminiService == null)
            {
                staticGeminiService = new GeminiService("AIzaSyAgRG_98cIwNlvtrtKVyZy3fCeZYmGW9Uo");
            }
            return staticGeminiService;
        }

        private void CaptureScreen_Click(object? sender, EventArgs e)
        {
            captureForm = new DoubleBufferedForm
            {
                WindowState = FormWindowState.Maximized,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.Black,
                Opacity = 0.3,
                ShowInTaskbar = false,
                TopMost = true
            };

            captureForm.KeyPress += (s, ev) =>
            {
                if (ev.KeyChar == (char)Keys.Escape)
                {
                    captureForm.Close();
                }
            };

            captureForm.MouseDown += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    isSelecting = true;
                    startPoint = ev.Location;
                    selectionRect = new Rectangle();
                }
            };

            captureForm.MouseMove += (s, ev) =>
            {
                if (isSelecting)
                {
                    Rectangle oldRect = selectionRect;
                    int x = Math.Min(startPoint.X, ev.X);
                    int y = Math.Min(startPoint.Y, ev.Y);
                    int width = Math.Abs(ev.X - startPoint.X);
                    int height = Math.Abs(ev.Y - startPoint.Y);
                    selectionRect = new Rectangle(x, y, width, height);

                    Rectangle invalidateRect = Rectangle.Union(oldRect, selectionRect);
                    invalidateRect.Inflate(2, 2);
                    captureForm.Invalidate(invalidateRect);
                }
            };

            captureForm.MouseUp += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    isSelecting = false;
                    captureForm.Opacity = 0;
                    Application.DoEvents();
                    CaptureRegion();
                    captureForm.Close();
                }
            };

            captureForm.Paint += (s, ev) =>
            {
                if (isSelecting && selectionRect.Width > 0 && selectionRect.Height > 0)
                {
                    ev.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // Dış çerçeve (gölge efekti için)
                    using (Pen outerPen = new Pen(Color.Black, 4))
                    {
                        ev.Graphics.DrawRectangle(outerPen, selectionRect);
                    }

                    // İç çerçeve
                    using (Pen innerPen = new Pen(Color.DeepSkyBlue, 3))
                    {
                        ev.Graphics.DrawRectangle(innerPen, selectionRect);
                    }

                    // Boyut bilgisi
                    string dimensions = $"{selectionRect.Width} x {selectionRect.Height}";
                    using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
                    {
                        // Metin boyutunu ölç
                        SizeF textSize = ev.Graphics.MeasureString(dimensions, font);

                        // Metin için arka plan konumu
                        int textX = selectionRect.Right - (int)textSize.Width - 10;
                        int textY = selectionRect.Bottom + 5;

                        // Metin arka planı
                        using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(64, 0, 0, 0)))
                        {
                            ev.Graphics.FillRectangle(backBrush,
                                textX - 5, textY - 2,
                                textSize.Width + 10, textSize.Height + 4);
                        }

                        // Metin
                        using (SolidBrush textBrush = new SolidBrush(Color.White))
                        {
                            ev.Graphics.DrawString(dimensions, font, textBrush, textX, textY);
                        }
                    }
                }
            };

            captureForm.Show();
        }

        private async void CaptureRegion()
        {
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0) return;

            using (Bitmap bitmap = new Bitmap(selectionRect.Width, selectionRect.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(selectionRect.Location, Point.Empty, selectionRect.Size);
                }

                // Görseli bellekte tutarak base64'e çevir
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    byte[] imageBytes = ms.ToArray();
                    string base64Image = Convert.ToBase64String(imageBytes);

                    // Sonuç formunun konumunu belirle
                    Control? controlForScreen = captureForm ?? Form.ActiveForm;
                    if (controlForScreen == null && Application.OpenForms.Count > 0)
                    {
                        controlForScreen = Application.OpenForms[0];
                    }

                    // Eğer hala null ise, varsayılan ekranı kullan
                    Screen currentScreen = controlForScreen != null
                        ? Screen.FromControl(controlForScreen)
                        : Screen.PrimaryScreen ?? Screen.AllScreens[0];

                    Rectangle screenBounds = currentScreen.WorkingArea;

                    // Sağ kenarın taşmadığını kontrol et
                    int x = screenBounds.Right - 250; // Form genişliği 250
                    if (selectionRect.Right + 250 <= screenBounds.Right)
                    {
                        x = selectionRect.Right - 125; // Sağ kenarın ortasında
                    }

                    int y = screenBounds.Height / 2 - 60; // Form yüksekliği 120 olduğu için yarısı

                    Point resultLocation = new Point(x, y);

                    // API çağrısını başlat
                    var analysisTask = geminiService.AnalyzeImage(base64Image);

                    // Sonuç formunu göster ve analiz task'ını ilet
                    ResultForm resultForm = new ResultForm(resultLocation, analysisTask);
                    resultForm.Show();

                    // API yanıtını bekle ve bir değişkende sakla
                    string response = await analysisTask;
                    // Cevabı bir değişkende saklayabilirsiniz
                    string analyzedText = response;
                }
            }
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Program program = new Program();
            Application.Run();
        }
    }
}
