using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeConnectionResult
    {
        public PythonModelRuntimeConnectionResult(
            PythonModelSettings settings,
            PythonModelRuntimeSelfTestReport selfTestReport,
            string summaryText,
            string detailText)
        {
            Settings = settings ?? new PythonModelSettings();
            SelfTestReport = selfTestReport ?? PythonModelRuntimeSelfTestService.BuildReport(Settings);
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
        }

        public PythonModelSettings Settings { get; }
        public PythonModelRuntimeSelfTestReport SelfTestReport { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
    }

    public static class PythonModelRuntimeConnectionService
    {
        public static PythonModelRuntimeConnectionResult BuildYoloV5FolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath)
        {
            string projectRootPath = ResolveYoloV5ProjectRoot(selectedFolderPath);
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EngineYoloV5;
            settings.ProjectRootPath = projectRootPath;
            settings.ClientScriptPath = ResolveClientScriptPath(string.Empty, projectRootPath);
            settings.PythonExecutablePath = ResolveLocalPythonExecutablePath(projectRootPath);

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string summary = report.CanTrain
                ? "\uD559\uC2B5 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778"
                : "YOLOv5 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778 \uD544\uC694";
            string detail = report.CanInspect
                ? "\uD559\uC2B5\uACFC \uD604\uC7AC \uAC80\uC0AC\uAC00 \uAC00\uB2A5\uD569\uB2C8\uB2E4. \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uBC18\uC601\uD558\uC138\uC694."
                : report.CanTrain
                    ? "\uD559\uC2B5\uC740 \uAC00\uB2A5\uD558\uACE0 \uAC80\uC0AC \uBAA8\uB378\uC740 \uC120\uD0DD\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uBC18\uC601\uD558\uC138\uC694."
                    : "\uC810\uAC80 \uBAA9\uB85D\uC758 \uD544\uC694 \uD56D\uBAA9\uC744 \uD655\uC778\uD558\uACE0 \uACBD\uB85C\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static PythonModelRuntimeConnectionResult BuildYoloV8FolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath)
            => BuildYoloV8FolderConnection(
                currentSettings,
                selectedFolderPath,
                LabelingDatasetPurpose.Segmentation);

        public static PythonModelRuntimeConnectionResult BuildYoloV8FolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath,
            LabelingDatasetPurpose datasetPurpose)
        {
            string projectRootPath = selectedFolderPath?.Trim() ?? string.Empty;
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EngineYoloV8;
            settings.ProjectRootPath = projectRootPath;
            settings.ClientScriptPath = ResolveClientScriptPath(string.Empty, projectRootPath);
            settings.PythonExecutablePath = ResolveLocalPythonExecutablePath(projectRootPath);

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string weightPurpose = datasetPurpose switch
            {
                LabelingDatasetPurpose.Segmentation => "segmentation",
                LabelingDatasetPurpose.AnomalyDetection => "classification",
                _ => "object-detection"
            };
            string summary = report.CanInspect
                ? "YOLOv8 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778"
                : "YOLOv8 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778 \uD544\uC694";
            string detail = report.CanInspect
                ? $"\uB85C\uCEEC Ultralytics worker\uB97C \uC5F0\uACB0\uD588\uC2B5\uB2C8\uB2E4. \uD604\uC7AC \uC774\uBBF8\uC9C0\uC640 \uAC80\uC0AC \uBAA8\uB378\uC740 \uC720\uC9C0\uD588\uC73C\uBA70, {weightPurpose} \uD559\uC2B5 \uC2DC\uC791 \uAC00\uC911\uCE58\uB294 \uD559\uC2B5 \uC124\uC815\uC5D0\uC11C \uC790\uB3D9 \uACB0\uC815\uB429\uB2C8\uB2E4."
                : $"Python\uACFC worker \uACBD\uB85C\uB97C \uC810\uAC80\uD558\uACE0, \uD604\uC7AC \uAC80\uC0AC\uC5D0 \uC4F8 \uBAA8\uB378\uC744 \uBA85\uC2DC\uC801\uC73C\uB85C \uC120\uD0DD\uD558\uC138\uC694. {weightPurpose} \uD559\uC2B5 \uC2DC\uC791 \uAC00\uC911\uCE58\uB294 \uD559\uC2B5 \uC124\uC815\uC5D0\uC11C \uC790\uB3D9 \uACB0\uC815\uB429\uB2C8\uB2E4.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static PythonModelRuntimeConnectionResult BuildYolo11FolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath,
            LabelingDatasetPurpose datasetPurpose)
        {
            string projectRootPath = selectedFolderPath?.Trim() ?? string.Empty;
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EngineYolo11;
            settings.ProjectRootPath = projectRootPath;
            settings.ClientScriptPath = PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath();
            settings.PythonExecutablePath = ResolveLocalPythonExecutablePath(projectRootPath);

            string startWeightFileName = datasetPurpose switch
            {
                LabelingDatasetPurpose.Segmentation => "yolo11n-seg.pt",
                LabelingDatasetPurpose.AnomalyDetection => "yolo11n-cls.pt",
                _ => "yolo11n.pt"
            };
            bool hasStartWeight = File.Exists(Path.Combine(projectRootPath, startWeightFileName));
            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string summary = report.Items.Any(item => string.Equals(item.LabelText, "Ultralytics", StringComparison.Ordinal) && item.IsPassed)
                ? "YOLO11 실행기 연결 확인"
                : "YOLO11 실행기 연결 확인 필요";
            string detail = hasStartWeight
                ? $"기존 Ultralytics 폴더와 앱 내 YOLO11 worker를 연결했습니다. 현재 이미지와 검사 모델은 유지하며, {startWeightFileName}은 학습 시작 가중치로만 사용합니다."
                : $"기존 Ultralytics 폴더와 앱 내 YOLO11 worker를 연결했습니다. 학습 전 {startWeightFileName} 시작 가중치가 이 폴더에 있는지 확인하세요. 현재 이미지와 검사 모델은 바꾸지 않습니다.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static PythonModelRuntimeConnectionResult BuildUnetFolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath)
        {
            string projectRootPath = string.IsNullOrWhiteSpace(selectedFolderPath)
                ? PythonModelSettings.GetDefaultUnetProjectRootPath()
                : selectedFolderPath.Trim();
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EngineUnet;
            settings.ProjectRootPath = projectRootPath;
            settings.ClientScriptPath = PythonModelRuntimeBundledWorkerService.ResolveUnetWorkerScriptPath();
            settings.PythonExecutablePath = ResolveLocalPythonExecutablePath(projectRootPath);
            settings.WeightsPath = PythonModelSettings.GetDefaultUnetWeightsPath(projectRootPath);

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string summary = report.CanTrain
                ? "U-Net segmentation runtime ready"
                : "U-Net segmentation runtime needs setup";
            string detail = report.CanTrain
                ? "The app-owned segmentation export is trained from C:\\Git\\unet. Select the produced best.pt only when you want to run inspection."
                : "C:\\Git\\unet needs its Python environment and the bundled U-Net worker. The recipe labels remain unchanged.";
            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static string ResolveKnownLocalRuntimeFolder(
            string currentProjectRootPath,
            string expectedFolderName)
        {
            string currentRoot = currentProjectRootPath?.Trim() ?? string.Empty;
            string expectedName = expectedFolderName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentRoot) || string.IsNullOrWhiteSpace(expectedName))
            {
                return string.Empty;
            }

            string currentLeaf = Path.GetFileName(currentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(currentLeaf, expectedName, StringComparison.OrdinalIgnoreCase)
                && IsLocalRuntimeFolder(currentRoot))
            {
                return currentRoot;
            }

            string parent = Directory.GetParent(currentRoot)?.FullName ?? string.Empty;
            string sibling = string.IsNullOrWhiteSpace(parent) ? string.Empty : Path.Combine(parent, expectedName);
            return IsLocalRuntimeFolder(sibling) ? sibling : string.Empty;
        }

        public static bool TryBuildAnomalyTrainingConnection(
            PythonModelSettings currentSettings,
            out PythonModelRuntimeConnectionResult result)
        {
            result = null;
            string engine = PythonModelSettings.NormalizeModelEngine(currentSettings?.ModelEngine);
            if (string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(engine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal))
            {
                return false;
            }

            string yolo8Root = ResolveKnownLocalRuntimeFolder(currentSettings?.ProjectRootPath, "yolov8");
            if (string.IsNullOrWhiteSpace(yolo8Root))
            {
                return false;
            }

            PythonModelRuntimeConnectionResult candidate = BuildYoloV8FolderConnection(
                currentSettings,
                yolo8Root,
                LabelingDatasetPurpose.AnomalyDetection);
            if (!candidate.SelfTestReport.CanTrain)
            {
                return false;
            }

            result = candidate;
            return true;
        }

        public static PythonModelRuntimeConnectionResult BuildUltralyticsPythonConnection(
            PythonModelSettings currentSettings,
            string engine,
            string selectedPythonOrVenvPath)
        {
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.NormalizeModelEngine(engine);
            if (settings.ModelEngine != PythonModelSettings.EngineYoloV8
                && settings.ModelEngine != PythonModelSettings.EngineYolo11)
            {
                settings.ModelEngine = PythonModelSettings.EngineYolo11;
            }

            settings.PythonExecutablePath = ResolvePythonOrVenvPath(selectedPythonOrVenvPath);
            settings.ProjectRootPath = PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerRootPath();
            settings.ClientScriptPath = PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath();

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            bool hasUltralytics = report.Items.Any(item => string.Equals(item.LabelText, "Ultralytics", StringComparison.Ordinal) && item.IsPassed);
            string summary = hasUltralytics
                ? "Ultralytics \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778"
                : "Ultralytics \uC124\uCE58 \uD544\uC694";
            string detail = hasUltralytics
                ? "\uC120\uD0DD\uD55C Python\uC5D0 ultralytics \uD328\uD0A4\uC9C0\uAC00 \uD655\uC778\uB410\uC2B5\uB2C8\uB2E4. \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uBC18\uC601\uD558\uC138\uC694."
                : "\uC120\uD0DD\uD55C Python\uC5D0 ultralytics \uD328\uD0A4\uC9C0\uAC00 \uBCF4\uC774\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uB2E4\uC74C \uB2E8\uACC4\uC5D0\uC11C \uC124\uCE58 \uB610\uB294 \uB2E4\uB978 venv \uC5F0\uACB0\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        private static PythonModelSettings Clone(PythonModelSettings source)
        {
            source ??= new PythonModelSettings();
            return new PythonModelSettings
            {
                PythonExecutablePath = source.PythonExecutablePath ?? string.Empty,
                ModelEngine = PythonModelSettings.NormalizeModelEngine(source.ModelEngine),
                ProjectRootPath = source.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source.ClientScriptPath ?? string.Empty,
                WeightsPath = source.WeightsPath ?? string.Empty,
                ImageRootPath = source.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source.MinimumDetectionConfidence,
                MaximumDetectionCandidates = source.MaximumDetectionCandidates,
                InferenceImageSize = source.InferenceImageSize,
                DetectionTimeoutSeconds = source.DetectionTimeoutSeconds,
                AutoStartClient = source.AutoStartClient
            };
        }

        private static string ResolveYoloV5ProjectRoot(string selectedFolderPath)
        {
            string selected = selectedFolderPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return selected;
            }

            string leaf = Path.GetFileName(selected.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string parent = Directory.GetParent(selected)?.FullName ?? string.Empty;
            if (string.Equals(leaf, "yolov5Master", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }

            return selected;
        }

        private static string ResolveClientScriptPath(string currentPath, string projectRootPath)
        {
            foreach (string fileName in new[] { "labelling_tcp_client.py", "labeling_tcp_client.py" })
            {
                string candidate = Path.Combine(projectRootPath ?? string.Empty, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string preferred = Path.Combine(projectRootPath ?? string.Empty, "labelling_tcp_client.py");
            return string.IsNullOrWhiteSpace(currentPath) ? preferred : currentPath.Trim();
        }

        private static string ResolveLocalPythonExecutablePath(string projectRootPath)
        {
            foreach (string candidate in EnumerateLocalPythonCandidates(projectRootPath))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> EnumerateLocalPythonCandidates(string projectRootPath)
        {
            foreach (string environmentDirectory in new[] { ".venv-gpu", ".venv", "venv", "env" })
            {
                yield return Path.Combine(projectRootPath ?? string.Empty, environmentDirectory, "Scripts", "python.exe");
            }
        }

        private static bool IsLocalRuntimeFolder(string projectRootPath)
            => !string.IsNullOrWhiteSpace(projectRootPath)
                && Directory.Exists(projectRootPath)
                && EnumerateLocalPythonCandidates(projectRootPath).Any(File.Exists)
                && new[] { "labelling_tcp_client.py", "labeling_tcp_client.py" }
                    .Select(fileName => Path.Combine(projectRootPath, fileName))
                    .Any(File.Exists);

        private static string ResolvePythonOrVenvPath(string selectedPath)
        {
            string trimmed = selectedPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }

            if (Directory.Exists(trimmed))
            {
                string scriptsPython = string.Equals(Path.GetFileName(trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "Scripts", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(trimmed, "python.exe")
                    : Path.Combine(trimmed, "Scripts", "python.exe");
                if (File.Exists(scriptsPython))
                {
                    return scriptsPython;
                }
            }

            return trimmed;
        }

    }
}
