using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Optional dataset-local source lineage metadata. The sidecar is kept
    /// separate from images and annotations so legacy datasets remain valid
    /// when no source-group information was supplied.
    /// </summary>
    public sealed class YoloDatasetSourceGroupSidecar
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = YoloDatasetSourceGroupService.SchemaVersion;

        [JsonProperty("entries")]
        public List<YoloDatasetSourceGroupEntry> Entries { get; set; } = new List<YoloDatasetSourceGroupEntry>();
    }

    public sealed class YoloDatasetSourceGroupEntry
    {
        [JsonProperty("relativeImagePath")]
        public string RelativeImagePath { get; set; } = string.Empty;

        [JsonProperty("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        [JsonProperty("sourceGroupId")]
        public string SourceGroupId { get; set; } = string.Empty;

        [JsonProperty("parentSourceId")]
        public string ParentSourceId { get; set; } = string.Empty;

        [JsonProperty("transformation")]
        public string Transformation { get; set; } = string.Empty;

        [JsonProperty("captureBundleId")]
        public string CaptureBundleId { get; set; } = string.Empty;
    }

    public sealed class YoloDatasetSourceGroupLoadResult
    {
        internal YoloDatasetSourceGroupLoadResult(
            string sidecarPath,
            bool isAvailable,
            bool isValid,
            YoloDatasetSourceGroupSidecar sidecar,
            IReadOnlyList<string> errors)
        {
            SidecarPath = sidecarPath ?? string.Empty;
            IsAvailable = isAvailable;
            IsValid = isValid;
            Sidecar = sidecar ?? new YoloDatasetSourceGroupSidecar();
            Errors = errors ?? Array.Empty<string>();
        }

        public string SidecarPath { get; }

        public bool IsAvailable { get; }

        public bool IsValid { get; }

        public YoloDatasetSourceGroupSidecar Sidecar { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<YoloDatasetSourceGroupEntry> Entries =>
            Sidecar.Entries ?? (IReadOnlyList<YoloDatasetSourceGroupEntry>)Array.Empty<YoloDatasetSourceGroupEntry>();

        public string Summary => string.Join(Environment.NewLine, Errors);
    }

    public sealed class YoloDatasetSourceGroupResolution
    {
        internal YoloDatasetSourceGroupResolution(
            string relativeImagePath,
            bool isKnown,
            string sourceId,
            string sourceGroupId,
            string originalSourceId,
            string parentSourceId,
            string transformation,
            string captureBundleId,
            IReadOnlyList<string> lineageSourceIds)
        {
            RelativeImagePath = relativeImagePath ?? string.Empty;
            IsKnown = isKnown;
            SourceId = sourceId ?? YoloDatasetSourceGroupService.Unknown;
            SourceGroupId = sourceGroupId ?? YoloDatasetSourceGroupService.Unknown;
            OriginalSourceId = originalSourceId ?? YoloDatasetSourceGroupService.Unknown;
            ParentSourceId = parentSourceId ?? YoloDatasetSourceGroupService.Unknown;
            Transformation = transformation ?? YoloDatasetSourceGroupService.Unknown;
            CaptureBundleId = captureBundleId ?? YoloDatasetSourceGroupService.Unknown;
            LineageSourceIds = lineageSourceIds ?? Array.Empty<string>();
        }

        public string RelativeImagePath { get; }

        public bool IsKnown { get; }

        public string SourceId { get; }

        public string SourceGroupId { get; }

        public string OriginalSourceId { get; }

        public string ParentSourceId { get; }

        public string Transformation { get; }

        public string CaptureBundleId { get; }

        public IReadOnlyList<string> LineageSourceIds { get; }
    }
}
