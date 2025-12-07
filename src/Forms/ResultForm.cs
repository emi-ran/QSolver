using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;

namespace QSolver
{
    public class ResultForm : Form
    {
        private Label thinkingLabel = null!;
        private Label resultLabel = null!;
        private Button confirmButton = null!;
        private Button editButton = null!;
        private Button solutionStepsButton = null!;
        private System.Windows.Forms.Timer animationTimer = null!;
        private int animationDots = 0;

        private string questionText = string.Empty;
        private string solutionText = string.Empty;
        private string answerLetter = string.Empty;
        private string lectureText = string.Empty;
        private byte[]? screenshotData;
        private bool isTurboMode;

        private enum FormState
        {
            Analyzing,
            Analyzed,
            Solving,
            Solved
        }

        private FormState currentState;

        private System.Windows.Forms.Timer fadeTimer = null!;
        private float currentOpacity = 0.0f;
        private bool isFadingIn = true;

        public ResultForm(Point location, Task<string> analysisTask, byte[]? screenshotData = null, bool isTurboMode = false)
        {
            this.screenshotData = screenshotData;
            this.isTurboMode = isTurboMode;
            InitializeForm(location);

            // Analiz görevini başlat
            WaitForAnalysis(analysisTask);
        }

        // Turbo mode için constructor
        public ResultForm(Point location, Task<(string fullResponse, string answer, string title, string lecture)> directSolveTask, byte[]? screenshotData = null, bool isTurboMode = true)
        {
            this.screenshotData = screenshotData;
            this.isTurboMode = isTurboMode;
            InitializeForm(location);

            // Doğrudan çözme görevini başlat
            WaitForDirectSolve(directSolveTask);
        }

        private void InitializeForm(Point location)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.SupportsTransparentBackColor, true);

            UpdateStyles();

            this.Size = new Size(280, 150);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0;

