using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    public static class YoloWorkerSmokeTestService
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png" };
        private static readonly ExternalProcessRunner ProcessRunner = new ExternalProcessRunner();

        public static async Task<YoloWorkerSmokeTestResult> RunAsync(
            PythonModelSettings settings,
            CancellationToken cancellationToken = default)
        {
            return await RunAsync(settings, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<YoloWorkerSmokeTestResult> RunAsync(
            PythonModelSettings settings,
            string imagePathOverride,
            CancellationToken cancellationToken = default)
            => await RunAsync(settings, imagePathOverride, cancellationToken, false, string.Empty).ConfigureAwait(false);

        public static async Task<YoloWorkerSmokeTestResult> RunAsync(
            PythonModelSettings settings,
            string imagePathOverride,
            CancellationToken cancellationToken,
            bool decisionEvaluation,
            string decisionOutputDirectory = "")
        {
            settings ??= new PythonModelSettings();
            if (decisionEvaluation)
            {
                settings = JsonConvert.DeserializeObject<PythonModelSettings>(JsonConvert.SerializeObject(settings));
                settings.MinimumDetectionConfidence = 0;
                settings.ClientScriptPath = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine) switch
                {
                    PythonModelSettings.EngineUnet => PythonModelRuntimeBundledWorkerService.ResolveUnetWorkerScriptPath(),
                    PythonModelSettings.EnginePatchCore => PythonModelRuntimeBundledWorkerService.ResolvePatchCoreWorkerScriptPath(),
                    PythonModelSettings.EngineYoloV5 or PythonModelSettings.EngineYoloV8 or PythonModelSettings.EngineYolo11 => PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath(),
                    _ => settings.ClientScriptPath
                };
            }
            PythonModelRuntimePathResolver.ApplyDefaults(settings);

            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string projectRootPath = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string clientScriptPath = settings.ClientScriptPath?.Trim() ?? string.Empty;
            string modelRootPath = settings.GetModelRootPath();
            string weightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            string modelName = settings.GetProtocolModelName();
            string imagePath = !string.IsNullOrWhiteSpace(imagePathOverride)
                ? imagePathOverride.Trim()
                : ResolveSmokeImagePath(settings);

            var errors = ValidateInputs(settings, pythonExecutablePath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            if (errors.Count > 0)
            {
                return BuildInputFailure(errors, pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            }

            string captureImageHash = decisionEvaluation ? HashingService.ComputeFileSha256(imagePath, lowerCase: true) : string.Empty;
            string captureWeightsHash = decisionEvaluation ? HashingService.ComputeFileSha256(weightsPath, lowerCase: true) : string.Empty;
            int timeoutMilliseconds = Math.Clamp((settings.DetectionTimeoutSeconds + 90) * 1000, 120000, 300000);
            ProcessStartInfo startInfo = CreateSmokeStartInfo(
                pythonExecutablePath,
                projectRootPath,
                clientScriptPath,
                modelRootPath,
                weightsPath,
                imagePath,
                settings.InferenceImageSize,
                settings.MinimumDetectionConfidence,
                modelName, decisionEvaluation, decisionOutputDirectory);
            string decisionResultPath = string.Empty;
            if (decisionEvaluation && (modelName is "yolov5" or "yolov8" or "yolo11")
                && !string.IsNullOrWhiteSpace(decisionOutputDirectory))
            {
                Directory.CreateDirectory(decisionOutputDirectory);
                decisionResultPath = Path.Combine(Path.GetFullPath(decisionOutputDirectory), Guid.NewGuid().ToString("N") + "-result.json");
                startInfo.ArgumentList.Add("--result-json");
                startInfo.ArgumentList.Add(decisionResultPath);
            }
            ExternalProcessRunResult processResult = await ProcessRunner
                .RunAsync(startInfo, TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken)
                .ConfigureAwait(false);
            if (processResult.Canceled || !string.IsNullOrEmpty(decisionResultPath) && cancellationToken.IsCancellationRequested)
            {
                return BuildProcessFailure("YOLO smoke test canceled.", pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath, processResult.Output, processResult.Error);
            }

            if (processResult.TimedOut)
            {
                return BuildProcessFailure("YOLO smoke test timed out.", pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath, processResult.Output, processResult.Error);
            }

            if (!processResult.Started)
            {
                return BuildProcessFailure(
                    string.IsNullOrWhiteSpace(processResult.Error)
                        ? "YOLO smoke test process did not start."
                        : processResult.Error,
                    pythonExecutablePath,
                    projectRootPath,
                    clientScriptPath,
                    modelRootPath,
                    weightsPath,
                    imagePath,
                    processResult.Output,
                    processResult.Error);
            }

            string resultOutput = processResult.Output;
            if (!string.IsNullOrEmpty(decisionResultPath))
            {
                if (processResult.ExitCode != 0)
                {
                    YoloWorkerSmokeTestResult failure = ParseSmokeOutput(processResult.ExitCode, processResult.Output, processResult.Error,
                        pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
                    return BuildProcessFailure(FirstNonEmpty(failure.Errors.FirstOrDefault(), $"YOLO smoke test failed. ExitCode={processResult.ExitCode}"),
                        pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath, processResult.Output, processResult.Error, processResult.ExitCode);
                }
                try
                {
                    JObject envelope = JObject.Parse(File.ReadAllText(decisionResultPath, new System.Text.UTF8Encoding(false, true)));
                    if (envelope["type"]?.Value<string>() != "SmokeTestResult") throw new JsonException("Expected a SmokeTestResult object.");
                    resultOutput = envelope.ToString(Formatting.None);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or System.Text.DecoderFallbackException)
                {
                    return BuildProcessFailure("YOLO decision evaluation result could not be read: " + error.Message, pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath, processResult.Output, processResult.Error, processResult.ExitCode);
                }
            }
            YoloWorkerSmokeTestResult parsed = ParseSmokeOutput(
                processResult.ExitCode,
                resultOutput,
                processResult.Error,
                pythonExecutablePath,
                projectRootPath,
                clientScriptPath,
                modelRootPath,
                weightsPath,
                imagePath);
            parsed.Output = processResult.Output;
            if (decisionEvaluation)
            {
                parsed.CaptureImageSha256 = captureImageHash; parsed.CaptureWeightsSha256 = captureWeightsHash;
                parsed.HasCompletePredictions = parsed.HasCompletePredictions && parsed.Succeeded
                    && string.Equals(Path.GetFullPath(parsed.ImagePath), Path.GetFullPath(imagePath), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFullPath(parsed.WeightsPath), Path.GetFullPath(weightsPath), StringComparison.OrdinalIgnoreCase)
                    && HashingService.ComputeFileSha256(imagePath, lowerCase: true) == captureImageHash
                    && HashingService.ComputeFileSha256(weightsPath, lowerCase: true) == captureWeightsHash;
            }
            return parsed;
        }

        public static string ResolveSmokeImagePath(PythonModelSettings settings)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(settings);

            foreach (string root in BuildImageRootCandidates(settings))
            {
                string imagePath = FindFirstImage(root);
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    return imagePath;
                }
            }

            return string.Empty;
        }

        private static IReadOnlyList<string> BuildImageRootCandidates(PythonModelSettings settings)
        {
            string projectRootPath = settings.ProjectRootPath?.Trim() ?? string.Empty;
            return new[]
            {
                settings.ImageRootPath,
                Path.Combine(projectRootPath, "data", "train", "images"),
                Path.Combine(projectRootPath, "data", "valid", "images"),
                Path.Combine(projectRootPath, "data", "images"),
                PythonModelRuntimePathResolver.GetDefaultImageRootPath()
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        private static List<string> ValidateInputs(
            PythonModelSettings settings,
            string pythonExecutablePath,
            string clientScriptPath,
            string modelRootPath,
            string weightsPath,
            string imagePath)
        {
            var errors = new List<string>();
            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(settings, requireWeights: true);
            errors.AddRange(validation.Errors);

            if (PythonModelSettingsValidator.LooksLikePath(pythonExecutablePath) && !File.Exists(pythonExecutablePath))
            {
                errors.Add($"Python executable was not found: {pythonExecutablePath}");
            }

            if (string.IsNullOrWhiteSpace(clientScriptPath) || !File.Exists(clientScriptPath))
            {
                errors.Add($"YOLO TCP client script was not found: {clientScriptPath}");
            }

            if (string.IsNullOrWhiteSpace(modelRootPath) || !Directory.Exists(modelRootPath))
            {
                errors.Add($"YOLO model root was not found: {modelRootPath}");
            }

            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                errors.Add($"YOLO weight file was not found: {weightsPath}");
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                errors.Add("Smoke test image was not found. Put an image under data\\train\\images or set the image root.");
            }

            return errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static ProcessStartInfo CreateSmokeStartInfo(
            string pythonExecutablePath,
            string projectRootPath,
            string clientScriptPath,
            string modelRootPath,
            string weightsPath,
            string imagePath,
            int imageSize,
            float confidence,
            string modelName,
            bool decisionEvaluation = false,
            string decisionOutputDirectory = "")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutablePath,
                WorkingDirectory = Directory.Exists(projectRootPath) ? projectRootPath : AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add(clientScriptPath);
            startInfo.ArgumentList.Add("--smoke-test");
            if (decisionEvaluation) startInfo.ArgumentList.Add("--decision-evaluation");
            if (decisionEvaluation && modelName == "patchcore")
            {
                if (string.IsNullOrWhiteSpace(decisionOutputDirectory)) throw new ArgumentException("A decision-evaluation output directory is required.");
                startInfo.ArgumentList.Add("--heatmap-output");
                startInfo.ArgumentList.Add(Path.Combine(decisionOutputDirectory, Guid.NewGuid().ToString("N") + "-heatmap.png"));
            }
            startInfo.ArgumentList.Add("--weights");
            startInfo.ArgumentList.Add(weightsPath);
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                startInfo.ArgumentList.Add("--model");
                startInfo.ArgumentList.Add(modelName.Trim());
            }

            startInfo.ArgumentList.Add("--model-root");
            startInfo.ArgumentList.Add(modelRootPath);
            startInfo.ArgumentList.Add("--image");
            startInfo.ArgumentList.Add(imagePath);
            startInfo.ArgumentList.Add("--img-size");
            startInfo.ArgumentList.Add(imageSize.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--conf");
            startInfo.ArgumentList.Add(confidence.ToString(CultureInfo.InvariantCulture));
            return startInfo;
        }

        private static YoloWorkerSmokeTestResult ParseSmokeOutput(
            int exitCode,
            string output,
            string error,
            string pythonExecutablePath,
            string projectRootPath,
            string clientScriptPath,
            string modelRootPath,
            string weightsPath,
            string imagePath)
        {
            JObject envelope = TryFindLastJsonObject(output);
            if (envelope == null)
            {
                string failureSummary = exitCode == 0
                    ? "YOLO smoke test finished but no JSON result was returned."
                    : FirstNonEmpty(error, output, $"YOLO smoke test failed. ExitCode={exitCode}");
                return BuildProcessFailure(failureSummary, pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath, output, error, exitCode);
            }

            bool ok = envelope["ok"]?.Value<bool>() == true;
            JArray candidates = envelope["candidates"] as JArray ?? new JArray();
            IReadOnlyList<YoloWorkerSmokeCandidate> parsedCandidates = ParseCandidates(candidates);
            YoloWorkerSmokeCandidate firstCandidate = parsedCandidates.FirstOrDefault();
            string errorCode = envelope["error"]?["code"]?.Value<string>() ?? string.Empty;
            string errorMessage = envelope["error"]?["message"]?.Value<string>() ?? envelope["error"]?.Value<string>() ?? string.Empty;
            string summary = ok
                ? $"YOLO smoke test OK. Candidates:{parsedCandidates.Count}"
                : FirstNonEmpty(
                    !string.IsNullOrWhiteSpace(errorCode) && !string.IsNullOrWhiteSpace(errorMessage)
                        ? $"{errorCode}: {errorMessage}"
                        : string.Empty,
                    errorMessage,
                    error,
                    "YOLO smoke test failed.");

            return new YoloWorkerSmokeTestResult
            {
                Succeeded = ok && exitCode == 0,
                ExitCode = exitCode,
                Summary = summary,
                PythonExecutablePath = pythonExecutablePath,
                ProjectRootPath = projectRootPath,
                ClientScriptPath = clientScriptPath,
                ModelRootPath = modelRootPath,
                WeightsPath = envelope["weightsPath"]?.Value<string>() ?? weightsPath,
                ImagePath = envelope["image"]?["path"]?.Value<string>() ?? imagePath,
                CandidateCount = parsedCandidates.Count,
                FirstClassName = firstCandidate?.ClassName ?? string.Empty,
                FirstConfidence = firstCandidate?.Confidence,
                Candidates = parsedCandidates,
                CaptureConfidenceFloor = envelope["image"]?["captureConfidenceFloor"]?.Value<double?>(),
                HasCompletePredictions = envelope["image"]?["hasCompletePredictions"]?.Value<bool>() == true,
                CaptureCandidateLimit = envelope["image"]?["captureCandidateLimit"]?.Value<int?>(),
                CaptureAnomalyScore = envelope["image"]?["anomalyScore"]?.Value<double?>(),
                ElapsedMilliseconds = envelope["elapsedMs"]?.Value<int?>(),
                Output = output ?? string.Empty,
                Error = error ?? string.Empty,
                ErrorCode = errorCode,
                Errors = ok ? Array.Empty<string>() : new[] { summary }
            };
        }

        private static IReadOnlyList<YoloWorkerSmokeCandidate> ParseCandidates(JArray candidates)
        {
            var result = new List<YoloWorkerSmokeCandidate>();
            if (candidates == null)
            {
                return result;
            }

            int fallbackIndex = 1;
            foreach (JObject candidate in candidates.OfType<JObject>())
            {
                result.Add(new YoloWorkerSmokeCandidate
                {
                    Index = candidate["index"]?.Value<int?>()
                        ?? candidate["candidateIndex"]?.Value<int?>()
                        ?? fallbackIndex,
                    ClassId = candidate["classId"]?.Value<int?>()
                        ?? candidate["cls"]?.Value<int?>(),
                    ClassName = FirstNonEmpty(
                        candidate["className"]?.Value<string>(),
                        candidate["name"]?.Value<string>(),
                        "Defect"),
                    Confidence = candidate["confidence"]?.Value<double?>()
                        ?? candidate["conf"]?.Value<double?>()
                        ?? candidate["score"]?.Value<double?>()
                        ?? 0D,
                    X = candidate["x"]?.Value<double?>()
                        ?? candidate["left"]?.Value<double?>()
                        ?? 0D,
                    Y = candidate["y"]?.Value<double?>()
                        ?? candidate["top"]?.Value<double?>()
                        ?? 0D,
                    Width = candidate["width"]?.Value<double?>()
                        ?? candidate["w"]?.Value<double?>()
                        ?? 0D,
                    Height = candidate["height"]?.Value<double?>()
                        ?? candidate["h"]?.Value<double?>()
                        ?? 0D,
                    CandidateType = candidate["candidateType"]?.Value<string>() ?? string.Empty,
                    PredictionType = candidate["predictionType"]?.Value<string>() ?? string.Empty,
                    ImageLevel = candidate["imageLevel"]?.Value<bool?>() ?? false,
                    AnomalyScore = candidate["anomalyScore"]?.Value<double?>(),
                    AnomalyThreshold = candidate["anomalyThreshold"]?.Value<double?>(),
                    HeatmapPath = candidate["heatmapPath"]?.Value<string>() ?? string.Empty,
                    SegmentationType = candidate["segmentationType"]?.Value<string>() ?? string.Empty,
                    PolygonPoints = ParsePolygonPoints(candidate["polygonPoints"]),
                    NormalizedPolygonPoints = ParsePolygonPoints(candidate["normalizedPolygonPoints"])
                });
                fallbackIndex++;
            }

            return result;
        }

        private static IReadOnlyList<DetectionPolygonPoint> ParsePolygonPoints(JToken token)
        {
            if (!(token is JArray array))
            {
                return Array.Empty<DetectionPolygonPoint>();
            }

            return array
                .OfType<JObject>()
                .Select(point => new DetectionPolygonPoint
                {
                    X = point["x"]?.Value<float?>() ?? 0F,
                    Y = point["y"]?.Value<float?>() ?? 0F
                })
                .ToList();
        }

        private static JObject TryFindLastJsonObject(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            foreach (string line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Reverse())
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    return JObject.Parse(trimmed);
                }
                catch
                {
                    // Keep scanning; Python or torch may write non-result JSON in future.
                }
            }

            return null;
        }

        private static string FindFirstImage(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return string.Empty;
            }

            return Directory
                .EnumerateFiles(rootPath)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? string.Empty;
        }

        private static YoloWorkerSmokeTestResult BuildInputFailure(
            IReadOnlyList<string> errors,
            string pythonExecutablePath,
            string projectRootPath,
            string clientScriptPath,
            string modelRootPath,
            string weightsPath,
            string imagePath)
        {
            return new YoloWorkerSmokeTestResult
            {
                Succeeded = false,
                ExitCode = -1,
                Summary = errors.FirstOrDefault() ?? "YOLO smoke test inputs are not ready.",
                PythonExecutablePath = pythonExecutablePath,
                ProjectRootPath = projectRootPath,
                ClientScriptPath = clientScriptPath,
                ModelRootPath = modelRootPath,
                WeightsPath = weightsPath,
                ImagePath = imagePath,
                Errors = errors?.ToList() ?? new List<string>()
            };
        }

        private static YoloWorkerSmokeTestResult BuildProcessFailure(
            string summary,
            string pythonExecutablePath,
            string projectRootPath,
            string clientScriptPath,
            string modelRootPath,
            string weightsPath,
            string imagePath,
            string output = "",
            string error = "",
            int exitCode = -1)
        {
            return new YoloWorkerSmokeTestResult
            {
                Succeeded = false,
                ExitCode = exitCode,
                Summary = summary ?? "YOLO smoke test failed.",
                PythonExecutablePath = pythonExecutablePath,
                ProjectRootPath = projectRootPath,
                ClientScriptPath = clientScriptPath,
                ModelRootPath = modelRootPath,
                WeightsPath = weightsPath,
                ImagePath = imagePath,
                Output = output ?? string.Empty,
                Error = error ?? string.Empty,
                Errors = new[] { summary ?? "YOLO smoke test failed." }
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
