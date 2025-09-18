using System;
using System.Drawing;
using System.Windows.Forms;

namespace QSolver.Forms
{
    public partial class ImageViewerForm : Form
    {
        private PictureBox imagePictureBox = null!;
        private Button closeButton = null!;
        private Button zoomInButton = null!;
        private Button zoomOutButton = null!;
        private Button resetZoomButton = null!;
        private Label zoomLabel = null!;
        private Panel imagePanel = null!;

        private Image originalImage;
        private float zoomFactor = 1.0f;
        private Point mouseDownPoint;
        private bool isDragging = false;

        public ImageViewerForm(Image image)
        {
            this.originalImage = image;
            InitializeComponent();
            DisplayImage();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "QSolver - Görsel Görüntüleyici";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.KeyPreview = true;
            this.WindowState = FormWindowState.Maximized;

            // Image panel (scrollable container)
            imagePanel = new Panel
            {
                Location = new Point(0, 50),
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 50),
                BackColor = Color.FromArgb(55, 55, 58),
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Picture box
            imagePictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(55, 55, 58),
                Cursor = Cursors.Hand
            };

            // Mouse events for dragging
            imagePictureBox.MouseDown += ImagePictureBox_MouseDown;
            imagePictureBox.MouseMove += ImagePictureBox_MouseMove;
            imagePictureBox.MouseUp += ImagePictureBox_MouseUp;
            imagePictureBox.MouseWheel += ImagePictureBox_MouseWheel;

            // Toolbar panel
            var toolbarPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, 50),
                BackColor = Color.FromArgb(35, 35, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Zoom in button
            zoomInButton = new Button
            {
                Text = "🔍+",
                Location = new Point(10, 10),
                Size = new Size(50, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            zoomInButton.FlatAppearance.BorderSize = 0;
            zoomInButton.Click += ZoomInButton_Click;

            // Zoom out button
            zoomOutButton = new Button
            {
                Text = "🔍-",
                Location = new Point(70, 10),
                Size = new Size(50, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            zoomOutButton.FlatAppearance.BorderSize = 0;
            zoomOutButton.Click += ZoomOutButton_Click;

            // Reset zoom button
            resetZoomButton = new Button
            {
                Text = "1:1",
                Location = new Point(130, 10),
                Size = new Size(50, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            resetZoomButton.FlatAppearance.BorderSize = 0;
            resetZoomButton.Click += ResetZoomButton_Click;

            // Zoom label
            zoomLabel = new Label
            {
                Text = "100%",
                Location = new Point(190, 15),
                Size = new Size(60, 20),
                ForeColor = Color.FromArgb(241, 241, 241),
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Close button
            closeButton = new Button
            {
                Text = "❌ Kapat",
                Location = new Point(this.ClientSize.Width - 120, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += CloseButton_Click;

            // Add controls
            toolbarPanel.Controls.Add(zoomInButton);
            toolbarPanel.Controls.Add(zoomOutButton);
            toolbarPanel.Controls.Add(resetZoomButton);
            toolbarPanel.Controls.Add(zoomLabel);
            toolbarPanel.Controls.Add(closeButton);

            imagePanel.Controls.Add(imagePictureBox);

            this.Controls.Add(toolbarPanel);
            this.Controls.Add(imagePanel);

            // Keyboard shortcuts
            this.KeyDown += ImageViewerForm_KeyDown;

            // Resize event
            this.Resize += ImageViewerForm_Resize;

            this.ResumeLayout(false);
        }

        private void DisplayImage()
        {
            if (originalImage == null) return;

            imagePictureBox.Image = originalImage;
            UpdateImageSize();
        }

        private void UpdateImageSize()
        {
            if (originalImage == null) return;

            int newWidth = (int)(originalImage.Width * zoomFactor);
            int newHeight = (int)(originalImage.Height * zoomFactor);

            imagePictureBox.Size = new Size(newWidth, newHeight);
            imagePictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            // Center the image if it's smaller than the panel
            if (newWidth < imagePanel.ClientSize.Width)
            {
                imagePictureBox.Location = new Point((imagePanel.ClientSize.Width - newWidth) / 2, imagePictureBox.Location.Y);
            }
            if (newHeight < imagePanel.ClientSize.Height)
            {
                imagePictureBox.Location = new Point(imagePictureBox.Location.X, (imagePanel.ClientSize.Height - newHeight) / 2);
            }

            zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
        }

        private void ZoomInButton_Click(object? sender, EventArgs e)
        {
            ZoomIn();
        }

        private void ZoomOutButton_Click(object? sender, EventArgs e)
        {
            ZoomOut();
        }

        private void ResetZoomButton_Click(object? sender, EventArgs e)
        {
            ResetZoom();
        }

        private void CloseButton_Click(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void ZoomIn()
        {
            if (zoomFactor < 5.0f)
            {
                zoomFactor *= 1.25f;
                UpdateImageSize();
            }
        }

        private void ZoomOut()
        {
            if (zoomFactor > 0.1f)
            {
                zoomFactor /= 1.25f;
                UpdateImageSize();
            }
        }

        private void ResetZoom()
        {
            zoomFactor = 1.0f;
            UpdateImageSize();
            imagePictureBox.Location = new Point(0, 0);
        }

        private void ImagePictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                mouseDownPoint = e.Location;
                imagePictureBox.Cursor = Cursors.SizeAll;
            }
        }

        private void ImagePictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point newLocation = new Point(
                    imagePictureBox.Location.X + (e.X - mouseDownPoint.X),
                    imagePictureBox.Location.Y + (e.Y - mouseDownPoint.Y)
                );
                imagePictureBox.Location = newLocation;
            }
        }

        private void ImagePictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            isDragging = false;
            imagePictureBox.Cursor = Cursors.Hand;
        }

        private void ImagePictureBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                ZoomIn();
            }
            else
            {
                ZoomOut();
            }
        }

        private void ImageViewerForm_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
                case Keys.Add:
                case Keys.Oemplus:
                    ZoomIn();
                    break;
                case Keys.Subtract:
                case Keys.OemMinus:
                    ZoomOut();
                    break;
                case Keys.NumPad0:
                case Keys.D0:
                    ResetZoom();
                    break;
            }
        }

        private void ImageViewerForm_Resize(object? sender, EventArgs e)
        {
            if (imagePanel != null)
            {
                imagePanel.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 50);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Original image'ı dispose etme, başka yerde kullanılıyor olabilir
                imagePictureBox.Image = null;
            }
            base.Dispose(disposing);
        }
    }
}
