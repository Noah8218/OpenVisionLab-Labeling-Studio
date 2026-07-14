using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class DetectionOverlayItem
    {
        public int CandidateIndex { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public RectangleF Bounds { get; set; }
        public IReadOnlyList<PointF> ContourPoints { get; set; } = Array.Empty<PointF>();
        public bool IsContourOnly { get; set; }
        public Color Color { get; set; } = Color.Red;
        public bool IsSelected { get; set; }

        public string Label
        {
            get
            {
                string className = ToCanvasSafeText(string.IsNullOrWhiteSpace(ClassName) ? "Unknown" : ClassName);
                string indexText = CandidateIndex > 0 ? $"{CandidateIndex} " : string.Empty;
                return $"AI {indexText}{className} {Confidence * 100F:0.#}%";
            }
        }

        private static string ToCanvasSafeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Unknown";
            }

            string trimmed = text.Trim();
            return trimmed.All(ch => ch >= 0x20 && ch <= 0x7E) ? trimmed : "Class";
        }
    }
}
