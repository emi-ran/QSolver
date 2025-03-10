using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace QSolver
{
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
            this.Size = new Size(250, 120);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // Formun ekran sınırları içinde kalmasını sağla
            Screen currentScreen = Screen.FromControl(this);
            Rectangle screenBounds = currentScreen.WorkingArea;
            int x = location.X;
            int y = location.Y;

            // Sağ kenardan taşma kontrolü
            if (x + this.Width > screenBounds.Right)
            {
                x = screenBounds.Right - this.Width;
            }

            // Alt kenardan taşma kontrolü
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
                Text = "Düzenle",
                Size = new Size(100, 30),
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
                Size = new Size(210, 30),
                Location = new Point(10, 80),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Green,
                ForeColor = Color.White,
                Visible = false
            };

            // Animasyon zamanlayıcısı
            animationTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };
            animationTimer.Tick += (s, e) =>
            {
                animationDots = (animationDots + 1) % 4;
                string dots = new string('.', animationDots);
                thinkingLabel.Text = currentState == FormState.Analyzing
                    ? $"Soru Analiz Ediliyor{dots}"
                    : $"Soru Çözülüyor{dots}";
            };
            animationTimer.Start();

            // Olay işleyicileri
            confirmButton.Click += ConfirmButton_Click;
            editButton.Click += EditButton_Click;
            solutionStepsButton.Click += SolutionStepsButton_Click;

            // Kontrolleri forma ekle
            this.Controls.Add(thinkingLabel);
            this.Controls.Add(resultLabel);
            this.Controls.Add(confirmButton);
            this.Controls.Add(editButton);
            this.Controls.Add(solutionStepsButton);

            // Escape tuşu ile formu kapat
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };

            // Analiz görevini başlat
            WaitForAnalysis(analysisTask);
        }

        private void ConfirmButton_Click(object? sender, EventArgs e)
        {
            if (currentState == FormState.Analyzed)
            {
                StartSolvingQuestion();
            }
            else if (currentState == FormState.Solved)
            {
                this.Close();
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Mevcut formu gizle
                this.Visible = false;

                // Düzenleme formunu göster
                using (var editForm = new QuestionEditForm(questionText))
                {
                    if (editForm.ShowDialog() == DialogResult.OK && editForm.IsEdited)
                    {
                        questionText = editForm.EditedQuestionText;
                        resultLabel.Text = "Soru düzenlendi. Çözüm için Onayla'ya tıklayın.";
                    }
                }

                // Mevcut formu tekrar göster
                this.Visible = true;
                this.BringToFront();
            }
            catch (Exception ex)
            {
                this.Visible = true;
                MessageBox.Show($"Düzenleme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SolutionStepsButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Mevcut formu gizle
                this.Visible = false;

                // Çözüm adımları formunu göster
                var stepsForm = new SolutionStepsForm(solutionText);

                // Form kapandığında mevcut formu tekrar göster
                stepsForm.FormClosed += (s, args) =>
                {
                    this.Visible = true;
                    this.BringToFront();
                };

                stepsForm.Show();
            }
            catch (Exception ex)
            {
                this.Visible = true;
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
                    questionText = "Soru bulunamadı.";

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
                        thinkingLabel.Text = "Soru Çözülüyor...";
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

            // Form kenarlarını çiz
            using (Pen pen = new Pen(Color.DarkGray, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer?.Stop();
                animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}