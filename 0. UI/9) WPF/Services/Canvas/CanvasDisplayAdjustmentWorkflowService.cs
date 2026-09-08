using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas display-adjustment values and admission rules without
    /// depending on WPF. The canvas ViewModel projects this state to bindings.
    /// </summary>
    public sealed class CanvasDisplayAdjustmentWorkflowService
    {
        private int brightness;
        private double contrastPercent = 100D;
        private double gamma = 1D;
        private bool isInverted;
        private bool isHistogramEqualized;
        private bool isEnabled;
        private bool isOpen;

        public int Brightness => brightness;

        public double ContrastPercent => contrastPercent;

        public double Gamma => gamma;

        public bool IsInverted => isInverted;

        public bool IsHistogramEqualized => isHistogramEqualized;

        public bool IsEnabled => isEnabled;

        public bool IsOpen => isOpen;

        public bool IsActive
            => brightness != 0
                || Math.Abs(contrastPercent - 100D) >= 0.0001D
                || Math.Abs(gamma - 1D) >= 0.0001D
                || isInverted
                || isHistogramEqualized;

        public bool SetBrightness(int value)
        {
            int normalized = Math.Clamp(value, -100, 100);
            return Set(ref brightness, normalized);
        }

        public bool SetContrastPercent(double value)
        {
            double normalized = Math.Clamp(value, 50D, 200D);
            return Set(ref contrastPercent, normalized);
        }

        public bool SetGamma(double value)
        {
            double normalized = Math.Clamp(value, 0.2D, 3D);
            return Set(ref gamma, normalized);
        }

        public bool SetInverted(bool value)
        {
            return Set(ref isInverted, value);
        }

        public bool SetHistogramEqualized(bool value)
        {
            return Set(ref isHistogramEqualized, value);
        }

        public bool SetEnabled(bool enabled)
        {
            bool changed = Set(ref isEnabled, enabled);
            if (!enabled)
            {
                changed |= Set(ref isOpen, false);
            }

            return changed;
        }

        public bool SetOpen(bool open)
        {
            return Set(ref isOpen, open && isEnabled);
        }

        public ImageDisplayAdjustmentOptions GetOptions()
            => new ImageDisplayAdjustmentOptions
            {
                Brightness = brightness,
                Contrast = contrastPercent / 100D,
                Gamma = gamma,
                Invert = isInverted,
                EqualizeHistogram = isHistogramEqualized
            };

        public void Reset()
        {
            brightness = 0;
            contrastPercent = 100D;
            gamma = 1D;
            isInverted = false;
            isHistogramEqualized = false;
        }

        private static bool Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            return true;
        }
    }
}
