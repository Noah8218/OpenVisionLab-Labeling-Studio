using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private const double MobileSamUsableIou = 0.50D;
    private const double MobileSamEditableIou = 0.25D;

    private static int RunRealMobileSamUsabilityMatrix(string[] args)
    {
        try
        {
            string datasetRoot = Path.GetFullPath(GetArgumentValue(args, "--dataset-root", string.Empty));
            AssertTrue(Directory.Exists(datasetRoot), "--dataset-root must point to the hexagon defect package");
            string labelsPath = Path.Combine(datasetRoot, "labels.json");
            string datasetSummaryPath = Path.Combine(datasetRoot, "dataset_summary.json");
            AssertTrue(File.Exists(labelsPath), "dataset labels.json was not found");
            AssertTrue(File.Exists(datasetSummaryPath), "dataset_summary.json was not found");

            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(FindRepositoryRoot(), "artifacts", "mobile-sam-usability-matrix")));
            string runRoot = Path.Combine(artifactRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(runRoot);

            ExternalYoloSourceTreeSnapshot sourceBefore = CaptureExternalYoloSourceTree(datasetRoot);
            JObject datasetSummary = JObject.Parse(File.ReadAllText(datasetSummaryPath));
            AssertTrue(datasetSummary.Value<bool?>("synthetic") == true, "matrix source must declare synthetic=true");
            string[] classNames = (datasetSummary["defect_classes"] as JArray)?
                .OfType<JObject>()
                .OrderBy(item => item.Value<int?>("id") ?? int.MaxValue)
                .Select(item => item.Value<string>("name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray() ?? Array.Empty<string>();
            AssertEqual(8, classNames.Length);

            JArray labels = JArray.Parse(File.ReadAllText(labelsPath));
            IReadOnlyList<MobileSamMatrixSample> samples = SelectMobileSamMatrixSamples(datasetRoot, labels, classNames);
            AssertEqual(24, samples.Count);
            AssertEqual(24, samples.Select(sample => sample.ImagePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            JArray manifestSamples = new JArray(samples.Select(sample => new JObject
            {
                ["className"] = sample.ClassName,
                ["sourceSplit"] = sample.SourceSplit,
                ["imageRelativePath"] = sample.ImageRelativePath,
                ["maskRelativePath"] = sample.MaskRelativePath,
                ["imageSha256"] = ComputeFileSha256(sample.ImagePath),
                ["maskSha256"] = ComputeFileSha256(sample.MaskPath),
                ["promptBox"] = JArray.FromObject(new[]
                {
                    sample.PromptBounds.X,
                    sample.PromptBounds.Y,
                    sample.PromptBounds.Width,
                    sample.PromptBounds.Height
                })
            }));
            string selectionFingerprint = ComputeTextSha256(manifestSamples.ToString(Formatting.None));
            var selectionManifest = new JObject
            {
                ["version"] = 1,
                ["evidenceOrigin"] = "synthetic",
                ["selectionPolicy"] = "first ordinal single-defect image per class from each train/val/test split",
                ["classCount"] = classNames.Length,
                ["samplesPerClass"] = 3,
                ["sampleCount"] = samples.Count,
                ["selectionFingerprintSha256"] = selectionFingerprint,
                ["datasetLabelsSha256"] = ComputeFileSha256(labelsPath),
                ["sourceTreeFileCountBefore"] = sourceBefore.FileCount,
                ["sourceTreeSha256Before"] = sourceBefore.TreeSha256,
                ["samples"] = manifestSamples
            };
            string selectionManifestPath = Path.Combine(runRoot, "selection-manifest.json");
            File.WriteAllText(selectionManifestPath, selectionManifest.ToString(Formatting.Indented));

            var service = new WpfMobileSamBoxPromptService();
            var settings = new PythonModelSettings();
            settings.EnsureDefaults();
            var resultRows = new List<JObject>(samples.Count);
            string weightsPath = string.Empty;
            for (int index = 0; index < samples.Count; index++)
            {
                MobileSamMatrixSample sample = samples[index];
                WpfMobileSamBoxPromptRequest request = service.BuildRequest(
                    settings,
                    sample.ImagePath,
                    sample.PromptBounds,
                    Array.IndexOf(classNames, sample.ClassName),
                    sample.ClassName);
                AssertTrue(request.IsValid, string.Join(" ", request.Errors));
                weightsPath = request.WeightsPath;
                WpfMobileSamBoxPromptResult result = service.RunAsync(request).GetAwaiter().GetResult();
                var row = new JObject
                {
                    ["className"] = sample.ClassName,
                    ["sourceSplit"] = sample.SourceSplit,
                    ["imageRelativePath"] = sample.ImageRelativePath,
                    ["maskRelativePath"] = sample.MaskRelativePath,
                    ["promptBox"] = JArray.FromObject(new[]
                    {
                        sample.PromptBounds.X,
                        sample.PromptBounds.Y,
                        sample.PromptBounds.Width,
                        sample.PromptBounds.Height
                    }),
                    ["workerSucceeded"] = result.Succeeded,
                    ["elapsedMs"] = result.ElapsedMilliseconds,
                    ["workerMaskArea"] = result.MaskArea,
                    ["runtime"] = result.RuntimeSummary,
                    ["weightsSha256"] = result.WeightsSha256
                };
                if (result.Succeeded && result.Candidate?.PolygonPoints?.Count >= 3)
                {
                    string predictedMaskRelativePath = Path.Combine(
                        "predicted-masks",
                        sample.SourceSplit,
                        sample.ClassName,
                        Path.GetFileNameWithoutExtension(sample.ImagePath) + ".png");
                    string predictedMaskPath = Path.Combine(runRoot, predictedMaskRelativePath);
                    MobileSamMaskMetric metric = ComputeMobileSamMaskMetric(
                        sample.MaskPath,
                        result.Candidate.PolygonPoints,
                        predictedMaskPath);
                    string disposition = ClassifyMobileSamCandidate(metric.IoU);
                    row["polygonPointCount"] = result.Candidate.PolygonPoints.Count;
                    row["predictedMaskRelativePath"] = predictedMaskRelativePath.Replace('\\', '/');
                    row["predictedMaskSha256"] = ComputeFileSha256(predictedMaskPath);
                    row["groundTruthPixels"] = metric.GroundTruthPixels;
                    row["predictedPixels"] = metric.PredictedPixels;
                    row["intersectionPixels"] = metric.IntersectionPixels;
                    row["unionPixels"] = metric.UnionPixels;
                    row["iou"] = metric.IoU;
                    row["dice"] = metric.Dice;
                    row["predictedToGroundTruthAreaRatio"] = metric.AreaRatio;
                    row["disposition"] = disposition;
                    row["failureType"] = ClassifyMobileSamFailure(metric, disposition);
                    row["polygon"] = JArray.FromObject(result.Candidate.PolygonPoints);
                }
                else
                {
                    row["disposition"] = "skip";
                    row["failureType"] = "worker-failure";
                    row["errorCode"] = result.ErrorCode;
                    row["error"] = result.Error;
                }

                resultRows.Add(row);
                Console.WriteLine(FormattableString.Invariant(
                    $"MOBILE_SAM_MATRIX_SAMPLE={index + 1}/{samples.Count} CLASS={sample.ClassName} SPLIT={sample.SourceSplit} IOU={(row.Value<double?>("iou") ?? 0D):F6} DISPOSITION={row.Value<string>("disposition")}"));
            }

            ExternalYoloSourceTreeSnapshot sourceAfter = CaptureExternalYoloSourceTree(datasetRoot);
            AssertEqual(sourceBefore.FileCount, sourceAfter.FileCount);
            AssertEqual(sourceBefore.TreeSha256, sourceAfter.TreeSha256);
            AssertTrue(resultRows.All(row => row.Value<bool>("workerSucceeded")), "all 24 fixed samples should complete MobileSAM inference");
            AssertEqual(1, resultRows.Select(row => row.Value<string>("runtime")).Distinct(StringComparer.Ordinal).Count());
            AssertEqual(1, resultRows.Select(row => row.Value<string>("weightsSha256")).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            IReadOnlyList<JObject> classRows = BuildMobileSamClassRows(classNames, resultRows);
            int usableCount = resultRows.Count(row => string.Equals(row.Value<string>("disposition"), "usable", StringComparison.Ordinal));
            int editableCount = resultRows.Count(row => string.Equals(row.Value<string>("disposition"), "edit", StringComparison.Ordinal));
            int skipCount = resultRows.Count - usableCount - editableCount;
            double overallPassRate = usableCount / (double)resultRows.Count;
            bool boxOnlyGatePassed = overallPassRate >= 0.75D
                && classRows.All(row => (row.Value<double?>("medianIou") ?? 0D) >= MobileSamUsableIou);
            string runtime = resultRows[0].Value<string>("runtime") ?? string.Empty;
            string weightsSha256 = resultRows[0].Value<string>("weightsSha256") ?? string.Empty;
            var summary = new JObject
            {
                ["status"] = "Complete",
                ["scope"] = "MobileSAM fixed 8-class synthetic box-prompt usability matrix",
                ["evidenceOrigin"] = "synthetic",
                ["fieldValidation"] = "Not evaluated",
                ["productionAccuracyClaimed"] = false,
                ["selectionFingerprintSha256"] = selectionFingerprint,
                ["sourceTreeFileCountBefore"] = sourceBefore.FileCount,
                ["sourceTreeFileCountAfter"] = sourceAfter.FileCount,
                ["sourceTreeSha256Before"] = sourceBefore.TreeSha256,
                ["sourceTreeSha256After"] = sourceAfter.TreeSha256,
                ["weightsPath"] = weightsPath,
                ["weightsSha256"] = weightsSha256,
                ["runtime"] = runtime,
                ["usableIouThreshold"] = MobileSamUsableIou,
                ["editableIouThreshold"] = MobileSamEditableIou,
                ["sampleCount"] = resultRows.Count,
                ["usableCount"] = usableCount,
                ["editableCount"] = editableCount,
                ["skipCount"] = skipCount,
                ["overallUsableRate"] = overallPassRate,
                ["medianIou"] = Median(resultRows.Select(row => row.Value<double?>("iou") ?? 0D)),
                ["medianElapsedMs"] = Median(resultRows.Select(row => row.Value<double?>("elapsedMs") ?? 0D)),
                ["p95ElapsedMs"] = Percentile(resultRows.Select(row => row.Value<double?>("elapsedMs") ?? 0D), 0.95D),
                ["boxOnlyGatePassed"] = boxOnlyGatePassed,
                ["classes"] = new JArray(classRows),
                ["boundary"] = "Synthetic labeling-assist evidence only; no production accuracy or automatic model-adoption claim."
            };
            string resultsPath = Path.Combine(runRoot, "sample-results.jsonl");
            File.WriteAllLines(resultsPath, resultRows.Select(row => row.ToString(Formatting.None)));
            string summaryJsonPath = Path.Combine(runRoot, "summary.json");
            File.WriteAllText(summaryJsonPath, summary.ToString(Formatting.Indented));
            string summaryMarkdownPath = Path.Combine(runRoot, "summary.md");
            File.WriteAllText(summaryMarkdownPath, BuildMobileSamMatrixMarkdown(summary, classRows));

            Console.WriteLine("MOBILE_SAM_MATRIX_ROOT=" + runRoot);
            Console.WriteLine("MOBILE_SAM_MATRIX_SUMMARY=" + summaryJsonPath);
            Console.WriteLine("MOBILE_SAM_MATRIX_REPORT=" + summaryMarkdownPath);
            Console.WriteLine(FormattableString.Invariant(
                $"MOBILE_SAM_MATRIX_RESULT=usable:{usableCount} edit:{editableCount} skip:{skipCount} median_iou:{summary.Value<double>("medianIou"):F6} usable_rate:{overallPassRate:F6} box_only_gate:{boxOnlyGatePassed}"));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("MOBILE_SAM_MATRIX_FAILED=" + error);
            return 1;
        }
    }

    private static IReadOnlyList<MobileSamMatrixSample> SelectMobileSamMatrixSamples(
        string datasetRoot,
        JArray labels,
        IReadOnlyList<string> classNames)
    {
        var samples = new List<MobileSamMatrixSample>();
        foreach (string sourceSplit in new[] { "train", "val", "test" })
        {
            foreach (string className in classNames)
            {
                JObject row = labels
                    .OfType<JObject>()
                    .Where(item => string.Equals(item.Value<string>("status"), "NG", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.Value<string>("defect_count"), "1", StringComparison.Ordinal)
                        && string.Equals(item.Value<string>("defect_names"), className, StringComparison.Ordinal)
                        && string.Equals(item.Value<string>("split"), sourceSplit, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.Value<string>("image_path"), StringComparer.Ordinal)
                    .FirstOrDefault();
                AssertTrue(row != null, $"no single-defect {sourceSplit} sample for class {className}");
                JArray boxes = JArray.Parse(row.Value<string>("defect_bboxes_xyxy_json") ?? "[]");
                AssertEqual(1, boxes.Count);
                JObject box = (JObject)boxes[0];
                int left = box.Value<int>("x_min");
                int top = box.Value<int>("y_min");
                int right = box.Value<int>("x_max");
                int bottom = box.Value<int>("y_max");
                string imageRelativePath = row.Value<string>("image_path") ?? string.Empty;
                string maskRelativePath = row.Value<string>("defect_binary_mask_path") ?? string.Empty;
                string imagePath = Path.Combine(datasetRoot, imageRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string maskPath = Path.Combine(datasetRoot, maskRelativePath.Replace('/', Path.DirectorySeparatorChar));
                AssertTrue(File.Exists(imagePath), "matrix source image was not found: " + imagePath);
                AssertTrue(File.Exists(maskPath), "matrix ground-truth mask was not found: " + maskPath);
                samples.Add(new MobileSamMatrixSample(
                    className,
                    sourceSplit,
                    imageRelativePath,
                    maskRelativePath,
                    imagePath,
                    maskPath,
                    new Rectangle(left, top, right - left, bottom - top)));
            }
        }

        return samples;
    }

    private static MobileSamMaskMetric ComputeMobileSamMaskMetric(
        string groundTruthMaskPath,
        IReadOnlyList<DetectionPolygonPoint> polygon,
        string predictedMaskPath)
    {
        using var groundTruth = new Bitmap(groundTruthMaskPath);
        Directory.CreateDirectory(Path.GetDirectoryName(predictedMaskPath) ?? string.Empty);
        using var predicted = new Bitmap(groundTruth.Width, groundTruth.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (Graphics graphics = Graphics.FromImage(predicted))
        using (var brush = new SolidBrush(Color.White))
        {
            graphics.Clear(Color.Black);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            graphics.FillPolygon(brush, polygon.Select(point => new PointF(point.X, point.Y)).ToArray());
        }
        predicted.Save(predictedMaskPath, System.Drawing.Imaging.ImageFormat.Png);

        int groundTruthPixels = 0;
        int predictedPixels = 0;
        int intersectionPixels = 0;
        for (int y = 0; y < groundTruth.Height; y++)
        {
            for (int x = 0; x < groundTruth.Width; x++)
            {
                Color truthColor = groundTruth.GetPixel(x, y);
                bool truth = truthColor.R > 0 || truthColor.G > 0 || truthColor.B > 0;
                bool prediction = predicted.GetPixel(x, y).R > 0;
                if (truth)
                {
                    groundTruthPixels++;
                }
                if (prediction)
                {
                    predictedPixels++;
                }
                if (truth && prediction)
                {
                    intersectionPixels++;
                }
            }
        }

        int unionPixels = groundTruthPixels + predictedPixels - intersectionPixels;
        double iou = unionPixels == 0 ? 1D : intersectionPixels / (double)unionPixels;
        double diceDenominator = groundTruthPixels + predictedPixels;
        double dice = diceDenominator == 0 ? 1D : 2D * intersectionPixels / diceDenominator;
        double areaRatio = groundTruthPixels == 0 ? 0D : predictedPixels / (double)groundTruthPixels;
        return new MobileSamMaskMetric(
            groundTruthPixels,
            predictedPixels,
            intersectionPixels,
            unionPixels,
            iou,
            dice,
            areaRatio);
    }

    private static string ClassifyMobileSamCandidate(double iou)
        => iou >= MobileSamUsableIou ? "usable" : iou >= MobileSamEditableIou ? "edit" : "skip";

    private static string ClassifyMobileSamFailure(MobileSamMaskMetric metric, string disposition)
    {
        if (string.Equals(disposition, "usable", StringComparison.Ordinal))
        {
            return "none";
        }
        if (metric.AreaRatio > 2D)
        {
            return "over-segmentation";
        }
        if (metric.AreaRatio < 0.5D)
        {
            return "under-segmentation";
        }
        return "boundary-mismatch";
    }

    private static IReadOnlyList<JObject> BuildMobileSamClassRows(
        IReadOnlyList<string> classNames,
        IReadOnlyList<JObject> results)
        => classNames.Select(className =>
        {
            JObject[] rows = results
                .Where(row => string.Equals(row.Value<string>("className"), className, StringComparison.Ordinal))
                .ToArray();
            return new JObject
            {
                ["className"] = className,
                ["sampleCount"] = rows.Length,
                ["usableCount"] = rows.Count(row => string.Equals(row.Value<string>("disposition"), "usable", StringComparison.Ordinal)),
                ["editableCount"] = rows.Count(row => string.Equals(row.Value<string>("disposition"), "edit", StringComparison.Ordinal)),
                ["skipCount"] = rows.Count(row => string.Equals(row.Value<string>("disposition"), "skip", StringComparison.Ordinal)),
                ["medianIou"] = Median(rows.Select(row => row.Value<double?>("iou") ?? 0D)),
                ["medianDice"] = Median(rows.Select(row => row.Value<double?>("dice") ?? 0D)),
                ["medianElapsedMs"] = Median(rows.Select(row => row.Value<double?>("elapsedMs") ?? 0D)),
                ["dominantFailure"] = rows
                    .Select(row => row.Value<string>("failureType") ?? "worker-failure")
                    .Where(value => !string.Equals(value, "none", StringComparison.Ordinal))
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => group.Key)
                    .FirstOrDefault() ?? "none"
            };
        }).ToArray();

    private static string BuildMobileSamMatrixMarkdown(JObject summary, IReadOnlyList<JObject> classRows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# MobileSAM 8-Class Synthetic Usability Matrix");
        builder.AppendLine();
        builder.AppendLine("- Status: Complete");
        builder.AppendLine("- Evidence origin: synthetic");
        builder.AppendLine("- Field validation: Not evaluated");
        builder.AppendLine($"- Selection fingerprint: `{summary.Value<string>("selectionFingerprintSha256")}`");
        builder.AppendLine($"- Source tree SHA-256 before/after: `{summary.Value<string>("sourceTreeSha256Before")}` / `{summary.Value<string>("sourceTreeSha256After")}`");
        builder.AppendLine($"- Runtime: {summary.Value<string>("runtime")}");
        builder.AppendLine($"- Weight SHA-256: `{summary.Value<string>("weightsSha256")}`");
        builder.AppendLine();
        builder.AppendLine("| Class | Usable / Edit / Skip | Median IoU | Median Dice | Median ms | Dominant failure |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (JObject row in classRows)
        {
            builder.AppendLine(FormattableString.Invariant(
                $"| {row.Value<string>("className")} | {row.Value<int>("usableCount")} / {row.Value<int>("editableCount")} / {row.Value<int>("skipCount")} | {row.Value<double>("medianIou"):F4} | {row.Value<double>("medianDice"):F4} | {row.Value<double>("medianElapsedMs"):F1} | {row.Value<string>("dominantFailure")} |"));
        }
        builder.AppendLine();
        builder.AppendLine(FormattableString.Invariant(
            $"Overall: usable {summary.Value<int>("usableCount")}, edit {summary.Value<int>("editableCount")}, skip {summary.Value<int>("skipCount")}, median IoU {summary.Value<double>("medianIou"):F4}, usable rate {summary.Value<double>("overallUsableRate"):P1}, box-only gate {summary.Value<bool>("boxOnlyGatePassed")}."));
        builder.AppendLine();
        builder.AppendLine("Boundary: synthetic labeling-assist evidence only; no production accuracy or automatic model-adoption claim.");
        return builder.ToString();
    }

    private static double Median(IEnumerable<double> values)
        => Percentile(values, 0.50D);

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return 0D;
        }
        int index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static string ComputeTextSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));

    private static void TestMobileSamUsabilityMetric()
    {
        string root = CreateTempRoot();
        try
        {
            string truthPath = Path.Combine(root, "truth.png");
            string predictionPath = Path.Combine(root, "prediction.png");
            using (var truth = new Bitmap(64, 64))
            using (Graphics graphics = Graphics.FromImage(truth))
            {
                graphics.Clear(Color.Black);
                graphics.FillRectangle(Brushes.White, new Rectangle(10, 10, 21, 21));
                truth.Save(truthPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            IReadOnlyList<DetectionPolygonPoint> polygon = new[]
            {
                new DetectionPolygonPoint { X = 10, Y = 10 },
                new DetectionPolygonPoint { X = 31, Y = 10 },
                new DetectionPolygonPoint { X = 31, Y = 31 },
                new DetectionPolygonPoint { X = 10, Y = 31 }
            };
            MobileSamMaskMetric metric = ComputeMobileSamMaskMetric(truthPath, polygon, predictionPath);
            AssertTrue(metric.IoU > 0.90D, "matching smart-mask polygon should have high IoU");
            AssertEqual("usable", ClassifyMobileSamCandidate(metric.IoU));
            AssertEqual("edit", ClassifyMobileSamCandidate(0.30D));
            AssertEqual("skip", ClassifyMobileSamCandidate(0.10D));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private sealed class MobileSamMatrixSample
    {
        public MobileSamMatrixSample(
            string className,
            string sourceSplit,
            string imageRelativePath,
            string maskRelativePath,
            string imagePath,
            string maskPath,
            Rectangle promptBounds)
        {
            ClassName = className;
            SourceSplit = sourceSplit;
            ImageRelativePath = imageRelativePath;
            MaskRelativePath = maskRelativePath;
            ImagePath = imagePath;
            MaskPath = maskPath;
            PromptBounds = promptBounds;
        }

        public string ClassName { get; }
        public string SourceSplit { get; }
        public string ImageRelativePath { get; }
        public string MaskRelativePath { get; }
        public string ImagePath { get; }
        public string MaskPath { get; }
        public Rectangle PromptBounds { get; }
    }

    private sealed class MobileSamMaskMetric
    {
        public MobileSamMaskMetric(
            int groundTruthPixels,
            int predictedPixels,
            int intersectionPixels,
            int unionPixels,
            double iou,
            double dice,
            double areaRatio)
        {
            GroundTruthPixels = groundTruthPixels;
            PredictedPixels = predictedPixels;
            IntersectionPixels = intersectionPixels;
            UnionPixels = unionPixels;
            IoU = iou;
            Dice = dice;
            AreaRatio = areaRatio;
        }

        public int GroundTruthPixels { get; }
        public int PredictedPixels { get; }
        public int IntersectionPixels { get; }
        public int UnionPixels { get; }
        public double IoU { get; }
        public double Dice { get; }
        public double AreaRatio { get; }
    }
}
