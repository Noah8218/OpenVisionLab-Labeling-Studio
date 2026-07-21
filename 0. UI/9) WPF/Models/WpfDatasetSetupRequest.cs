using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// UI-independent dataset setup input passed from the wizard ViewModel to
    /// the dataset setup services.
    /// </summary>
    public sealed class WpfDatasetSetupRequest
    {
        public LabelingDatasetPurpose Purpose { get; set; } = LabelingDatasetPurpose.ObjectDetection;

        public string RecipeName { get; set; } = string.Empty;

        public string OutputRootPath { get; set; } = string.Empty;

        public IReadOnlyList<string> ClassNames { get; set; } = Array.Empty<string>();

        public WpfDatasetSamplePresetKind SamplePresetKind { get; set; } = WpfDatasetSamplePresetKind.Empty;

        public string SampleSourcePath { get; set; } = string.Empty;
    }
}
