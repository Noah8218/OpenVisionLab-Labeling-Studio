using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public class MobileSamBoxPromptRequest
    {
        public string PythonExecutablePath { get; init; } = string.Empty;
        public string WorkerScriptPath { get; init; } = string.Empty;
        public string WeightsPath { get; init; } = string.Empty;
        public string ImagePath { get; init; } = string.Empty;
        public Rectangle PromptBounds { get; init; }
        public int? ClassId { get; init; }
        public string ClassName { get; init; } = string.Empty;
        public string Device { get; init; } = "cpu";
        public IReadOnlyList<WpfSmartMaskPromptPoint> PromptPoints { get; init; } = Array.Empty<WpfSmartMaskPromptPoint>();
        public int MaximumPolygonPoints { get; init; } = 96;
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public class MobileSamBoxPromptResult
    {
        public bool Succeeded { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
        public string ErrorCode { get; init; } = string.Empty;
        public string WeightsSha256 { get; init; } = string.Empty;
        public string RuntimeSummary { get; init; } = string.Empty;
        public double ElapsedMilliseconds { get; init; }
        public int MaskArea { get; init; }
        public YoloWorkerSmokeCandidate Candidate { get; init; }
    }

    public class MobileSamBoxPromptService
    {
        private readonly ExternalProcessRunner processRunner;

        public MobileSamBoxPromptService(ExternalProcessRunner processRunner = null)
        {
            this.processRunner = processRunner ?? new ExternalProcessRunner();
        }

        public MobileSamBoxPromptRequest BuildRequest(
            PythonModelSettings currentSettings,
            string imagePath,
            Rectangle promptBounds,
            int? classId,
            string className,
            IReadOnlyList<WpfSmartMaskPromptPoint> promptPoints = null,
            int maximumPolygonPoints = 96)
        {
            currentSettings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(currentSettings);
            string normalizedImagePath = string.IsNullOrWhiteSpace(imagePath)
                ? string.Empty
                : Path.GetFullPath(imagePath);
            string runtimeRoot = ResolveRuntimeRoot(currentSettings);
            string pythonPath = string.IsNullOrWhiteSpace(runtimeRoot)
                ? string.Empty
                : Path.Combine(runtimeRoot, ".venv", "Scripts", "python.exe");
            string weightsPath = string.IsNullOrWhiteSpace(runtimeRoot)
                ? string.Empty
                : Path.Combine(runtimeRoot, "mobile_sam.pt");
            string workerPath = PythonModelRuntimeBundledWorkerService.ResolveMobileSamBoxPromptWorkerScriptPath();
            var errors = new List<string>();
            if (!File.Exists(pythonPath))
            {
                errors.Add("Ultralytics Python 실행기를 찾을 수 없습니다.");
            }
            if (!File.Exists(weightsPath))
            {
                errors.Add("MobileSAM 가중치 mobile_sam.pt를 찾을 수 없습니다.");
            }
            if (!File.Exists(workerPath))
            {
                errors.Add("앱의 MobileSAM 박스 프롬프트 worker를 찾을 수 없습니다.");
            }
            if (string.IsNullOrWhiteSpace(normalizedImagePath) || !File.Exists(normalizedImagePath))
            {
                errors.Add("스마트 마스크를 만들 원본 이미지를 찾을 수 없습니다.");
            }
            if (promptBounds.Width <= 0 || promptBounds.Height <= 0)
            {
                errors.Add("결함을 감싼 박스를 먼저 그려야 합니다.");
            }

            return new MobileSamBoxPromptRequest
            {
                PythonExecutablePath = pythonPath,
                WorkerScriptPath = workerPath,
                WeightsPath = weightsPath,
                ImagePath = normalizedImagePath,
                PromptBounds = promptBounds,
                ClassId = classId,
                ClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim(),
                Device = "cpu",
                PromptPoints = promptPoints ?? Array.Empty<WpfSmartMaskPromptPoint>(),
                MaximumPolygonPoints = Math.Clamp(maximumPolygonPoints, 16, 1024),
                Errors = errors
            };
        }

        public async Task<MobileSamBoxPromptResult> RunAsync(
            MobileSamBoxPromptRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request?.IsValid != true)
            {
                return Failure("InvalidInput", string.Join(" ", request?.Errors ?? Array.Empty<string>()));
            }

            ExternalProcessRunResult processResult = await processRunner
                .RunAsync(CreateStartInfo(request), TimeSpan.FromSeconds(45), cancellationToken)
                .ConfigureAwait(false);
            if (processResult.Canceled)
            {
                return Failure("Canceled", "MobileSAM 후보 생성을 취소했습니다.");
            }

            if (processResult.TimedOut)
            {
                return Failure("Timeout", "MobileSAM 후보 생성은 45초 안에 끝나지 않았습니다.");
            }

            if (!processResult.Started)
            {
                return Failure("ProcessStartFailed", processResult.Error);
            }

            return ParseResult(processResult.ExitCode, processResult.Output, processResult.Error, request);
        }

        public MobileSamBoxPromptResult ParseResult(
            int exitCode,
            string stdout,
            string stderr,
            MobileSamBoxPromptRequest request)
        {
            JObject payload = ParseLastJsonObject(exitCode == 0 ? stdout : stderr)
                ?? ParseLastJsonObject(stdout)
                ?? ParseLastJsonObject(stderr);
            if (exitCode != 0 || payload?.Value<bool?>("success") != true)
            {
                return Failure(
                    payload?.Value<string>("errorCode") ?? "WorkerFailed",
                    payload?.Value<string>("error") ?? FirstNonEmpty(stderr, stdout, "MobileSAM worker가 실패했습니다."));
            }

            IReadOnlyList<DetectionPolygonPoint> polygon = (payload["polygon"] as JArray)?
                .OfType<JObject>()
                .Select(point => new DetectionPolygonPoint
                {
                    X = point.Value<float?>("x") ?? 0F,
                    Y = point.Value<float?>("y") ?? 0F
                })
                .ToList() ?? new List<DetectionPolygonPoint>();
            if (polygon.Count < 3)
            {
                return Failure("InvalidMask", "MobileSAM 후보 경계점이 3개보다 적습니다.");
            }

            JObject bounds = payload["bounds"] as JObject;
            var candidate = new YoloWorkerSmokeCandidate
            {
                Index = 1,
                ClassId = request.ClassId,
                ClassName = request.ClassName,
                Confidence = 1D,
                X = bounds?.Value<double?>("x") ?? 0D,
                Y = bounds?.Value<double?>("y") ?? 0D,
                Width = Math.Max(1D, bounds?.Value<double?>("width") ?? 0D),
                Height = Math.Max(1D, bounds?.Value<double?>("height") ?? 0D),
                CandidateType = "smart-mask",
                PredictionType = "segmentation-assist",
                SegmentationType = "polygon",
                PolygonPoints = polygon
            };
            double elapsed = payload.Value<double?>("elapsedMs") ?? 0D;
            string runtime = string.Join(
                " / ",
                new[]
                {
                    payload.Value<string>("model"),
                    "Ultralytics " + (payload.Value<string>("ultralyticsVersion") ?? "-"),
                    "Torch " + (payload.Value<string>("torchVersion") ?? "-"),
                    payload.Value<string>("device")
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return new MobileSamBoxPromptResult
            {
                Succeeded = true,
                Candidate = candidate,
                ElapsedMilliseconds = elapsed,
                MaskArea = payload.Value<int?>("maskArea") ?? 0,
                WeightsSha256 = payload.Value<string>("weightsSha256") ?? string.Empty,
                RuntimeSummary = runtime,
                Summary = $"스마트 마스크 후보 1개 생성 / +{request.PromptPoints.Count(point => point.Kind == WpfSmartMaskPointKind.Positive)} -{request.PromptPoints.Count(point => point.Kind == WpfSmartMaskPointKind.Negative)} / 경계점 {polygon.Count}개 / {elapsed.ToString("N0", CultureInfo.CurrentCulture)}ms"
            };
        }

        private static ProcessStartInfo CreateStartInfo(MobileSamBoxPromptRequest request)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.PythonExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(request.WorkerScriptPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(request.WorkerScriptPath);
            AddArgument(startInfo, "--weights", request.WeightsPath);
            AddArgument(startInfo, "--image", request.ImagePath);
            AddArgument(startInfo, "--x", request.PromptBounds.X.ToString(CultureInfo.InvariantCulture));
            AddArgument(startInfo, "--y", request.PromptBounds.Y.ToString(CultureInfo.InvariantCulture));
            AddArgument(startInfo, "--width", request.PromptBounds.Width.ToString(CultureInfo.InvariantCulture));
            AddArgument(startInfo, "--height", request.PromptBounds.Height.ToString(CultureInfo.InvariantCulture));
            AddArgument(startInfo, "--device", request.Device);
            AddArgument(startInfo, "--max-polygon-points", request.MaximumPolygonPoints.ToString(CultureInfo.InvariantCulture));
            foreach (WpfSmartMaskPromptPoint point in request.PromptPoints)
            {
                AddArgument(
                    startInfo,
                    "--point",
                    string.Join(
                        ",",
                        point.Position.X.ToString(CultureInfo.InvariantCulture),
                        point.Position.Y.ToString(CultureInfo.InvariantCulture),
                        point.Label.ToString(CultureInfo.InvariantCulture)));
            }
            return startInfo;
        }

        private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
        {
            startInfo.ArgumentList.Add(name);
            startInfo.ArgumentList.Add(value ?? string.Empty);
        }

        private static JObject ParseLastJsonObject(string text)
        {
            foreach (string line in (text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Reverse())
            {
                try
                {
                    return JObject.Parse(line.Trim());
                }
                catch
                {
                }
            }

            return null;
        }

        private static string ResolveRuntimeRoot(PythonModelSettings settings)
        {
            foreach (string source in new[]
            {
                settings?.ProjectRootPath,
                Environment.CurrentDirectory,
                AppContext.BaseDirectory
            })
            {
                string candidate = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(source, "yolov8");
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

        private static MobileSamBoxPromptResult Failure(string code, string message)
            => new MobileSamBoxPromptResult
            {
                ErrorCode = code ?? string.Empty,
                Error = message ?? string.Empty,
                Summary = message ?? string.Empty
            };

    }

    [Obsolete("Use MobileSamBoxPromptService.", false)]
    public sealed class WpfMobileSamBoxPromptService : MobileSamBoxPromptService
    {
        public WpfMobileSamBoxPromptService(ExternalProcessRunner processRunner = null)
            : base(processRunner)
        {
        }

        public new WpfMobileSamBoxPromptRequest BuildRequest(
            PythonModelSettings currentSettings,
            string imagePath,
            Rectangle promptBounds,
            int? classId,
            string className,
            IReadOnlyList<WpfSmartMaskPromptPoint> promptPoints = null,
            int maximumPolygonPoints = 96)
        {
            return WpfMobileSamBoxPromptRequest.FromCanonical(base.BuildRequest(
                currentSettings,
                imagePath,
                promptBounds,
                classId,
                className,
                promptPoints,
                maximumPolygonPoints));
        }

        public async Task<WpfMobileSamBoxPromptResult> RunAsync(
            WpfMobileSamBoxPromptRequest request,
            CancellationToken cancellationToken = default)
        {
            MobileSamBoxPromptResult result = await base.RunAsync(request, cancellationToken).ConfigureAwait(false);
            return WpfMobileSamBoxPromptResult.FromCanonical(result);
        }

        public WpfMobileSamBoxPromptResult ParseResult(
            int exitCode,
            string stdout,
            string stderr,
            WpfMobileSamBoxPromptRequest request)
        {
            return WpfMobileSamBoxPromptResult.FromCanonical(base.ParseResult(exitCode, stdout, stderr, request));
        }
    }

    [Obsolete("Use MobileSamBoxPromptRequest.", false)]
    public sealed class WpfMobileSamBoxPromptRequest : MobileSamBoxPromptRequest
    {
        internal static WpfMobileSamBoxPromptRequest FromCanonical(MobileSamBoxPromptRequest source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfMobileSamBoxPromptRequest
            {
                PythonExecutablePath = source.PythonExecutablePath,
                WorkerScriptPath = source.WorkerScriptPath,
                WeightsPath = source.WeightsPath,
                ImagePath = source.ImagePath,
                PromptBounds = source.PromptBounds,
                ClassId = source.ClassId,
                ClassName = source.ClassName,
                Device = source.Device,
                PromptPoints = source.PromptPoints,
                MaximumPolygonPoints = source.MaximumPolygonPoints,
                Errors = source.Errors
            };
        }
    }

    [Obsolete("Use MobileSamBoxPromptResult.", false)]
    public sealed class WpfMobileSamBoxPromptResult : MobileSamBoxPromptResult
    {
        internal static WpfMobileSamBoxPromptResult FromCanonical(MobileSamBoxPromptResult source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfMobileSamBoxPromptResult
            {
                Succeeded = source.Succeeded,
                Summary = source.Summary,
                Error = source.Error,
                ErrorCode = source.ErrorCode,
                WeightsSha256 = source.WeightsSha256,
                RuntimeSummary = source.RuntimeSummary,
                ElapsedMilliseconds = source.ElapsedMilliseconds,
                MaskArea = source.MaskArea,
                Candidate = source.Candidate
            };
        }
    }
}
