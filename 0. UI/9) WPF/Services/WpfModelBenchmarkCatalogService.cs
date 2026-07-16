using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfModelBenchmarkCatalogService
    {
        public IReadOnlyList<WpfModelBenchmarkRun> Load(string repositoryRoot = "")
        {
            string root = string.IsNullOrWhiteSpace(repositoryRoot)
                ? FindRepositoryRoot()
                : Path.GetFullPath(repositoryRoot);
            var runs = new List<WpfModelBenchmarkRun>();

            LoadPairwiseComparisons(Path.Combine(root, "artifacts", "yolo-model-comparison"), runs);
            LoadAnomalyClassifications(Path.Combine(root, "artifacts", "yolo-classification-evaluation"), runs);

            return runs
                .GroupBy(run => run.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(run => run.CreatedAt)
                .ThenBy(run => run.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        public static string FindRepositoryRoot(string startPath = "")
        {
            IEnumerable<string> starts = new[]
            {
                startPath,
                Environment.CurrentDirectory,
                AppContext.BaseDirectory
            }.Where(path => !string.IsNullOrWhiteSpace(path));

            foreach (string start in starts)
            {
                DirectoryInfo current;
                try
                {
                    current = new DirectoryInfo(Path.GetFullPath(start));
                }
                catch
                {
                    continue;
                }

                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "OpenVisionLab.LabelingStudio.csproj")))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            return Environment.CurrentDirectory;
        }

        private static void LoadPairwiseComparisons(string artifactsRoot, ICollection<WpfModelBenchmarkRun> runs)
        {
            foreach (string summaryPath in EnumerateSummaryFiles(artifactsRoot, "comparison-summary.json"))
            {
                try
                {
                    JObject summary = JObject.Parse(File.ReadAllText(summaryPath));
                    AddPairwiseRun(summary, summaryPath, "baseline", runs);
                    AddPairwiseRun(summary, summaryPath, "candidate", runs);
                }
                catch
                {
                    // A broken historical report must not hide the remaining catalog.
                }
            }
        }

        private static void AddPairwiseRun(
            JObject summary,
            string summaryPath,
            string role,
            ICollection<WpfModelBenchmarkRun> runs)
        {
            JToken model = summary.SelectToken(role);
            if (model == null)
            {
                return;
            }

            string engine = FirstNonEmpty(
                model.SelectToken("engine")?.Value<string>(),
                model.SelectToken("name")?.Value<string>(),
                "Model runtime");
            string weightsPath = model.SelectToken("weights")?.Value<string>() ?? string.Empty;
            string taskKey = NormalizeTaskKey(summary.SelectToken("modelTask")?.Value<string>());
            string split = FirstNonEmpty(
                summary.SelectToken("evidence.split")?.Value<string>(),
                summary.SelectToken("task")?.Value<string>(),
                "unknown");
            int evidenceCount = summary.SelectToken("evidence.imageCount")?.Value<int?>()
                ?? summary.SelectToken("evidence.comparisonLabelCount")?.Value<int?>()
                ?? 0;
            int repeatCount = model.SelectToken("benchmark.repeatCount")?.Value<int?>()
                ?? summary.SelectToken("benchmarkRepeatCount")?.Value<int?>()
                ?? 0;
            var metrics = new List<WpfModelBenchmarkMetric>
            {
                ReadMetric(model, "metrics.precision", "precision", "Precision", 10, isPercent: true, supportsDelta: true),
                ReadMetric(model, "metrics.recall", "recall", "Recall", 20, isPercent: true, supportsDelta: true),
                ReadMetric(model, "metrics.map50", "map50", "mAP50", 30, isPercent: true, supportsDelta: true),
                ReadMetric(model, "metrics.map5095", "map5095", "mAP50-95", 40, isPercent: true, supportsDelta: true)
            };

            int? uiCandidateCount = model.SelectToken("confidence.uiCandidateCount")?.Value<int?>();
            if (uiCandidateCount.HasValue)
            {
                metrics.Add(new WpfModelBenchmarkMetric(
                    "uiCandidateCount",
                    "UI candidates",
                    uiCandidateCount.Value,
                    order: 80,
                    isPercent: false,
                    supportsDelta: false));
            }

            DateTimeOffset createdAt = ReadDateTimeOffset(
                summary.SelectToken("createdAt")?.Value<string>(),
                File.GetLastWriteTimeUtc(summaryPath));
            string dataPath = summary.SelectToken("dataYaml")?.Value<string>() ?? string.Empty;
            string evidenceFingerprintSha256 = summary.SelectToken("evidence.fingerprintSha256")?.Value<string>() ?? string.Empty;
            string modelName = BuildModelName(weightsPath, engine);
            runs.Add(new WpfModelBenchmarkRun(
                id: summaryPath + "|" + role,
                sourcePath: summaryPath,
                sourceRole: role,
                sourceTypeText: "\uBAA8\uB378 \uBE44\uAD50",
                createdAt: createdAt,
                displayName: engine + " \u00B7 " + modelName,
                modelName: modelName,
                runtimeName: engine,
                taskKey: taskKey,
                taskText: FormatTaskText(taskKey),
                weightsPath: weightsPath,
                weightsSha256: model.SelectToken("weightsSha256")?.Value<string>() ?? string.Empty,
                evaluationDataPath: dataPath,
                evidenceFingerprintSha256: evidenceFingerprintSha256,
                split: split,
                evidenceCount: evidenceCount,
                imageSize: summary.SelectToken("imageSize")?.Value<int?>() ?? 0,
                batchSize: summary.SelectToken("batchSize")?.Value<int?>() ?? 0,
                confidence: summary.SelectToken("uiConfidence")?.Value<double?>(),
                timingSource: model.SelectToken("benchmark.source")?.Value<string>() ?? string.Empty,
                timingRepeatCount: repeatCount,
                taktMs: model.SelectToken("benchmark.taktMs")?.Value<double?>(),
                taktMinMs: model.SelectToken("benchmark.taktMinMs")?.Value<double?>(),
                taktMaxMs: model.SelectToken("benchmark.taktMaxMs")?.Value<double?>(),
                decisionText: FormatDecisionText(summary.SelectToken("promotion.recommendation")?.Value<string>()),
                metrics: metrics.Where(metric => metric != null).ToArray(),
                classMetrics: ReadClassMetrics(model.SelectToken("classMetrics")),
                groundTruthReview: ReadGroundTruthReview(model.SelectToken("groundTruthReview"))));
        }

        private static void LoadAnomalyClassifications(string artifactsRoot, ICollection<WpfModelBenchmarkRun> runs)
        {
            foreach (string summaryPath in EnumerateSummaryFiles(artifactsRoot, "classification-evaluation-summary.json"))
            {
                try
                {
                    JObject summary = JObject.Parse(File.ReadAllText(summaryPath));
                    string weightsPath = summary.SelectToken("weightsPath")?.Value<string>() ?? string.Empty;
                    string modelName = BuildModelName(weightsPath, "Classification model");
                    string runtimeName = InferClassificationRuntime(weightsPath, modelName);
                    var metrics = new List<WpfModelBenchmarkMetric>
                    {
                        ReadMetric(summary, "metrics.accuracy", "accuracy", "Accuracy", 10, isPercent: true, supportsDelta: true),
                        ReadMetric(summary, "metrics.normalAccuracy", "normalAccuracy", "Normal accuracy", 20, isPercent: true, supportsDelta: true),
                        ReadMetric(summary, "metrics.abnormalAccuracy", "abnormalAccuracy", "Abnormal accuracy", 30, isPercent: true, supportsDelta: true),
                        ReadMetric(summary, "metrics.totalImageCount", "totalImageCount", "Images", 70, isPercent: false, supportsDelta: false),
                        ReadMetric(summary, "metrics.correctImageCount", "correctImageCount", "Correct", 80, isPercent: false, supportsDelta: false)
                    };
                    double? total = summary.SelectToken("metrics.totalImageCount")?.Value<double?>();
                    double? correct = summary.SelectToken("metrics.correctImageCount")?.Value<double?>();
                    if (total.HasValue && correct.HasValue)
                    {
                        metrics.Add(new WpfModelBenchmarkMetric(
                            "misclassifiedImageCount",
                            "Misclassified",
                            Math.Max(0D, total.Value - correct.Value),
                            order: 90,
                            isPercent: false,
                            supportsDelta: false));
                    }

                    runs.Add(new WpfModelBenchmarkRun(
                        id: summaryPath + "|evaluation",
                        sourcePath: summaryPath,
                        sourceRole: "evaluation",
                        sourceTypeText: "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00",
                        createdAt: ReadDateTimeOffset(
                            summary.SelectToken("generatedUtc")?.Value<string>(),
                            File.GetLastWriteTimeUtc(summaryPath)),
                        displayName: modelName,
                        modelName: modelName,
                        runtimeName: runtimeName,
                        taskKey: "anomaly-classification",
                        taskText: "\uC774\uC0C1 \uBD84\uB958",
                        weightsPath: weightsPath,
                        weightsSha256: summary.SelectToken("weightsSha256")?.Value<string>() ?? string.Empty,
                        evaluationDataPath: summary.SelectToken("datasetRoot")?.Value<string>() ?? string.Empty,
                        evidenceFingerprintSha256: summary.SelectToken("evidence.fingerprintSha256")?.Value<string>() ?? string.Empty,
                        split: summary.SelectToken("split")?.Value<string>() ?? "unknown",
                        evidenceCount: summary.SelectToken("metrics.totalImageCount")?.Value<int?>() ?? 0,
                        imageSize: 0,
                        batchSize: 0,
                        confidence: summary.SelectToken("thresholds.minimumConfidence")?.Value<double?>(),
                        timingSource: string.Empty,
                        timingRepeatCount: 0,
                        taktMs: null,
                        taktMinMs: null,
                        taktMaxMs: null,
                        decisionText: FormatDecisionText(summary.SelectToken("promotion.recommendation")?.Value<string>()),
                        metrics: metrics.Where(metric => metric != null).ToArray(),
                        classMetrics: Array.Empty<WpfModelBenchmarkClassMetric>(),
                        groundTruthReview: null));
                }
                catch
                {
                    // A broken historical report must not hide the remaining catalog.
                }
            }
        }

        private static IEnumerable<string> EnumerateSummaryFiles(string artifactsRoot, string fileName)
        {
            if (!Directory.Exists(artifactsRoot))
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.EnumerateFiles(artifactsRoot, fileName, SearchOption.AllDirectories).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static WpfModelBenchmarkMetric ReadMetric(
            JToken root,
            string tokenPath,
            string key,
            string displayName,
            int order,
            bool isPercent,
            bool supportsDelta)
        {
            double? value = root?.SelectToken(tokenPath)?.Value<double?>();
            return value.HasValue
                ? new WpfModelBenchmarkMetric(key, displayName, value.Value, order, isPercent, supportsDelta)
                : null;
        }

        private static IReadOnlyList<WpfModelBenchmarkClassMetric> ReadClassMetrics(JToken token)
        {
            return token?.Children<JObject>()
                .Select(item => new WpfModelBenchmarkClassMetric(
                    item.SelectToken("classId")?.Value<int?>() ?? -1,
                    item.SelectToken("className")?.Value<string>() ?? string.Empty,
                    item.SelectToken("imageCount")?.Value<int?>(),
                    item.SelectToken("instanceCount")?.Value<int?>(),
                    item.SelectToken("precision")?.Value<double?>(),
                    item.SelectToken("recall")?.Value<double?>(),
                    item.SelectToken("map50")?.Value<double?>(),
                    item.SelectToken("map5095")?.Value<double?>()))
                .Where(item => item.ClassId >= 0)
                .OrderBy(item => item.ClassId)
                .ToArray()
                ?? Array.Empty<WpfModelBenchmarkClassMetric>();
        }

        private static WpfModelBenchmarkGroundTruthReview ReadGroundTruthReview(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            int schemaVersion = ReadNonNegativeInt(token.SelectToken("schemaVersion")) ?? 1;
            string schema = token.SelectToken("schema")?.Value<string>() ?? string.Empty;
            string geometryCoordinateSystem = token.SelectToken("geometryCoordinateSystem")?.Value<string>() ?? string.Empty;

            IReadOnlyList<WpfModelBenchmarkGroundTruthClassReview> perClass = token.SelectToken("perClass")?.Children<JObject>()
                .Select(item => new WpfModelBenchmarkGroundTruthClassReview(
                    item.SelectToken("classId")?.Value<int?>() ?? -1,
                    item.SelectToken("className")?.Value<string>() ?? string.Empty,
                    item.SelectToken("groundTruthCount")?.Value<int?>() ?? 0,
                    item.SelectToken("predictionCount")?.Value<int?>() ?? 0,
                    item.SelectToken("truePositiveCount")?.Value<int?>() ?? 0,
                    item.SelectToken("falsePositiveCount")?.Value<int?>() ?? 0,
                    item.SelectToken("falseNegativeCount")?.Value<int?>() ?? 0))
                .Where(item => item.ClassId >= 0)
                .OrderBy(item => item.ClassId)
                .ToArray()
                ?? Array.Empty<WpfModelBenchmarkGroundTruthClassReview>();
            JToken exampleToken = token.SelectToken("examples");
            IEnumerable<JObject> exampleItems = exampleToken is JObject singleExample
                ? new[] { singleExample }
                : exampleToken?.Children<JObject>() ?? Enumerable.Empty<JObject>();
            IReadOnlyList<WpfModelBenchmarkGroundTruthExample> examples = exampleItems
                .Select(item => new WpfModelBenchmarkGroundTruthExample(
                    item.SelectToken("imagePath")?.Value<string>() ?? string.Empty,
                    item.SelectToken("imageName")?.Value<string>() ?? string.Empty,
                    item.SelectToken("errorType")?.Value<string>() ?? string.Empty,
                    item.SelectToken("classId")?.Value<int?>() ?? -1,
                    item.SelectToken("className")?.Value<string>() ?? string.Empty,
                    ReadFiniteDouble(item.SelectToken("confidence")),
                    ReadFiniteDouble(item.SelectToken("bestIou")),
                    ReadNormalizedBox(item.SelectToken("predictionBox")),
                    ReadNormalizedBox(item.SelectToken("groundTruthBox"))))
                .Where(item => item.ClassId >= 0)
                .ToArray()
                ?? Array.Empty<WpfModelBenchmarkGroundTruthExample>();
            IReadOnlyList<WpfModelBenchmarkThresholdReview> thresholdSweep = schemaVersion >= 2
                ? token.SelectToken("thresholdSweep")?.Children<JObject>()
                    .Select(ReadGroundTruthThresholdReview)
                    .Where(item => item != null)
                    .OrderByDescending(item => item.Confidence)
                    .ToArray()
                    ?? Array.Empty<WpfModelBenchmarkThresholdReview>()
                : Array.Empty<WpfModelBenchmarkThresholdReview>();

            return new WpfModelBenchmarkGroundTruthReview(
                ReadFiniteDouble(token.SelectToken("confidence")),
                ReadFiniteDouble(token.SelectToken("predictionNmsIouThreshold")),
                ReadFiniteDouble(token.SelectToken("iouThreshold")),
                ReadNonNegativeInt(token.SelectToken("imageCount")) ?? 0,
                ReadNonNegativeInt(token.SelectToken("truePositiveCount")) ?? 0,
                ReadNonNegativeInt(token.SelectToken("falsePositiveCount")) ?? 0,
                ReadNonNegativeInt(token.SelectToken("falseNegativeCount")) ?? 0,
                perClass,
                examples,
                schemaVersion,
                schema,
                geometryCoordinateSystem,
                thresholdSweep);
        }

        private static WpfModelBenchmarkThresholdReview ReadGroundTruthThresholdReview(JObject token)
        {
            double? confidence = ReadUnitIntervalDouble(token?.SelectToken("confidence"));
            int? groundTruthCount = ReadNonNegativeInt(token?.SelectToken("groundTruthCount"));
            int? predictionCount = ReadNonNegativeInt(token?.SelectToken("predictionCount"));
            int? truePositiveCount = ReadNonNegativeInt(token?.SelectToken("truePositiveCount"));
            int? falsePositiveCount = ReadNonNegativeInt(token?.SelectToken("falsePositiveCount"));
            int? falseNegativeCount = ReadNonNegativeInt(token?.SelectToken("falseNegativeCount"));
            if (!confidence.HasValue
                || !groundTruthCount.HasValue
                || !predictionCount.HasValue
                || !truePositiveCount.HasValue
                || !falsePositiveCount.HasValue
                || !falseNegativeCount.HasValue)
            {
                return null;
            }

            return new WpfModelBenchmarkThresholdReview(
                confidence.Value,
                groundTruthCount.Value,
                predictionCount.Value,
                truePositiveCount.Value,
                falsePositiveCount.Value,
                falseNegativeCount.Value,
                ReadUnitIntervalDouble(token.SelectToken("precision")),
                ReadUnitIntervalDouble(token.SelectToken("recall")),
                ReadUnitIntervalDouble(token.SelectToken("f1")));
        }

        private static WpfModelBenchmarkNormalizedBox ReadNormalizedBox(JToken token)
        {
            int? classId = ReadNonNegativeInt(token?.SelectToken("classId"));
            double? xMin = ReadUnitIntervalDouble(token?.SelectToken("xMin"));
            double? yMin = ReadUnitIntervalDouble(token?.SelectToken("yMin"));
            double? xMax = ReadUnitIntervalDouble(token?.SelectToken("xMax"));
            double? yMax = ReadUnitIntervalDouble(token?.SelectToken("yMax"));
            if (!classId.HasValue
                || !xMin.HasValue
                || !yMin.HasValue
                || !xMax.HasValue
                || !yMax.HasValue
                || xMax.Value <= xMin.Value
                || yMax.Value <= yMin.Value)
            {
                return null;
            }

            return new WpfModelBenchmarkNormalizedBox(
                classId.Value,
                xMin.Value,
                yMin.Value,
                xMax.Value,
                yMax.Value,
                ReadUnitIntervalDouble(token.SelectToken("confidence")));
        }

        private static int? ReadNonNegativeInt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null
                || !int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                || value < 0)
            {
                return null;
            }

            return value;
        }

        private static double? ReadFiniteDouble(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null
                || !double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value)
                || double.IsInfinity(value))
            {
                return null;
            }

            return value;
        }

        private static double? ReadUnitIntervalDouble(JToken token)
        {
            double? value = ReadFiniteDouble(token);
            return value.HasValue && value.Value >= 0D && value.Value <= 1D ? value : null;
        }

        private static string BuildModelName(string weightsPath, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(weightsPath))
            {
                try
                {
                    DirectoryInfo weightsDirectory = Directory.GetParent(weightsPath);
                    if (weightsDirectory != null
                        && string.Equals(weightsDirectory.Name, "weights", StringComparison.OrdinalIgnoreCase)
                        && weightsDirectory.Parent != null)
                    {
                        return weightsDirectory.Parent.Name;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(weightsPath);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        return fileName;
                    }
                }
                catch
                {
                }
            }

            return string.IsNullOrWhiteSpace(fallback) ? "Model" : fallback.Trim();
        }

        private static string InferClassificationRuntime(string weightsPath, string modelName)
        {
            string source = (weightsPath + " " + modelName).ToLowerInvariant();
            if (source.Contains("yolov8", StringComparison.Ordinal))
            {
                return "Ultralytics YOLOv8";
            }

            if (source.Contains("yolov5", StringComparison.Ordinal))
            {
                return "YOLOv5";
            }

            return "Classification runtime";
        }

        private static string NormalizeTaskKey(string task)
        {
            string normalized = (task ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "detect" => "object-detection",
                "detection" => "object-detection",
                "segment" => "segmentation",
                "seg" => "segmentation",
                "classify" => "classification",
                _ => string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized
            };
        }

        private static string FormatTaskText(string taskKey)
        {
            return taskKey switch
            {
                "object-detection" => "\uAC1D\uCCB4 \uD0D0\uC9C0",
                "segmentation" => "\uC138\uADF8\uBA58\uD14C\uC774\uC158",
                "classification" => "\uBD84\uB958",
                "anomaly-classification" => "\uC774\uC0C1 \uBD84\uB958",
                _ => "\uAE30\uD0C0"
            };
        }

        private static string FormatDecisionText(string decision)
        {
            return (decision ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "adopt" => "\uCC44\uD0DD \uADFC\uAC70",
                "promote" => "\uAD50\uCCB4 \uCD94\uCC9C",
                "hold" => "\uBCF4\uB958",
                "review" => "\uAC80\uD1A0 \uD544\uC694",
                "benchmark" => "\uC131\uB2A5 \uBD84\uC11D",
                _ => "\uD310\uC815 \uC5C6\uC74C"
            };
        }

        private static DateTimeOffset ReadDateTimeOffset(string text, DateTime fallbackUtc)
        {
            return DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset parsed)
                ? parsed
                : new DateTimeOffset(DateTime.SpecifyKind(fallbackUtc, DateTimeKind.Utc));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }
    }

    public sealed class WpfModelBenchmarkRun
    {
        public WpfModelBenchmarkRun(
            string id,
            string sourcePath,
            string sourceRole,
            string sourceTypeText,
            DateTimeOffset createdAt,
            string displayName,
            string modelName,
            string runtimeName,
            string taskKey,
            string taskText,
            string weightsPath,
            string weightsSha256,
            string evaluationDataPath,
            string evidenceFingerprintSha256,
            string split,
            int evidenceCount,
            int imageSize,
            int batchSize,
            double? confidence,
            string timingSource,
            int timingRepeatCount,
            double? taktMs,
            double? taktMinMs,
            double? taktMaxMs,
            string decisionText,
            IReadOnlyList<WpfModelBenchmarkMetric> metrics,
            IReadOnlyList<WpfModelBenchmarkClassMetric> classMetrics,
            WpfModelBenchmarkGroundTruthReview groundTruthReview)
        {
            Id = id ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            SourceRole = sourceRole ?? string.Empty;
            SourceTypeText = sourceTypeText ?? string.Empty;
            CreatedAt = createdAt;
            DisplayName = displayName ?? string.Empty;
            ModelName = modelName ?? string.Empty;
            RuntimeName = runtimeName ?? string.Empty;
            TaskKey = taskKey ?? string.Empty;
            TaskText = taskText ?? string.Empty;
            WeightsPath = weightsPath ?? string.Empty;
            WeightsSha256 = NormalizeSha256(weightsSha256);
            EvaluationDataPath = evaluationDataPath ?? string.Empty;
            EvidenceFingerprintSha256 = NormalizeSha256(evidenceFingerprintSha256);
            Split = split ?? string.Empty;
            EvidenceCount = Math.Max(0, evidenceCount);
            ImageSize = Math.Max(0, imageSize);
            BatchSize = Math.Max(0, batchSize);
            Confidence = confidence;
            TimingSource = timingSource ?? string.Empty;
            TimingRepeatCount = Math.Max(0, timingRepeatCount);
            TaktMs = taktMs;
            TaktMinMs = taktMinMs;
            TaktMaxMs = taktMaxMs;
            DecisionText = decisionText ?? string.Empty;
            Metrics = metrics ?? Array.Empty<WpfModelBenchmarkMetric>();
            ClassMetrics = classMetrics ?? Array.Empty<WpfModelBenchmarkClassMetric>();
            GroundTruthReview = groundTruthReview;
        }

        public string Id { get; }
        public string SourcePath { get; }
        public string SourceRole { get; }
        public string SourceTypeText { get; }
        public DateTimeOffset CreatedAt { get; }
        public string DisplayName { get; }
        public string ModelName { get; }
        public string RuntimeName { get; }
        public string TaskKey { get; }
        public string TaskText { get; }
        public string WeightsPath { get; }
        public string WeightsSha256 { get; }
        public string EvaluationDataPath { get; }
        public string EvidenceFingerprintSha256 { get; }
        public string Split { get; }
        public int EvidenceCount { get; }
        public int ImageSize { get; }
        public int BatchSize { get; }
        public double? Confidence { get; }
        public string TimingSource { get; }
        public int TimingRepeatCount { get; }
        public double? TaktMs { get; }
        public double? TaktMinMs { get; }
        public double? TaktMaxMs { get; }
        public string DecisionText { get; }
        public IReadOnlyList<WpfModelBenchmarkMetric> Metrics { get; }
        public IReadOnlyList<WpfModelBenchmarkClassMetric> ClassMetrics { get; }
        public WpfModelBenchmarkGroundTruthReview GroundTruthReview { get; }

        public string QualityComparisonKey => string.Join(
            "|",
            TaskKey,
            string.IsNullOrWhiteSpace(EvidenceFingerprintSha256)
                ? "path:" + NormalizePath(EvaluationDataPath)
                : "sha256:" + EvidenceFingerprintSha256.Trim().ToLowerInvariant(),
            Split.Trim().ToLowerInvariant(),
            EvidenceCount.ToString(CultureInfo.InvariantCulture));

        public string TimingComparisonKey => string.Join(
            "|",
            TaskKey,
            ImageSize.ToString(CultureInfo.InvariantCulture),
            BatchSize.ToString(CultureInfo.InvariantCulture),
            TimingSource.Trim().ToLowerInvariant(),
            TimingRepeatCount.ToString(CultureInfo.InvariantCulture));

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
            }
            catch
            {
                return path.Trim().ToLowerInvariant();
            }
        }

        private static string NormalizeSha256(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
                ? normalized.ToLowerInvariant()
                : string.Empty;
        }
    }

    public sealed class WpfModelBenchmarkMetric
    {
        public WpfModelBenchmarkMetric(
            string key,
            string displayName,
            double value,
            int order,
            bool isPercent,
            bool supportsDelta)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Value = value;
            Order = order;
            IsPercent = isPercent;
            SupportsDelta = supportsDelta;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public double Value { get; }
        public int Order { get; }
        public bool IsPercent { get; }
        public bool SupportsDelta { get; }

        public string FormatValue()
        {
            return IsPercent
                ? Value.ToString("P1", CultureInfo.CurrentCulture)
                : Value.ToString("0.##", CultureInfo.CurrentCulture);
        }
    }

    public sealed class WpfModelBenchmarkClassMetric
    {
        public WpfModelBenchmarkClassMetric(
            int classId,
            string className,
            int? imageCount,
            int? instanceCount,
            double? precision,
            double? recall,
            double? map50,
            double? map5095)
        {
            ClassId = classId;
            ClassName = className ?? string.Empty;
            ImageCount = imageCount;
            InstanceCount = instanceCount;
            Precision = precision;
            Recall = recall;
            Map50 = map50;
            Map5095 = map5095;
        }

        public int ClassId { get; }
        public string ClassName { get; }
        public int? ImageCount { get; }
        public int? InstanceCount { get; }
        public double? Precision { get; }
        public double? Recall { get; }
        public double? Map50 { get; }
        public double? Map5095 { get; }
    }

    public sealed class WpfModelBenchmarkGroundTruthReview
    {
        public WpfModelBenchmarkGroundTruthReview(
            double? confidence,
            double? predictionNmsIouThreshold,
            double? iouThreshold,
            int imageCount,
            int truePositiveCount,
            int falsePositiveCount,
            int falseNegativeCount,
            IReadOnlyList<WpfModelBenchmarkGroundTruthClassReview> perClass,
            IReadOnlyList<WpfModelBenchmarkGroundTruthExample> examples,
            int schemaVersion = 1,
            string schema = "",
            string geometryCoordinateSystem = "",
            IReadOnlyList<WpfModelBenchmarkThresholdReview> thresholdSweep = null)
        {
            Confidence = confidence;
            PredictionNmsIouThreshold = predictionNmsIouThreshold;
            IouThreshold = iouThreshold;
            ImageCount = Math.Max(0, imageCount);
            TruePositiveCount = Math.Max(0, truePositiveCount);
            FalsePositiveCount = Math.Max(0, falsePositiveCount);
            FalseNegativeCount = Math.Max(0, falseNegativeCount);
            PerClass = perClass ?? Array.Empty<WpfModelBenchmarkGroundTruthClassReview>();
            Examples = examples ?? Array.Empty<WpfModelBenchmarkGroundTruthExample>();
            SchemaVersion = Math.Max(1, schemaVersion);
            Schema = schema ?? string.Empty;
            GeometryCoordinateSystem = geometryCoordinateSystem ?? string.Empty;
            ThresholdSweep = thresholdSweep ?? Array.Empty<WpfModelBenchmarkThresholdReview>();
        }

        public double? Confidence { get; }
        public double? PredictionNmsIouThreshold { get; }
        public double? IouThreshold { get; }
        public int ImageCount { get; }
        public int TruePositiveCount { get; }
        public int FalsePositiveCount { get; }
        public int FalseNegativeCount { get; }
        public IReadOnlyList<WpfModelBenchmarkGroundTruthClassReview> PerClass { get; }
        public IReadOnlyList<WpfModelBenchmarkGroundTruthExample> Examples { get; }
        public int SchemaVersion { get; }
        public string Schema { get; }
        public string GeometryCoordinateSystem { get; }
        public IReadOnlyList<WpfModelBenchmarkThresholdReview> ThresholdSweep { get; }
    }

    public sealed class WpfModelBenchmarkGroundTruthClassReview
    {
        public WpfModelBenchmarkGroundTruthClassReview(
            int classId,
            string className,
            int groundTruthCount,
            int predictionCount,
            int truePositiveCount,
            int falsePositiveCount,
            int falseNegativeCount)
        {
            ClassId = classId;
            ClassName = className ?? string.Empty;
            GroundTruthCount = Math.Max(0, groundTruthCount);
            PredictionCount = Math.Max(0, predictionCount);
            TruePositiveCount = Math.Max(0, truePositiveCount);
            FalsePositiveCount = Math.Max(0, falsePositiveCount);
            FalseNegativeCount = Math.Max(0, falseNegativeCount);
        }

        public int ClassId { get; }
        public string ClassName { get; }
        public int GroundTruthCount { get; }
        public int PredictionCount { get; }
        public int TruePositiveCount { get; }
        public int FalsePositiveCount { get; }
        public int FalseNegativeCount { get; }
    }

    public sealed class WpfModelBenchmarkGroundTruthExample
    {
        public WpfModelBenchmarkGroundTruthExample(
            string imagePath,
            string imageName,
            string errorType,
            int classId,
            string className,
            double? confidence,
            double? bestIou,
            WpfModelBenchmarkNormalizedBox predictionBox = null,
            WpfModelBenchmarkNormalizedBox groundTruthBox = null)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            ErrorType = errorType ?? string.Empty;
            ClassId = classId;
            ClassName = className ?? string.Empty;
            Confidence = confidence;
            BestIou = bestIou;
            PredictionBox = predictionBox;
            GroundTruthBox = groundTruthBox;
        }

        public string ImagePath { get; }
        public string ImageName { get; }
        public string ErrorType { get; }
        public int ClassId { get; }
        public string ClassName { get; }
        public double? Confidence { get; }
        public double? BestIou { get; }
        public WpfModelBenchmarkNormalizedBox PredictionBox { get; }
        public WpfModelBenchmarkNormalizedBox GroundTruthBox { get; }
    }

    public sealed class WpfModelBenchmarkThresholdReview
    {
        public WpfModelBenchmarkThresholdReview(
            double confidence,
            int groundTruthCount,
            int predictionCount,
            int truePositiveCount,
            int falsePositiveCount,
            int falseNegativeCount,
            double? precision,
            double? recall,
            double? f1)
        {
            Confidence = confidence;
            GroundTruthCount = Math.Max(0, groundTruthCount);
            PredictionCount = Math.Max(0, predictionCount);
            TruePositiveCount = Math.Max(0, truePositiveCount);
            FalsePositiveCount = Math.Max(0, falsePositiveCount);
            FalseNegativeCount = Math.Max(0, falseNegativeCount);
            Precision = precision;
            Recall = recall;
            F1 = f1;
        }

        public double Confidence { get; }
        public int GroundTruthCount { get; }
        public int PredictionCount { get; }
        public int TruePositiveCount { get; }
        public int FalsePositiveCount { get; }
        public int FalseNegativeCount { get; }
        public double? Precision { get; }
        public double? Recall { get; }
        public double? F1 { get; }
    }

    public sealed class WpfModelBenchmarkNormalizedBox
    {
        public WpfModelBenchmarkNormalizedBox(
            int classId,
            double xMin,
            double yMin,
            double xMax,
            double yMax,
            double? confidence)
        {
            ClassId = classId;
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
            Confidence = confidence;
        }

        public int ClassId { get; }
        public double XMin { get; }
        public double YMin { get; }
        public double XMax { get; }
        public double YMax { get; }
        public double? Confidence { get; }
    }
}
