using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace QSolver
{
    public class SolutionStepsForm : Form
    {
        private static readonly string IconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "qsolver.ico");

        public SolutionStepsForm(string solutionText)
        {
            this.Text = "Çözüm Adımları";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true; // Formu en üstte tut

            // Form icon
            if (File.Exists(IconPath))
            {
                this.Icon = new Icon(IconPath);
            }

            // JSON kısmını kaldır
            int jsonIndex = solutionText.LastIndexOf("json{");
            if (jsonIndex > 0)
            {
                solutionText = solutionText.Substring(0, jsonIndex);
            }

            // Doğrudan JSON formatındaki cevabı kaldır
            int braceIndex = solutionText.LastIndexOf("{");
            if (braceIndex > 0)
            {
                string potentialJson = solutionText.Substring(braceIndex).Trim();
                if (potentialJson.Contains("\"solved\"") || potentialJson.Contains("\"answers\""))
                {
                    solutionText = solutionText.Substring(0, braceIndex).TrimEnd();
                }
            }

            // Sadece "json" kelimesini kaldır
            int jsonWordIndex = solutionText.LastIndexOf("`json");
            if (jsonWordIndex > 0)
            {
                solutionText = solutionText.Substring(0, jsonWordIndex).TrimEnd();
            }
            else
            {
                jsonWordIndex = solutionText.LastIndexOf("json");
                if (jsonWordIndex > 0 && (jsonWordIndex == 0 || char.IsWhiteSpace(solutionText[jsonWordIndex - 1])))
                {
                    solutionText = solutionText.Substring(0, jsonWordIndex).TrimEnd();
                }
            }

            // Sondaki backtick işaretlerini kaldır
            solutionText = RemoveTrailingBackticks(solutionText);

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

        // Sondaki backtick işaretlerini kaldıran yardımcı metot
        private string RemoveTrailingBackticks(string text)
        {
            // Metni kırp
            text = text.TrimEnd();

            // Sondaki backtick işaretlerini kaldır
            while (text.EndsWith("`"))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            // Sondaki çift backtick işaretlerini kaldır
            if (text.EndsWith("``"))
            {
                text = text.Substring(0, text.Length - 2).TrimEnd();
            }

            return text;
        }

        // Basit bir Markdown to HTML dönüştürücü
        private string ConvertMarkdownToHtml(string markdown)
        {
            // Başlıklar
            markdown = Regex.Replace(markdown, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);

            // Kalın
            markdown = Regex.Replace(markdown, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

            // İtalik
            markdown = Regex.Replace(markdown, @"\*(.+?)\*", "<em>$1</em>");

            // Kod blokları
            markdown = Regex.Replace(markdown, @"```(.+?)```", "<pre><code>$1</code></pre>", RegexOptions.Singleline);

            // Satır içi kod
            markdown = Regex.Replace(markdown, @"`(.+?)`", "<code>$1</code>");

            // Listeler
            markdown = Regex.Replace(markdown, @"^\* (.+)$", "<ul><li>$1</li></ul>", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^(\d+)\. (.+)$", "<ol><li>$2</li></ol>", RegexOptions.Multiline);

            // Paragraflar ve satır sonları
            markdown = Regex.Replace(markdown, @"^\s*$\n", "</p><p>", RegexOptions.Multiline);

            // Çift satır sonları paragraf olarak işle
            markdown = Regex.Replace(markdown, @"\n\n", "</p><p>", RegexOptions.Multiline);

            // Tek satır sonları <br> olarak işle
            markdown = Regex.Replace(markdown, @"\n", "<br>", RegexOptions.Multiline);

            // Tüm metni paragraf içine al
            markdown = "<p>" + markdown + "</p>";

            // Fazladan oluşan paragrafları temizle
            markdown = markdown.Replace("<p></p>", "");

            return markdown;
        }
    }
}