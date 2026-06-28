using Newtonsoft.Json.Linq;
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
    public sealed class YoloWorkerSmokeCandidate
    {
        public int Index { get; set; }
        public int? ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public System.Drawing.Rectangle ToRectangle()
        {
            int x = (int)Math.Round(X);
            int y = (int)Math.Round(Y);
            int width = (int)Math.Round(Width);
            int height = (int)Math.Round(Height);
            return width <= 0 || height <= 0
                ? System.Drawing.Rectangle.Empty
                : new System.Drawing.Rectangle(x, y, width, height);
        }
    }

    public sealed class YoloWorkerSmokeTestResult
    {
        public bool Succeeded { get; set; }
        public int ExitCode { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string PythonExecutablePath { get; set; } = string.Empty;
        public string ProjectRootPath { get; set; } = string.Empty;
        public string ClientScriptPath { get; set; } = string.Empty;
        public string ModelRootPath { get; set; } = string.Empty;
        public string WeightsPath { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public int CandidateCount { get; set; }
        public string FirstClassName { get; set; } = string.Empty;
        public double? FirstConfidence { get; set; }
        public IReadOnlyList<YoloWorkerSmokeCandidate> Candidates { get; set; } = Array.Empty<YoloWorkerSmokeCandidate>();
        public int? ElapsedMilliseconds { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }

    public static class YoloWorkerSmokeTestService
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png" };

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
        {
            settings ??= new PythonModelSettings();
            settings.EnsureDefaults();

            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string projectRootPath = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string clientScriptPath = settings.ClientScriptPath?.Trim() ?? string.Empty;
            string modelRootPath = settings.GetModelRootPath();
            string weightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            string imagePath = !string.IsNullOrWhiteSpace(imagePathOverride)
                ? imagePathOverride.Trim()
                : ResolveSmokeImagePath(settings);

            var errors = ValidateInputs(settings, pythonExecutablePath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            if (errors.Count > 0)
            {
                return BuildInputFailure(errors, pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            }

            int timeoutMilliseconds = Math.Clamp((settings.DetectionTimeoutSeconds + 90) * 1000, 120000, 300000);
            using var process = CreateSmokeProcess(
                pythonExecutablePath,
                projectRootPath,
                clientScriptPath,
                modelRootPath,
                weightsPath,
                imagePath,
                settings.InferenceImageSize,
                settings.MinimumDetectionConfidence);

            try
            {
                if (!process.Start())
                {
                    return BuildProcessFailure("YOLO smoke test process did not start.", pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                Task waitTask = process.WaitForExitAsync(cancellationToken);
                Task completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMilliseconds, cancellationToken)).ConfigureAwait(false);
                if (completed != waitTask)
                {
                    TryKill(process);
                    string output = await SafeRead(outputTask).ConfigureAwait(false);
                    string error = await SafeRead(errorTask).ConfigureAwait(false);
                    return BuildProcessFailure("YOLO smoke test timed out.", pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath, output, error);
                }

                string stdout = await SafeRead(outputTask).ConfigureAwait(false);
                string stderr = await SafeRead(errorTask).ConfigureAwait(false);
                return ParseSmokeOutput(process.ExitCode, stdout, stderr, pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return BuildProcessFailure("YOLO smoke test canceled.", pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            }
            catch (Exception ex)
            {
                return BuildProcessFailure(ex.Message, pythonExecutablePath, projectRootPath, clientScriptPath, modelRootPath, weightsPath, imagePath);
            }
        }

        public static string ResolveSmokeImagePath(PythonModelSettings settings)
        {
            settings ??= new PythonModelSettings();
            settings.EnsureDefaults();

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
                PythonModelSettings.GetDefaultImageRootPath()
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

        private static Process CreateSmokeProcess(
            string pythonExecutablePath,
            string projectRootPath,
            string clientScriptPath,
            string modelRootPath,
            string weightsPath,
            string imagePath,
            int imageSize,
            float confidence)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutablePath,
                    WorkingDirectory = Directory.Exists(projectRootPath) ? projectRootPath : AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.StartInfo.ArgumentList.Add(clientScriptPath);
            process.StartInfo.ArgumentList.Add("--smoke-test");
            process.StartInfo.ArgumentList.Add("--weights");
            process.StartInfo.ArgumentList.Add(weightsPath);
            process.StartInfo.ArgumentList.Add("--model-root");
            process.StartInfo.ArgumentList.Add(modelRootPath);
            process.StartInfo.ArgumentList.Add("--image");
            process.StartInfo.ArgumentList.Add(imagePath);
            process.StartInfo.ArgumentList.Add("--img-size");
            process.StartInfo.ArgumentList.Add(imageSize.ToString(CultureInfo.InvariantCulture));
            process.StartInfo.ArgumentList.Add("--conf");
            process.StartInfo.ArgumentList.Add(confidence.ToString(CultureInfo.InvariantCulture));
            return process;
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
            string errorMessage = envelope["error"]?["message"]?.Value<string>() ?? envelope["error"]?.Value<string>() ?? string.Empty;
            string summary = ok
                ? $"YOLO smoke test OK. Candidates:{parsedCandidates.Count}"
                : FirstNonEmpty(errorMessage, error, "YOLO smoke test failed.");

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
                ElapsedMilliseconds = envelope["elapsedMs"]?.Value<int?>(),
                Output = output ?? string.Empty,
                Error = error ?? string.Empty,
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
                        ?? 0D
                });
                fallbackIndex++;
            }

            return result;
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

        private static async Task<string> SafeRead(Task<string> task)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
