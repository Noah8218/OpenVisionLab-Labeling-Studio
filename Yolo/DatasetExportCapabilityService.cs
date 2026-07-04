using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class DatasetExportCapability
    {
        public string FormatKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Direction { get; set; } = string.Empty;

        public string DatasetPurpose { get; set; } = string.Empty;

        public bool IsImplemented { get; set; }

        public bool IsRecommendedNext { get; set; }

        public string RequirementSummary { get; set; } = string.Empty;

        public string VerificationSwitch { get; set; } = string.Empty;
    }

    public static class DatasetExportCapabilityService
    {
        public static IReadOnlyList<DatasetExportCapability> BuildCapabilities()
            => new List<DatasetExportCapability>
            {
                new DatasetExportCapability
                {
                    FormatKey = "yolo-detection-directory",
                    DisplayName = "YOLO detection directory",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Existing data/<split>/images and labels folder output used by the training flow.",
                    VerificationSwitch = "--dataset-readiness-purpose"
                },
                new DatasetExportCapability
                {
                    FormatKey = "coco-detection-json",
                    DisplayName = "COCO detection JSON",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "External JSON export for images, categories, boxes, empty-label images, and skipped invalid YOLO lines.",
                    VerificationSwitch = "--coco-detection-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "pascal-voc-detection",
                    DisplayName = "Pascal VOC XML",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "One XML file per image with image size, class name, empty-label images, and box coordinates.",
                    VerificationSwitch = "--pascal-voc-detection-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "label-studio-detection-json",
                    DisplayName = "Label Studio detection JSON",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Raw task JSON with image data and RectangleLabels annotations in Label Studio percent units.",
                    VerificationSwitch = "--label-studio-detection-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "cvat-images-archive",
                    DisplayName = "CVAT image task archive",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "CVAT for images 1.1 zip archive with annotations.xml and image files for bounding-box tasks.",
                    VerificationSwitch = "--cvat-image-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "coco-segmentation-json",
                    DisplayName = "COCO segmentation JSON",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "External JSON export for images, categories, polygon segment annotations, empty-label images, and skipped invalid segment records.",
                    VerificationSwitch = "--coco-segmentation-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "label-studio-segmentation-json",
                    DisplayName = "Label Studio segmentation JSON",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Raw task JSON with image data and PolygonLabels annotations in Label Studio percent units.",
                    VerificationSwitch = "--label-studio-segmentation-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "cvat-segmentation-archive",
                    DisplayName = "CVAT segmentation archive",
                    Direction = "export",
                    DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "CVAT for images 1.1 zip archive with annotations.xml polygon entries and image files for segmentation tasks.",
                    VerificationSwitch = "--cvat-segmentation-export"
                },
                new DatasetExportCapability
                {
                    FormatKey = "coco-detection-import",
                    DisplayName = "COCO detection import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports COCO detection images, categories, and boxes into the local YOLO dataset layout.",
                    VerificationSwitch = "--coco-detection-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "pascal-voc-detection-import",
                    DisplayName = "Pascal VOC detection import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports Pascal VOC XML annotations and source images into the local YOLO dataset layout.",
                    VerificationSwitch = "--pascal-voc-detection-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "label-studio-detection-import",
                    DisplayName = "Label Studio detection import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports Label Studio RectangleLabels task JSON into the local YOLO dataset layout.",
                    VerificationSwitch = "--label-studio-detection-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "cvat-detection-import",
                    DisplayName = "CVAT detection import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports CVAT image-task box annotations and archive images into the local YOLO dataset layout.",
                    VerificationSwitch = "--cvat-detection-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "coco-segmentation-import",
                    DisplayName = "COCO segmentation import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports COCO polygon segmentation annotations and source images into the local segmentation dataset layout.",
                    VerificationSwitch = "--coco-segmentation-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "label-studio-segmentation-import",
                    DisplayName = "Label Studio segmentation import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports Label Studio PolygonLabels task JSON into the local segmentation dataset layout.",
                    VerificationSwitch = "--label-studio-segmentation-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "cvat-segmentation-import",
                    DisplayName = "CVAT segmentation import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString(),
                    IsImplemented = true,
                    RequirementSummary = "Imports CVAT polygon annotations and archive images into the local segmentation dataset layout.",
                    VerificationSwitch = "--cvat-segmentation-import"
                },
                new DatasetExportCapability
                {
                    FormatKey = "labelbox-ndjson-detection-import",
                    DisplayName = "Labelbox NDJSON detection import",
                    Direction = "import",
                    DatasetPurpose = LabelingDatasetPurpose.ObjectDetection.ToString(),
                    IsImplemented = false,
                    IsRecommendedNext = true,
                    RequirementSummary = "Next interoperability target after CVAT segmentation import: import NDJSON detection annotations into local YOLO labels.",
                    VerificationSwitch = "--labelbox-ndjson-detection-import"
                }
            };

        public static DatasetExportCapability GetRecommendedNext()
            => BuildCapabilities().FirstOrDefault(item => item.IsRecommendedNext);

        public static IReadOnlyList<DatasetExportCapability> BuildImplementedCapabilities()
            => BuildCapabilities()
                .Where(item => item.IsImplemented)
                .ToList();
    }
}
