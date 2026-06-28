using System.Drawing;

namespace MvcVisionSystem
{
    public sealed class DetectionOverlayItem
    {
        public int CandidateIndex { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public RectangleF Bounds { get; set; }
        public Color Color { get; set; } = Color.Red;
        public bool IsSelected { get; set; }

        public string Label
        {
            get
            {
                string className = string.IsNullOrWhiteSpace(ClassName) ? "Unknown" : ClassName;
                string indexText = CandidateIndex > 0 ? $"#{CandidateIndex} " : string.Empty;
                return $"\uD6C4\uBCF4 {indexText}{className} {Confidence * 100F:0}%";
            }
        }
    }
}
