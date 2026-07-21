using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Linq;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestWpfSegmentationAdapterComparison()
    {
        string root = CreateTempRoot();
        try
        {
            CData data = CreateUnetSegmentationFixture(Path.Combine(root, "recipe"));
            string unetCheckpoint = Path.Combine(root, "unet-best.pt");
            string yoloCheckpoint = Path.Combine(root, "yolo-best.pt");
            string unetRuntimeRoot = Path.Combine(root, "unet-runtime");
            string ultralyticsRuntimeRoot = Path.Combine(root, "yolov8");
            Directory.CreateDirectory(unetRuntimeRoot);
            Directory.CreateDirectory(ultralyticsRuntimeRoot);
            File.WriteAllText(unetCheckpoint, "unet");
            File.WriteAllText(yoloCheckpoint, "yolo");
            data.ProjectSettings.PythonModel = new PythonModelSettings
            {
                ModelEngine = PythonModelSettings.EngineUnet,
                WeightsPath = unetCheckpoint,
                ProjectRootPath = unetRuntimeRoot,
                InferenceImageSize = 320,
                MinimumDetectionConfidence = 0.25F
            };
            data.ProjectSettings.ModelRegistry = new ModelRegistrySettings
            {
                Profiles =
                {
                    new ModelProfile
                    {
                        ProfileId = "yolo-v8-seg",
                        ModelEngine = PythonModelSettings.EngineYoloV8,
                        DatasetPurpose = LabelingDatasetPurpose.Segmentation.ToString()
                    }
                },
                Candidates =
                {
                    new ModelCandidate
                    {
                        CandidateId = "yolo-v8-candidate",
                        ProfileId = "yolo-v8-seg",
                        WeightsPath = yoloCheckpoint,
                        LastSeenUtc = "2026-07-21T00:00:00Z"
                    }
                }
            };

            var service = new WpfSegmentationAdapterComparisonRunService();
            WpfSegmentationAdapterComparisonContext context = service.BuildContext(
                data,
                data.ProjectSettings.ModelRegistry,
                data.ProjectSettings.PythonModel);

            AssertTrue(context.IsVisible, "segmentation recipes should expose the U-Net versus YOLO-seg comparison panel");
            AssertEqual(unetCheckpoint, context.UnetWeightsPath);
            AssertEqual(yoloCheckpoint, context.YoloWeightsPath);
            AssertEqual(PythonModelSettings.EngineYoloV8, context.SelectedYoloEngine);
            AssertTrue(context.CanonicalDatasetText.Contains("canonical export", StringComparison.OrdinalIgnoreCase), "comparison panel should disclose the app-owned common dataset contract");
            AssertTrue(context.DetailText.Contains("YOLO mAP", StringComparison.OrdinalIgnoreCase), "comparison panel should state that raw mask results are not merged with YOLO mAP");

            WpfSegmentationAdapterComparisonRunRequest request = service.BuildRequest(
                data,
                data.ProjectSettings.PythonModel,
                context.UnetWeightsPath,
                context.YoloWeightsPath,
                context.SelectedYoloEngine);
            AssertEqual(PythonModelSettings.EngineUnet, request.UnetSettings.ModelEngine);
            AssertEqual(PythonModelSettings.EngineYoloV8, request.YoloSettings.ModelEngine);
            AssertEqual(unetCheckpoint, request.UnetSettings.WeightsPath);
            AssertEqual(yoloCheckpoint, request.YoloSettings.WeightsPath);
            AssertEqual(unetRuntimeRoot, request.UnetSettings.ProjectRootPath);
            AssertEqual(ultralyticsRuntimeRoot, request.YoloSettings.ProjectRootPath);

            WpfSegmentationAdapterComparisonRunRequest yolo11Request = service.BuildRequest(
                data,
                data.ProjectSettings.PythonModel,
                context.UnetWeightsPath,
                context.YoloWeightsPath,
                PythonModelSettings.EngineYolo11);
            AssertEqual(PythonModelSettings.EngineYolo11, yolo11Request.YoloSettings.ModelEngine);
            AssertEqual(ultralyticsRuntimeRoot, yolo11Request.YoloSettings.ProjectRootPath);

            var viewModel = new WpfTrainingSettingsPanelViewModel();
            viewModel.SetSegmentationAdapterComparisonContext(context, preserveSelectedCheckpoints: false);
            AssertTrue(viewModel.IsSegmentationAdapterComparisonVisible, "comparison panel visibility should be ViewModel-owned");
            AssertTrue(viewModel.IsRunSegmentationAdapterComparisonEnabled, "existing selected checkpoints should enable the separate comparison command before another workflow command starts");
            viewModel.SetSegmentationAdapterComparisonExecutionState(
                isRunning: true,
                statusText: "공통 마스크 비교 실행 중",
                detailText: "fixture",
                actionText: "fixture");
            AssertTrue(!viewModel.IsRunSegmentationAdapterComparisonEnabled, "comparison command should be disabled while the comparison is running");
            viewModel.SetSegmentationAdapterComparisonExecutionState(
                isRunning: false,
                statusText: "비교 완료",
                detailText: "fixture",
                actionText: "자동 교체 안 함");
            AssertTrue(viewModel.IsRunSegmentationAdapterComparisonEnabled, "completion should restore the comparison command without adopting a model");

            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.ObjectDetection;
            WpfSegmentationAdapterComparisonContext nonSegmentation = service.BuildContext(
                data,
                data.ProjectSettings.ModelRegistry,
                data.ProjectSettings.PythonModel);
            AssertTrue(!nonSegmentation.IsVisible, "object-detection recipes must not expose a semantic-mask comparison as if it were mAP comparison");

            string rootPath = FindRepositoryRoot();
            string xaml = File.ReadAllText(Path.Combine(rootPath, "0. UI", "9) WPF", "Views", "WpfTrainingSettingsPanel.xaml"));
            string viewModelSource = File.ReadAllText(Path.Combine(rootPath, "0. UI", "9) WPF", "ViewModels", "WpfTrainingSettingsPanelViewModel.cs"));
            string shellSource = File.ReadAllText(Path.Combine(rootPath, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.SegmentationAdapterComparison.cs"));
            AssertTrue(xaml.Contains("SegmentationAdapterComparisonPanel", StringComparison.Ordinal), "Model Center should render a dedicated segmentation adapter comparison panel");
            AssertTrue(xaml.Contains("SegmentationUnetWeightsPath", StringComparison.Ordinal), "Model Center should bind the selected U-Net checkpoint through the ViewModel");
            AssertTrue(xaml.Contains("SegmentationYoloWeightsPath", StringComparison.Ordinal), "Model Center should bind the selected YOLO-seg checkpoint through the ViewModel");
            AssertTrue(xaml.Contains("RunSegmentationAdapterComparisonCommand", StringComparison.Ordinal), "Model Center should expose a dedicated comparison command");
            AssertTrue(viewModelSource.Contains("IsSegmentationAdapterComparisonVisible", StringComparison.Ordinal), "comparison visibility should remain in the ViewModel boundary");
            AssertTrue(shellSource.Contains("UnetSegmentationDatasetExportService", StringComparison.Ordinal) == false, "shell code-behind should delegate canonical export orchestration to the comparison service");
            AssertTrue(shellSource.Contains("검사 모델 자동 교체", StringComparison.Ordinal), "completion messaging should explicitly exclude automatic inspection-model adoption");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
