namespace QSolver.Models
{
    /// <summary>
    /// OCR analizi için structured output yanıtı
    /// </summary>
    public class OcrResponse
    {
        public string text { get; set; } = "";
        public bool hasText { get; set; } = false;
    }

    /// <summary>
    /// Soru çözümü için structured output yanıtı
    /// </summary>
    public class SolutionResponse
    {
        public string title { get; set; } = "";
        public string lecture_en { get; set; } = "";
        public string lecture_tr { get; set; } = "";
        public string explanation { get; set; } = "";
        public bool solved { get; set; } = false;
        public string answers { get; set; } = "";
    }

    /// <summary>
    /// Turbo Mode için minimal yanıt - sadece cevap ve başlık
    /// </summary>
    public class TurboResponse
    {
        public string title { get; set; } = "";
        public string lecture_en { get; set; } = "";
        public string lecture_tr { get; set; } = "";
        public bool solved { get; set; } = false;
        public string answers { get; set; } = "";
    }
}