            // Form yuvarlak köşeli olsun
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 15, 15));

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
                Text = QSolver.Services.LocalizationService.Get("Result.Analyze"),
                AutoSize = true,
                Location = new Point(20, 20),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Visible = true
            };

            // Sonuç etiketi
            resultLabel = new Label
            {
                Text = QSolver.Services.LocalizationService.Get("Result.Analyzed"),
                AutoSize = true,
                Location = new Point(20, 20),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Visible = false
            };

            // Onay butonu
            confirmButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Result.SolveButton"),
                Size = new Size(110, 35),
                Location = new Point(20, 55),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(64, 156, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Visible = false
            };
            confirmButton.FlatAppearance.BorderSize = 0;
            confirmButton.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, confirmButton.Width, confirmButton.Height, 10, 10));

            // Düzenleme butonu
            editButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Common.Edit"),
                Size = new Size(110, 35),
                Location = new Point(140, 55),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 159, 67),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Visible = false
            };
            editButton.FlatAppearance.BorderSize = 0;
            editButton.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, editButton.Width, editButton.Height, 10, 10));

            // Çözüm adımları butonu
            solutionStepsButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Result.StepsButton"),
                Size = new Size(240, 35),
                Location = new Point(20, 100),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 213, 115),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Visible = false
            };
            solutionStepsButton.FlatAppearance.BorderSize = 0;
            solutionStepsButton.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, solutionStepsButton.Width, solutionStepsButton.Height, 10, 10));

            // Fade-in/out animasyonu için timer
            fadeTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
            fadeTimer.Tick += (s, e) =>
            {
                if (isFadingIn)
                {
                    currentOpacity += 0.1f;
                    if (currentOpacity >= 1.0f)
                    {
                        currentOpacity = 1.0f;
                        fadeTimer.Stop();
                    }
                }
                else
                {
                    currentOpacity -= 0.1f;
                    if (currentOpacity <= 0.0f)
                    {
                        currentOpacity = 0.0f;
                        fadeTimer.Stop();
                        this.Close();
                    }
                }
                this.Opacity = currentOpacity;
            };

            // Animasyon zamanlayıcısı
            animationTimer = new System.Windows.Forms.Timer { Interval = 500 };
            animationTimer.Tick += (s, e) =>
            {
                animationDots = (animationDots + 1) % 4;
                string dots = new string('.', animationDots);
                thinkingLabel.Text = currentState == FormState.Analyzing
                    ? $"{QSolver.Services.LocalizationService.Get("Result.Analyze")}{dots}"
                    : $"{QSolver.Services.LocalizationService.Get("Result.Solving")}{dots}";
            };

            // Buton hover efektleri
            foreach (Button btn in new[] { confirmButton, editButton, solutionStepsButton })
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
                    if (currentState == FormState.Analyzing || currentState == FormState.Analyzed)
                    {
                        FadeOutAndClose();
                    }
                }
            };

            // Form kapatıldığında animasyonları durdur
            this.FormClosing += (s, e) =>
            {
                animationTimer?.Stop();
                fadeTimer?.Stop();
            };

            // Form yüklendiğinde fade-in efekti başlat
            this.Load += (s, e) =>
            {
                isFadingIn = true;
                fadeTimer.Start();
                animationTimer.Start();
            };
        }

        private void FadeOutAndClose()
        {
            isFadingIn = false;
            fadeTimer.Start();
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

        private void ConfirmButton_Click(object? sender, EventArgs e)
        {
            if (currentState == FormState.Analyzed)
            {
                confirmButton.Text = QSolver.Services.LocalizationService.Get("Common.OK");
                StartSolvingQuestion();
            }
            else if (currentState == FormState.Solved)
            {
                FadeOutAndClose();
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Formu gizlemek yerine opacity'sini düşür
                this.Opacity = 0.1;

                // Düzenleme formunu göster
                using (var editForm = new QuestionEditForm(questionText))
                {
                    if (editForm.ShowDialog() == DialogResult.OK && editForm.IsEdited)
                    {
                        questionText = editForm.EditedQuestionText;
                        resultLabel.Text = QSolver.Services.LocalizationService.Get("Result.Edited");
                    }
                }

                // Formu tekrar görünür yap
                this.Opacity = 1.0;
                this.BringToFront();
            }
            catch (Exception ex)
            {
                this.Opacity = 1.0;
                MessageBox.Show($"{QSolver.Services.LocalizationService.Get("Result.EditError")}: {ex.Message}", QSolver.Services.LocalizationService.Get("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SolutionStepsButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Formu gizlemek yerine opacity'sini düşür
                this.Opacity = 0.1;

                // Çözüm adımları formunu göster
                var stepsForm = new SolutionStepsForm(solutionText);

                // Form kapandığında mevcut formu tekrar göster
                stepsForm.FormClosed += (s, args) =>
                {
                    this.Opacity = 1.0;
                    this.BringToFront();
                };

                stepsForm.Show();
            }
            catch (Exception ex)
            {
                this.Opacity = 1.0;
                MessageBox.Show($"{QSolver.Services.LocalizationService.Get("Result.StepsError")}: {ex.Message}", QSolver.Services.LocalizationService.Get("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    questionText = QSolver.Services.LocalizationService.Get("Result.NotFound");

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
                    questionText = QSolver.Services.LocalizationService.Get("Result.AnalysisError");
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
                        resultLabel.Text = QSolver.Services.LocalizationService.Get("Result.AnalysisError");
                        resultLabel.Visible = true;
                        confirmButton.Visible = true;
                        editButton.Visible = true;

                        currentState = FormState.Analyzed;
                    });
                }
            }
        }

        private async void WaitForDirectSolve(Task<(string fullResponse, string answer, string title, string lecture)> directSolveTask)
        {
            try
            {
                // Turbo modda doğrudan çözme durumunu ayarla
                currentState = FormState.Solving;

                // UI'ı güncelle - "Soru Çözülüyor" göster
                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        thinkingLabel.Text = QSolver.Services.LocalizationService.Get("Result.Solving") + "...";
                        thinkingLabel.Visible = true;
                        resultLabel.Visible = false;
                        confirmButton.Visible = false;
                        editButton.Visible = false;
                        solutionStepsButton.Visible = false;
                        animationTimer.Start();
                    });
                }

                var (fullResponse, answer, title, lecture) = await directSolveTask;

                solutionText = fullResponse;
                answerLetter = answer;
                questionText = title; // Başlığı questionText olarak kullan
                lectureText = lecture; // Dersi kaydet

                // JSON formatında cevap var mı kontrol et
                if (answer == "?" || answer == "Hata")
                {
                    answerLetter = "Yapay zeka gerekli cevabı veremedi";
                }

                // Çözüm tamamlandığında UI'ı güncelle
                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;

                        // Cevap tek harf mi kontrol et
                        string prefix = answerLetter.Length == 1 ? QSolver.Services.LocalizationService.Get("Result.Answer", "") : QSolver.Services.LocalizationService.Get("Result.Answers", "");
                        resultLabel.Text = prefix + answerLetter;

                        resultLabel.Visible = true;
                        confirmButton.Text = QSolver.Services.LocalizationService.Get("Common.OK");
                        confirmButton.Visible = true;
                        solutionStepsButton.Visible = true;

                        currentState = FormState.Solved;

                        // Geçmişe kaydet
                        SaveToHistory();
                    });
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda UI'ı güncelle
                answerLetter = $"Hata oluştu: {ex.Message}";
                solutionText = QSolver.Services.LocalizationService.Get("Result.DirectSolveError");

                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;
                        resultLabel.Text = QSolver.Services.LocalizationService.Get("Result.SolveError");
                        resultLabel.Visible = true;
                        confirmButton.Text = QSolver.Services.LocalizationService.Get("Common.OK");
                        confirmButton.Visible = true;
                        solutionStepsButton.Visible = true;

                        currentState = FormState.Solved;
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
                        thinkingLabel.Text = QSolver.Services.LocalizationService.Get("Result.Solving") + "...";
                        thinkingLabel.Visible = true;
                        resultLabel.Visible = false;
                        confirmButton.Visible = false;
                        editButton.Visible = false;
                        solutionStepsButton.Visible = false;
                        animationTimer.Start();
                    });
                }

                // Soruyu çöz
                var (fullResponse, answer, lecture) = await Program.GetGeminiService().SolveQuestion(questionText);

                // JSON formatında cevap var mı kontrol et
                if (answer == "?" || answer == "Hata")
                {
                    // JSON formatında cevap yoksa tekrar dene
                    if (this.IsHandleCreated)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            thinkingLabel.Text = QSolver.Services.LocalizationService.Get("Result.RetryMessage");
                        });
                    }

                    // Tekrar dene
                    (fullResponse, answer, lecture) = await Program.GetGeminiService().SolveQuestion(questionText);

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
                                resultLabel.Text = QSolver.Services.LocalizationService.Get("Result.Answers", "") + answerLetter;
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
                lectureText = lecture;

                // UI'ı güncelle
                if (this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        animationTimer.Stop();
                        thinkingLabel.Visible = false;

                        // Cevap tek harf mi kontrol et
                        string prefix = answerLetter.Length == 1 ? QSolver.Services.LocalizationService.Get("Result.Answer", "") : QSolver.Services.LocalizationService.Get("Result.Answers", "");
                        resultLabel.Text = prefix + answerLetter;

                        resultLabel.Visible = true;
                        confirmButton.Visible = true;
                        solutionStepsButton.Visible = true;

                        currentState = FormState.Solved;

                        // Geçmişe kaydet
                        SaveToHistory();
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
                        resultLabel.Text = QSolver.Services.LocalizationService.Get("Result.SolveError");
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

            // Gradient arka plan
            using (LinearGradientBrush brush = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(48, 51, 107),
                Color.FromArgb(25, 25, 112),
                45F))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }

            // Form kenarları için gölge efekti
            using (Pen pen = new Pen(Color.FromArgb(40, Color.White), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && currentState != FormState.Solved)
            {
                MessageBox.Show(QSolver.Services.LocalizationService.Get("App.Exit"), QSolver.Services.LocalizationService.Get("Common.Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            base.OnFormClosing(e);
        }

        private async void SaveToHistory()
        {
            try
            {
                if (!string.IsNullOrEmpty(answerLetter) && answerLetter != "?" && answerLetter != "Hata" && screenshotData != null)
                {
                    await SolutionHistoryService.AddSolutionToHistory(
                        questionText,
                        answerLetter,
                        solutionText,
                        screenshotData,
                        isTurboMode,
                        lectureText, // Ders bilgisi
                        questionText  // Başlık olarak questionText kullan
                    );
                }
            }
            catch (Exception ex)
            {
                // Geçmişe kaydetme hatası, sessizce geç
                System.Diagnostics.Debug.WriteLine($"Geçmişe kaydetme hatası: {ex.Message}");
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

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}