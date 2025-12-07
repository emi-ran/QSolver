using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using QSolver.Helpers;

namespace QSolver.Forms
{
    public class LogViewerForm : Form
    {
        private readonly RichTextBox _logTextBox;
        private readonly Button _refreshButton;
        private readonly Button _clearButton;
        private static readonly string IconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "qsolver.ico");

        public LogViewerForm()
        {
            Text = QSolver.Services.LocalizationService.Get("Logs.Title");
            Size = new Size(800, 600);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = true;
            MaximizeBox = true;
            Padding = new Padding(20);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                RowStyles = {
                    new RowStyle(SizeType.Percent, 90F),
                    new RowStyle(SizeType.Percent, 10F)
                }
            };

            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10F),
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 50,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _clearButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Logs.Clear"),
                Width = 120,
                Height = 35,
                Font = new Font(Font.FontFamily, 10),
                Margin = new Padding(10, 0, 0, 0)
            };

            _refreshButton = new Button
            {
                Text = QSolver.Services.LocalizationService.Get("Logs.Refresh"),
                Width = 120,
                Height = 35,
                Font = new Font(Font.FontFamily, 10),
                Margin = new Padding(10, 0, 0, 0)
            };

            buttonPanel.Controls.Add(_refreshButton);
            buttonPanel.Controls.Add(_clearButton);

            mainPanel.Controls.Add(_logTextBox, 0, 0);
            mainPanel.Controls.Add(buttonPanel, 0, 1);

            Controls.Add(mainPanel);

            _refreshButton.Click += (s, e) => LoadLogs();
            _clearButton.Click += (s, e) => ClearLogs();

            LoadIcon();
            LoadLogs();
        }

        private void LoadIcon()
        {
            try
            {
                if (File.Exists(IconPath))
                {
                    this.Icon = new Icon(IconPath);
                }
                else
                {
                    LogHelper.LogWarning($"İkon dosyası bulunamadı: {IconPath}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError("İkon yüklenirken hata oluştu", ex);
            }
        }

        private void LoadLogs()
        {
            try
            {
                _logTextBox.Clear();
                string logContent = File.ReadAllText(LogHelper.LogFile, Encoding.UTF8);
                ColorizeAndAppendLogs(logContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    QSolver.Services.LocalizationService.Get("Logs.LoadError") + $": {ex.Message}",
                    QSolver.Services.LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void ClearLogs()
        {
            DialogResult result = MessageBox.Show(
                QSolver.Services.LocalizationService.Get("Logs.ClearConfirm"),
                QSolver.Services.LocalizationService.Get("Common.Confirm"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    File.WriteAllText(LogHelper.LogFile, string.Empty, Encoding.UTF8);
                    _logTextBox.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        QSolver.Services.LocalizationService.Get("Logs.ClearError") + $": {ex.Message}",
                        QSolver.Services.LocalizationService.Get("Common.Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        private void ColorizeAndAppendLogs(string logContent)
        {
            _logTextBox.Clear();
            string[] lines = logContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                Color textColor = Color.Black;
                if (line.Contains("[Error]")) textColor = Color.Red;
                else if (line.Contains("[Warning]")) textColor = Color.Orange;
                else if (line.Contains("[Info]")) textColor = Color.Blue;
                else if (line.Contains("[Debug]")) textColor = Color.Gray;

                _logTextBox.SelectionStart = _logTextBox.TextLength;
                _logTextBox.SelectionLength = 0;
                _logTextBox.SelectionColor = textColor;
                _logTextBox.AppendText(line + Environment.NewLine);
            }

            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }
    }
}