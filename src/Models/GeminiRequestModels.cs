using System.Text.Json.Serialization;

namespace QSolver.Models
{
    /// <summary>
    /// Gemini API isteği için ana model
    /// </summary>
    public class GeminiRequest
    {
        public required Content[] contents { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GenerationConfig? generationConfig { get; set; }
    }

    /// <summary>
    /// Gemini generation konfigürasyonu
    /// </summary>
    public class GenerationConfig
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? responseMimeType { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? responseSchema { get; set; }
    }

    /// <summary>
    /// İçerik wrapper
    /// </summary>
    public class Content
    {
        public required Part[] parts { get; set; }
    }

    /// <summary>
    /// İçerik parçası - metin veya görsel
    /// </summary>
    public class Part
    {
        public string? text { get; set; }
        public InlineData? inline_data { get; set; }
    }

    /// <summary>
    /// Base64 görsel verisi
    /// </summary>
    public class InlineData
    {
        public required string mime_type { get; set; }
        public required string data { get; set; }
    }
}
