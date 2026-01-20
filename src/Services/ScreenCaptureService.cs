using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using QSolver.Helpers;
using QSolver.Forms;

namespace QSolver.Services
{
    /// <summary>
    /// Handles screen capture and region selection for question solving.
    /// </summary>
    public class ScreenCaptureService
    {
        private DoubleBufferedForm? captureForm;
        private Point startPoint;
        private Rectangle selectionRect;
        private bool isSelecting;

        private readonly GeminiService geminiService;
        private readonly Action showApiKeyFormAction;

        // Edit mode variables
        private bool isEditMode;
        private bool isDragging;
        private bool isResizing;
        private Point dragOffset;
        private ResizeHandle activeHandle = ResizeHandle.None;
        private Rectangle solveButtonRect;
        private Rectangle cancelButtonRect;
        private bool solveButtonHovered;
        private bool cancelButtonHovered;
        private Point currentMousePosition;

        private enum ResizeHandle
        {
            None,
            TopLeft, Top, TopRight,
            Left, Right,
            BottomLeft, Bottom, BottomRight,
            Move
        }

        public ScreenCaptureService(GeminiService geminiService, Action showApiKeyFormAction)
        {
            this.geminiService = geminiService;
            this.showApiKeyFormAction = showApiKeyFormAction;
        }

        public void CaptureScreen()
        {
            LogHelper.LogInfo("Ekran yakalama işlemi başlatılıyor");

            if (string.IsNullOrEmpty(ApiKeyManager.GetRandomApiKey()))
            {
                LogHelper.LogWarning("API anahtarı bulunamadı");
                MessageBox.Show(
                    LocalizationService.Get("App.NoApiKey"),
                    LocalizationService.Get("Common.Warning"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                showApiKeyFormAction?.Invoke();
                return;
            }

            ResetSelection();
            CreateCaptureForm();
            SetupMouseEvents();
            SetupKeyboardEvents();
            SetupPaintEvents();
            ShowWithFadeIn();
        }

        private void ResetSelection()
        {
            isSelecting = false;
            isEditMode = false;
            isDragging = false;
            isResizing = false;
            selectionRect = new Rectangle();
            startPoint = Point.Empty;
            activeHandle = ResizeHandle.None;
        }

        private void CreateCaptureForm()
        {
            captureForm = new DoubleBufferedForm
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                BackColor = Color.Black,
                Opacity = 0,
                Cursor = Cursors.Cross,
                TopMost = true,
                ShowInTaskbar = false
            };

            captureForm.FormClosed += (s, e) =>
            {
                ResetSelection();
                captureForm = null;
                LogHelper.LogInfo("Ekran yakalama formu kapatıldı");
            };
        }

        private void SetupMouseEvents()
        {
            if (captureForm == null) return;

            captureForm.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (isEditMode)
                    {
                        HandleEditModeMouseDown(e.Location);
                    }
                    else
                    {
                        startPoint = e.Location;
                        isSelecting = true;
                        selectionRect = new Rectangle();
                        LogHelper.LogDebug($"Seçim başladı: X={e.X}, Y={e.Y}");
                    }
                }
            };

            captureForm.MouseMove += (s, e) =>
            {
                currentMousePosition = e.Location;

                if (isEditMode)
                {
                    HandleEditModeMouseMove(e.Location);
                }
                else if (isSelecting)
                {
                    int x = Math.Min(startPoint.X, e.X);
                    int y = Math.Min(startPoint.Y, e.Y);
                    int width = Math.Abs(startPoint.X - e.X);
                    int height = Math.Abs(startPoint.Y - e.Y);
                    selectionRect = new Rectangle(x, y, width, height);
                }

                captureForm?.Invalidate();
            };

