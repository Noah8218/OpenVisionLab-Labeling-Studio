using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
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
                ? "학습 실행 환경 연결 확인"
                : "YOLOv5 실행 환경 연결 확인 필요";
            string detail = report.CanInspect
                ? "추론이 가능합니다. 저장을 눌러 레시피에 반영하세요."
                : report.CanTrain
                    ? "학습이 가능합니다. 검사 모델을 선택한 뒤 저장을 눌러 레시피에 반영하세요."
                    : "점검 목록의 필요 항목을 확인하고 경로를 다시 선택하세요.";

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
                ? "YOLOv8 실행 환경 연결 확인"
                : "YOLOv8 실행 환경 연결 확인 필요";
            string detail = report.CanInspect
                ? $"로컬 Ultralytics 모델 통신 프로세스를 연결했습니다. 현재 이미지와 검사 모델은 유지했으며, {weightPurpose} 학습 시작 가중치는 학습 설정에서 자동 결정됩니다."
                : $"Python과 통신 스크립트 경로를 확인하고, 추론에 쓸 모델을 직접 선택하세요. {weightPurpose} 시작 가중치는 학습 설정에서 자동 선택합니다.";

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
                ? "YOLO11 실행 환경 연결 확인"
                : "YOLO11 실행 환경 연결 확인 필요";
            string detail = hasStartWeight
                ? $"기존 Ultralytics 폴더와 앱 내 YOLO11 모델 통신 프로세스를 연결했습니다. 현재 이미지와 검사 모델은 유지하며, {startWeightFileName}은 학습 시작 가중치로만 사용합니다."
                : $"기존 Ultralytics 폴더와 앱 내 YOLO11 모델 통신 프로세스를 연결했습니다. 학습 전 {startWeightFileName} 시작 가중치가 이 폴더에 있는지 확인하세요. 현재 이미지와 검사 모델은 바꾸지 않습니다.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static PythonModelRuntimeConnectionResult BuildUnetFolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath)
        {
            string projectRootPath = string.IsNullOrWhiteSpace(selectedFolderPath)
                ? PythonModelRuntimePathResolver.GetDefaultUnetProjectRootPath()
                : selectedFolderPath.Trim();
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EngineUnet;
            settings.ProjectRootPath = projectRootPath;
            settings.ClientScriptPath = PythonModelRuntimeBundledWorkerService.ResolveUnetWorkerScriptPath();
            settings.PythonExecutablePath = ResolveLocalPythonExecutablePath(projectRootPath);
            settings.WeightsPath = PythonModelRuntimePathResolver.GetDefaultUnetWeightsPath(projectRootPath);

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string summary = report.CanTrain
                ? "U-Net 세그멘테이션 실행 환경 준비 완료"
                : "U-Net 세그멘테이션 실행 환경 설정 필요";
            string detail = report.CanTrain
                ? "앱에서 내보낸 세그멘테이션 데이터로 학습합니다. 추론을 실행할 때만 학습 결과 best.pt를 선택하세요."
                : "U-Net을 실행할 Python 환경과 앱에 포함된 통신 스크립트를 확인하세요. 레시피 라벨은 유지됩니다.";
            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static PythonModelRuntimeConnectionResult BuildPatchCoreConnection(
            PythonModelSettings currentSettings,
            string pythonExecutablePath = "")
        {
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EnginePatchCore;
            settings.ProjectRootPath = PythonModelRuntimePathResolver.GetDefaultPatchCoreProjectRootPath();
            settings.ClientScriptPath = PythonModelRuntimeBundledWorkerService.ResolvePatchCoreWorkerScriptPath();
            settings.PythonExecutablePath = string.IsNullOrWhiteSpace(pythonExecutablePath)
                ? PythonModelSettingsValidator.ResolvePythonExecutable(currentSettings)
                : ResolvePythonOrVenvPath(pythonExecutablePath);
            settings.WeightsPath = PythonModelRuntimePathResolver.GetDefaultPatchCoreWeightsPath(settings.ProjectRootPath);

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string summary = report.CanTrain
                ? "PatchCore 정상 이미지 전용 학습 실행 환경 준비 완료"
                : "PatchCore 실행 환경 설정 필요";
            string detail = report.CanTrain
                ? "정상 train 이미지로 메모리 뱅크를 만들고 정상 val 이미지로 임계값을 보정합니다. 결과 위치는 저장 전 확인할 후보입니다."
                : "torch, torchvision, numpy, Pillow가 설치된 Python과 앱에 포함된 PatchCore 모델 통신 프로세스를 확인하세요.";
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
            PythonModelSettings resolvedSettings = Clone(currentSettings);
            PythonModelRuntimePathResolver.ApplyPathDefaults(resolvedSettings);
            string engine = PythonModelSettings.NormalizeModelEngine(resolvedSettings.ModelEngine);
            if (string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(engine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal)
                || string.Equals(engine, PythonModelSettings.EnginePatchCore, StringComparison.Ordinal))
            {
                return false;
            }

            string yolo8Root = ResolveKnownLocalRuntimeFolder(resolvedSettings.ProjectRootPath, "yolov8");
            if (string.IsNullOrWhiteSpace(yolo8Root))
            {
                return false;
            }

            PythonModelRuntimeConnectionResult candidate = BuildYoloV8FolderConnection(
                resolvedSettings,
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
                ? "Ultralytics 실행 환경 연결 확인"
                : "Ultralytics 설치 필요";
            string detail = hasUltralytics
                ? "선택한 Python에서 ultralytics 패키지를 확인했습니다. 저장을 눌러 레시피에 반영하세요."
                : "선택한 Python에서 ultralytics 패키지를 찾지 못했습니다. 다음 단계에서 설치하거나 다른 venv를 연결하세요.";

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