            captureForm.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (isEditMode)
                    {
                        HandleEditModeMouseUp(e.Location);
                    }
                    else if (isSelecting)
                    {
                        isSelecting = false;

                        if (selectionRect.Width > 10 && selectionRect.Height > 10)
                        {
                            bool turboMode = SettingsService.GetTurboMode();
                            if (turboMode)
                            {
                                // Turbo mod: Direk çöz
                                captureForm?.Hide();
                                CaptureRegion();
                            }
                            else
                            {
                                // Normal mod: Edit moda geç
                                isEditMode = true;
                                captureForm?.Invalidate();
                                LogHelper.LogInfo("Düzenleme moduna geçildi");
                            }
                        }
                    }
                }
            };
        }

        private void HandleEditModeMouseDown(Point location)
        {
            // Çöz butonuna tıklandı mı?
            if (solveButtonRect.Contains(location))
            {
                captureForm?.Hide();
                CaptureRegion();
                return;
            }

            // İptal butonuna tıklandı mı?
            if (cancelButtonRect.Contains(location))
            {
                captureForm?.Close();
                return;
            }

            // Handle kontrolü
            activeHandle = GetHandleAtPoint(location);
            if (activeHandle != ResizeHandle.None)
            {
                if (activeHandle == ResizeHandle.Move)
                {
                    isDragging = true;
                    dragOffset = new Point(location.X - selectionRect.X, location.Y - selectionRect.Y);
                }
                else
                {
                    isResizing = true;
                    startPoint = location;
                }
            }
        }

        private void HandleEditModeMouseMove(Point location)
        {
            if (isDragging)
            {
                int newX = location.X - dragOffset.X;
                int newY = location.Y - dragOffset.Y;

                // Ekran sınırları içinde tut
                if (captureForm != null)
                {
                    newX = Math.Max(0, Math.Min(newX, captureForm.Width - selectionRect.Width));
                    newY = Math.Max(0, Math.Min(newY, captureForm.Height - selectionRect.Height));
                }

                selectionRect = new Rectangle(newX, newY, selectionRect.Width, selectionRect.Height);
            }
            else if (isResizing)
            {
                ResizeSelection(location);
            }
            else
            {
                // Cursor güncelle
                UpdateCursor(location);

                // Buton hover durumu
                solveButtonHovered = solveButtonRect.Contains(location);
                cancelButtonHovered = cancelButtonRect.Contains(location);
            }
        }

        private void HandleEditModeMouseUp(Point location)
        {
            isDragging = false;
            isResizing = false;
            activeHandle = ResizeHandle.None;
        }

        private ResizeHandle GetHandleAtPoint(Point p)
        {
            int handleSize = 12;

            // Köşeler
            if (IsNearPoint(p, new Point(selectionRect.Left, selectionRect.Top), handleSize))
                return ResizeHandle.TopLeft;
            if (IsNearPoint(p, new Point(selectionRect.Right, selectionRect.Top), handleSize))
                return ResizeHandle.TopRight;
            if (IsNearPoint(p, new Point(selectionRect.Left, selectionRect.Bottom), handleSize))
                return ResizeHandle.BottomLeft;
            if (IsNearPoint(p, new Point(selectionRect.Right, selectionRect.Bottom), handleSize))
                return ResizeHandle.BottomRight;

            // Kenarlar
            if (IsNearPoint(p, new Point(selectionRect.Left + selectionRect.Width / 2, selectionRect.Top), handleSize))
                return ResizeHandle.Top;
            if (IsNearPoint(p, new Point(selectionRect.Left + selectionRect.Width / 2, selectionRect.Bottom), handleSize))
                return ResizeHandle.Bottom;
            if (IsNearPoint(p, new Point(selectionRect.Left, selectionRect.Top + selectionRect.Height / 2), handleSize))
                return ResizeHandle.Left;
            if (IsNearPoint(p, new Point(selectionRect.Right, selectionRect.Top + selectionRect.Height / 2), handleSize))
                return ResizeHandle.Right;

            // İçeride mi?
            if (selectionRect.Contains(p))
                return ResizeHandle.Move;

            return ResizeHandle.None;
        }

        private bool IsNearPoint(Point p, Point target, int tolerance)
        {
            return Math.Abs(p.X - target.X) <= tolerance && Math.Abs(p.Y - target.Y) <= tolerance;
        }

        private void ResizeSelection(Point location)
        {
            int minSize = 20;
            int left = selectionRect.Left;
            int top = selectionRect.Top;
            int right = selectionRect.Right;
            int bottom = selectionRect.Bottom;

            switch (activeHandle)
            {
                case ResizeHandle.TopLeft:
                    left = Math.Min(location.X, right - minSize);
                    top = Math.Min(location.Y, bottom - minSize);
                    break;
                case ResizeHandle.Top:
                    top = Math.Min(location.Y, bottom - minSize);
                    break;
                case ResizeHandle.TopRight:
                    right = Math.Max(location.X, left + minSize);
                    top = Math.Min(location.Y, bottom - minSize);
                    break;
                case ResizeHandle.Left:
                    left = Math.Min(location.X, right - minSize);
                    break;
                case ResizeHandle.Right:
                    right = Math.Max(location.X, left + minSize);
                    break;
                case ResizeHandle.BottomLeft:
                    left = Math.Min(location.X, right - minSize);
                    bottom = Math.Max(location.Y, top + minSize);
                    break;
                case ResizeHandle.Bottom:
                    bottom = Math.Max(location.Y, top + minSize);
                    break;
                case ResizeHandle.BottomRight:
                    right = Math.Max(location.X, left + minSize);
                    bottom = Math.Max(location.Y, top + minSize);
                    break;
            }

            selectionRect = new Rectangle(left, top, right - left, bottom - top);
        }

        private void UpdateCursor(Point location)
        {
            if (captureForm == null) return;

            var handle = GetHandleAtPoint(location);
            captureForm.Cursor = handle switch
            {
                ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
                ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                ResizeHandle.Move => Cursors.SizeAll,
                _ => solveButtonRect.Contains(location) || cancelButtonRect.Contains(location) ? Cursors.Hand : Cursors.Cross
            };
        }

        private void SetupKeyboardEvents()
        {
            if (captureForm == null) return;

            captureForm.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    LogHelper.LogInfo("Ekran yakalama işlemi kullanıcı tarafından iptal edildi");
                    captureForm?.Close();
                }
                else if (e.KeyCode == Keys.Enter && isEditMode)
                {
                    captureForm?.Hide();
                    CaptureRegion();
                }
            };
        }

        private void SetupPaintEvents()
        {
            if (captureForm == null) return;

            captureForm.Paint += (s, ev) =>
            {
                var g = ev.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                if (isEditMode)
                {
                    DrawEditModeSelection(g);
                    DrawEditModeButtons(g);
                }
                else
                {
                    if (!isSelecting)
                        DrawCrosshair(g);

                    if ((isSelecting || isEditMode) && selectionRect.Width > 0 && selectionRect.Height > 0)
                    {
                        DrawSelection(g);
                        DrawDimensions(g);
                    }
                }
            };
        }

        private void DrawEditModeSelection(Graphics g)
        {
            // Seçim alanı dışını karart
            using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
            {
                if (captureForm != null)
                {
                    // Üst
                    g.FillRectangle(darkBrush, 0, 0, captureForm.Width, selectionRect.Top);
                    // Alt
                    g.FillRectangle(darkBrush, 0, selectionRect.Bottom, captureForm.Width, captureForm.Height - selectionRect.Bottom);
                    // Sol
                    g.FillRectangle(darkBrush, 0, selectionRect.Top, selectionRect.Left, selectionRect.Height);
                    // Sağ
                    g.FillRectangle(darkBrush, selectionRect.Right, selectionRect.Top, captureForm.Width - selectionRect.Right, selectionRect.Height);
                }
            }

            // Seçim çerçevesi
            DrawSelection(g);
            DrawDimensions(g);
        }

        private void DrawEditModeButtons(Graphics g)
        {
            if (captureForm == null) return;

            int buttonWidth = 100;
            int buttonHeight = 36;
            int buttonSpacing = 10;
            int totalWidth = buttonWidth * 2 + buttonSpacing;

            // Buton pozisyonlarını hesapla
            int buttonsX = selectionRect.X + (selectionRect.Width - totalWidth) / 2;
            int buttonsY = selectionRect.Bottom + 15;

            // Ekran sınırları kontrolü
            if (buttonsY + buttonHeight + 10 > captureForm.Height)
            {
                buttonsY = selectionRect.Top - buttonHeight - 15;
            }
            if (buttonsY < 10)
            {
                buttonsY = selectionRect.Top + 10;
            }
            if (buttonsX < 10)
            {
                buttonsX = 10;
            }
            if (buttonsX + totalWidth > captureForm.Width - 10)
            {
                buttonsX = captureForm.Width - totalWidth - 10;
            }

            // Çöz butonu
            solveButtonRect = new Rectangle(buttonsX, buttonsY, buttonWidth, buttonHeight);
            DrawButton(g, solveButtonRect, LocalizationService.Get("Result.SolveButton"),
                solveButtonHovered ? Color.FromArgb(255, 46, 180, 100) : Color.FromArgb(255, 46, 213, 115), true);

            // İptal butonu
            cancelButtonRect = new Rectangle(buttonsX + buttonWidth + buttonSpacing, buttonsY, buttonWidth, buttonHeight);
            DrawButton(g, cancelButtonRect, LocalizationService.Get("Common.Cancel"),
                cancelButtonHovered ? Color.FromArgb(255, 220, 130, 50) : Color.FromArgb(255, 255, 159, 67), false);
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, Color bgColor, bool isPrimary)
        {
            // Arka plan
            using (var path = CreateRoundedRectPath(rect, 8))
            {
                using (SolidBrush bgBrush = new SolidBrush(bgColor))
                {
                    g.FillPath(bgBrush, path);
                }

                // Çerçeve
                using (Pen borderPen = new Pen(Color.FromArgb(100, 255, 255, 255), 1))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Metin
            using (Font font = new Font("Segoe UI", 11, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                var textSize = g.MeasureString(text, font);
                float textX = rect.X + (rect.Width - textSize.Width) / 2;
                float textY = rect.Y + (rect.Height - textSize.Height) / 2;
                g.DrawString(text, font, textBrush, textX, textY);
            }
        }

        private void DrawCrosshair(Graphics g)
        {
            if (captureForm == null) return;

            int crosshairSize = 20;
            int centerX = currentMousePosition.X;
            int centerY = currentMousePosition.Y;

            using (Pen darkPen = new Pen(Color.FromArgb(200, 0, 0, 0), 3))
            {
                g.DrawLine(darkPen, centerX - crosshairSize, centerY, centerX + crosshairSize, centerY);
                g.DrawLine(darkPen, centerX, centerY - crosshairSize, centerX, centerY + crosshairSize);
            }

            using (Pen whitePen = new Pen(Color.White, 1))
            {
                g.DrawLine(whitePen, centerX - crosshairSize, centerY, centerX + crosshairSize, centerY);
                g.DrawLine(whitePen, centerX, centerY - crosshairSize, centerX, centerY + crosshairSize);
            }

            using (SolidBrush centerBrush = new SolidBrush(Color.FromArgb(255, 64, 156, 255)))
            {
                g.FillEllipse(centerBrush, centerX - 3, centerY - 3, 6, 6);
            }
        }

        private void DrawSelection(Graphics g)
        {
            // Seçim alanı içini hafif aydınlat
            using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
            {
                g.FillRectangle(highlightBrush, selectionRect);
            }

            // Dış çerçeve (koyu)
            using (Pen darkPen = new Pen(Color.FromArgb(200, 0, 0, 0), 4))
            {
                g.DrawRectangle(darkPen, selectionRect);
            }

            // Orta çerçeve (mavi)
            using (Pen bluePen = new Pen(Color.FromArgb(255, 64, 156, 255), 2))
            {
                g.DrawRectangle(bluePen, selectionRect);
            }

            // İç çerçeve (beyaz)
            using (Pen whitePen = new Pen(Color.FromArgb(150, 255, 255, 255), 1))
            {
                var innerRect = new Rectangle(
                    selectionRect.X + 2,
                    selectionRect.Y + 2,
                    selectionRect.Width - 4,
                    selectionRect.Height - 4);
                if (innerRect.Width > 0 && innerRect.Height > 0)
                {
                    g.DrawRectangle(whitePen, innerRect);
                }
            }

            DrawCornerHandles(g);
        }

        private void DrawCornerHandles(Graphics g)
        {
            int handleSize = isEditMode ? 10 : 8;
            Color handleColor = Color.FromArgb(255, 64, 156, 255);

            // 8 handle: 4 köşe + 4 kenar ortası
            var handles = new (Point point, ResizeHandle handle)[]
            {
                (new Point(selectionRect.Left, selectionRect.Top), ResizeHandle.TopLeft),
                (new Point(selectionRect.Right, selectionRect.Top), ResizeHandle.TopRight),
                (new Point(selectionRect.Left, selectionRect.Bottom), ResizeHandle.BottomLeft),
                (new Point(selectionRect.Right, selectionRect.Bottom), ResizeHandle.BottomRight),
                (new Point(selectionRect.Left + selectionRect.Width / 2, selectionRect.Top), ResizeHandle.Top),
                (new Point(selectionRect.Left + selectionRect.Width / 2, selectionRect.Bottom), ResizeHandle.Bottom),
                (new Point(selectionRect.Left, selectionRect.Top + selectionRect.Height / 2), ResizeHandle.Left),
                (new Point(selectionRect.Right, selectionRect.Top + selectionRect.Height / 2), ResizeHandle.Right),
            };

            using (SolidBrush handleBrush = new SolidBrush(handleColor))
            using (Pen handlePen = new Pen(Color.White, 1))
            {
                foreach (var (point, _) in handles)
                {
                    var handleRect = new Rectangle(
                        point.X - handleSize / 2,
                        point.Y - handleSize / 2,
                        handleSize,
                        handleSize);
                    g.FillRectangle(handleBrush, handleRect);
                    g.DrawRectangle(handlePen, handleRect);
                }
            }
        }

        private void DrawDimensions(Graphics g)
        {
            string dimensions = $"{selectionRect.Width} x {selectionRect.Height}";
            using (Font font = new Font("Segoe UI", 11, FontStyle.Bold))
            {
                SizeF textSize = g.MeasureString(dimensions, font);

                int textX = selectionRect.X + (selectionRect.Width - (int)textSize.Width) / 2;
                int textY = selectionRect.Top - (int)textSize.Height - 15;

                if (textY < 10)
                {
                    textY = selectionRect.Bottom + 50; // Butonların altına
                }

                if (captureForm != null)
                {
                    if (textX < 10) textX = 10;
                    if (textX + textSize.Width > captureForm.Width - 10)
                    {
                        textX = captureForm.Width - (int)textSize.Width - 10;
                    }
                }

                using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                {
                    var bgRect = new Rectangle(
                        textX - 8, textY - 4,
                        (int)textSize.Width + 16, (int)textSize.Height + 8);

                    using (var path = CreateRoundedRectPath(bgRect, 6))
                    {
                        g.FillPath(backBrush, path);
                    }
                }

                using (Pen borderPen = new Pen(Color.FromArgb(255, 64, 156, 255), 1))
                {
                    var bgRect = new Rectangle(
                        textX - 8, textY - 4,
                        (int)textSize.Width + 16, (int)textSize.Height + 8);
                    using (var path = CreateRoundedRectPath(bgRect, 6))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }

                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString(dimensions, font, textBrush, textX, textY);
                }
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private void ShowWithFadeIn()
        {
            if (captureForm == null) return;

            var fadeTimer = new System.Windows.Forms.Timer { Interval = 1 };
            double opacity = 0;

            fadeTimer.Tick += (s, e) =>
            {
                opacity += 0.04;
                if (opacity >= 0.5)
                {
                    opacity = 0.5;
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                }
                if (captureForm != null && !captureForm.IsDisposed)
                {
                    captureForm.Opacity = opacity;
                }
            };

            captureForm.FormClosed += (s, e) =>
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();
            };

            captureForm.Show();
            fadeTimer.Start();
            LogHelper.LogInfo("Ekran yakalama formu gösterildi");
        }

        private void CaptureRegion()
        {
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0) return;

            LogHelper.LogInfo($"Bölge yakalanıyor: Genişlik={selectionRect.Width}, Yükseklik={selectionRect.Height}");

            using (Bitmap bitmap = new Bitmap(selectionRect.Width, selectionRect.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(selectionRect.Location, Point.Empty, selectionRect.Size);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    byte[] imageBytes = ms.ToArray();
                    string base64Image = Convert.ToBase64String(imageBytes);
                    byte[] screenshotData = imageBytes;

                    try
                    {
                        Point resultLocation = CalculateResultFormLocation();
                        bool turboMode = SettingsService.GetTurboMode();

                        if (turboMode)
                        {
                            LogHelper.LogInfo("Turbo mod aktif - Soru doğrudan çözülüyor...");
                            var directSolveTask = geminiService.SolveQuestionDirectly(base64Image);
                            var resultForm = new ResultForm(resultLocation, directSolveTask, screenshotData, true);
                            resultForm.Show();
                            LogHelper.LogInfo("Sonuç formu (turbo mod) gösterildi");
                        }
                        else
                        {
                            LogHelper.LogInfo("Görsel analiz ediliyor...");
                            var analysisTask = geminiService.AnalyzeImage(base64Image);
                            var resultForm = new ResultForm(resultLocation, analysisTask, screenshotData, false);
                            resultForm.Show();
                            LogHelper.LogInfo("Sonuç formu gösterildi");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError("API isteği sırasında hata oluştu", ex);
                        MessageBox.Show(
                            $"{LocalizationService.Get("Result.ApiRequestError")}: {ex.Message}",
                            LocalizationService.Get("Common.Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private Point CalculateResultFormLocation()
        {
            Control? controlForScreen = captureForm ?? Form.ActiveForm;
            if (controlForScreen == null && Application.OpenForms.Count > 0)
            {
                controlForScreen = Application.OpenForms[0];
            }

            Screen currentScreen = controlForScreen != null
                ? Screen.FromControl(controlForScreen)
                : Screen.PrimaryScreen ?? Screen.AllScreens[0];

            Rectangle screenBounds = currentScreen.WorkingArea;

            int x = screenBounds.Right - 250;
            if (selectionRect.Right + 250 <= screenBounds.Right)
            {
                x = selectionRect.Right - 125;
            }

            int y = screenBounds.Height / 2 - 60;

            return new Point(x, y);
        }
    }
}
