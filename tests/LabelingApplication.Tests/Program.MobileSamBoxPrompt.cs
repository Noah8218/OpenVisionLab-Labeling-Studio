using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunRealMobileSamBoxPrompt(string[] args)
    {
        try
        {
            string imagePath = Path.GetFullPath(GetArgumentValue(args, "--image", string.Empty));
            AssertTrue(File.Exists(imagePath), "--image must point to a real prompt image");
            Size imageSize = GetImageSize(imagePath);
            AssertTrue(
                TryParsePromptBox(GetArgumentValue(args, "--prompt-box", string.Empty), imageSize, out Rectangle promptBounds),
                "--prompt-box must be x,y,width,height inside the image");
            string evidenceRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(FindRepositoryRoot(), "artifacts", "mobile-sam-box-prompt")));
            string runRoot = Path.Combine(evidenceRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(runRoot);

            string sourceHashBefore = ComputeFileSha256(imagePath);
            var service = new WpfMobileSamBoxPromptService();
            var settings = new PythonModelSettings();
            settings.EnsureDefaults();
            WpfMobileSamBoxPromptRequest request = service.BuildRequest(
                settings,
                imagePath,
                promptBounds,
                classId: 0,
                className: "contamination_spot");
            AssertTrue(request.IsValid, string.Join(" ", request.Errors));
            WpfMobileSamBoxPromptResult result = service.RunAsync(request).GetAwaiter().GetResult();
            AssertTrue(result.Succeeded, result.Error);
            AssertTrue(result.Candidate?.PolygonPoints?.Count >= 3, "real MobileSAM result should contain a polygon");

            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            CData previousData = CGlobal.Inst.Data;
            var data = new CData();
            data.ConfigureOutputRoot(runRoot);
            data.LastSelectImageName = Path.GetFileNameWithoutExtension(imagePath);
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ProjectSettings.YoloDataset.TestPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "contamination_spot", DrawColor = Color.LimeGreen });
            CGlobal.Inst.Data = data;

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            Bitmap bitmap = new Bitmap(imagePath);
            try
            {
                SetPrivateField(window, "activeImagePath", imagePath);
                SetPrivateField(window, "activeImageSize", bitmap.Size);
                SetPrivateField(window, "activeImageBitmap", bitmap);
                SetPrivateField(window.MainCanvasViewModel, "_imageSize", bitmap.Size);
                InvokePrivateResult<object>(
                    window,
                    "ApplyDetectionCandidatesPreservingConfirmed",
                    new List<YoloWorkerSmokeCandidate> { result.Candidate },
                    true);
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(100));
                window.CandidateReviewViewModel.ConfirmSelectedCommand.Execute(null);
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(200));

                WpfCandidateReviewStateService candidateState = GetPrivateField<WpfCandidateReviewStateService>(window, "candidateReviewState");
                AssertEqual(0, candidateState.PendingCount);
                AssertEqual(1, candidateState.ConfirmedCount);
                string segmentPath = Directory.EnumerateFiles(Path.Combine(runRoot, "data", "train", "segments"), "*.json").Single();
                string maskPath = Directory.EnumerateFiles(Path.Combine(runRoot, "data", "train", "masks"), "*.png").Single();
                AssertTrue(CountExeSmokeSavedMaskPixels(maskPath) > 0, "confirmed real MobileSAM mask should contain foreground pixels");

                string sourceHashAfter = ComputeFileSha256(imagePath);
                AssertEqual(sourceHashBefore, sourceHashAfter);
                var evidence = new JObject
                {
                    ["status"] = "Complete",
                    ["scope"] = "MobileSAM box prompt to confirmed canonical segmentation label",
                    ["evidenceOrigin"] = "synthetic",
                    ["fieldValidation"] = "Not evaluated",
                    ["sourceImagePath"] = imagePath,
                    ["sourceImageSha256Before"] = sourceHashBefore,
                    ["sourceImageSha256After"] = sourceHashAfter,
                    ["promptBox"] = JArray.FromObject(new[] { promptBounds.X, promptBounds.Y, promptBounds.Width, promptBounds.Height }),
                    ["weightsPath"] = request.WeightsPath,
                    ["weightsSha256"] = result.WeightsSha256,
                    ["runtime"] = result.RuntimeSummary,
                    ["elapsedMs"] = result.ElapsedMilliseconds,
                    ["polygonPointCount"] = result.Candidate.PolygonPoints.Count,
                    ["maskArea"] = result.MaskArea,
                    ["segmentPath"] = segmentPath,
                    ["maskPath"] = maskPath,
                    ["boundary"] = "Synthetic labeling workflow evidence only; no production accuracy claim."
                };
                string evidencePath = Path.Combine(runRoot, "mobile-sam-evidence.json");
                File.WriteAllText(evidencePath, evidence.ToString(Formatting.Indented));
                Console.WriteLine("REAL_MOBILE_SAM_EVIDENCE=" + evidencePath);
                Console.WriteLine("REAL_MOBILE_SAM_SEGMENT=" + segmentPath);
                Console.WriteLine("REAL_MOBILE_SAM_MASK=" + maskPath);
                Console.WriteLine("REAL_MOBILE_SAM_POINTS=" + result.Candidate.PolygonPoints.Count);
                Console.WriteLine("REAL_MOBILE_SAM_ELAPSED_MS=" + result.ElapsedMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                return 0;
            }
            finally
            {
                SetPrivateField(window, "activeImageBitmap", null);
                window.Close();
                bitmap.Dispose();
                CGlobal.Inst.Data = previousData;
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("REAL_MOBILE_SAM_FAILED=" + error);
            return 1;
        }
    }

    private static void TestMobileSamBoxPromptContract()
    {
        string root = FindRepositoryRoot();
        string workerPath = Path.Combine(root, "Runtime", "Python", "openvisionlab_mobile_sam_box_prompt.py");
        AssertTrue(File.Exists(workerPath), "MobileSAM box-prompt worker should exist");
        string worker = File.ReadAllText(workerPath);
        AssertTrue(worker.Contains("from ultralytics import SAM", StringComparison.Ordinal), "MobileSAM worker should use the existing Ultralytics runtime");
        AssertTrue(worker.Contains("bboxes=[left, top, right, bottom]", StringComparison.Ordinal), "MobileSAM worker should use an operator box prompt");
        AssertTrue(worker.Contains("weightsSha256", StringComparison.Ordinal), "MobileSAM worker should report weight provenance");

        var service = new WpfMobileSamBoxPromptService();
        string relativeImagePath = Path.GetRelativePath(Environment.CurrentDirectory, workerPath);
        WpfMobileSamBoxPromptRequest normalizedRequest = service.BuildRequest(
            new PythonModelSettings(),
            relativeImagePath,
            new Rectangle(1, 1, 2, 2),
            classId: 0,
            className: "Defect");
        AssertEqual(Path.GetFullPath(relativeImagePath), normalizedRequest.ImagePath);
        var request = new WpfMobileSamBoxPromptRequest
        {
            ImagePath = "fixture.png",
            PromptBounds = new Rectangle(10, 20, 30, 40),
            ClassId = 2,
            ClassName = "scratch"
        };
        string payload = JsonConvert.SerializeObject(new
        {
            success = true,
            model = "MobileSAM",
            ultralyticsVersion = "8.4.101",
            torchVersion = "2.12.1+cpu",
            device = "cpu",
            weightsSha256 = new string('a', 64),
            elapsedMs = 1234.5,
            maskArea = 456,
            bounds = new { x = 11.0, y = 21.0, width = 27.0, height = 35.0 },
            polygon = new[]
            {
                new { x = 11.0, y = 21.0 },
                new { x = 38.0, y = 22.0 },
                new { x = 30.0, y = 56.0 },
                new { x = 12.0, y = 50.0 }
            }
        });
        WpfMobileSamBoxPromptResult result = service.ParseResult(0, payload, string.Empty, request);
        AssertTrue(result.Succeeded, "valid MobileSAM output should parse successfully");
        AssertEqual("smart-mask", result.Candidate.CandidateType);
        AssertEqual("polygon", result.Candidate.SegmentationType);
        AssertEqual("scratch", result.Candidate.ClassName);
        AssertEqual(4, result.Candidate.PolygonPoints.Count);
        AssertEqual(456, result.MaskArea);
        AssertTrue(result.RuntimeSummary.Contains("8.4.101", StringComparison.Ordinal), "MobileSAM result should preserve runtime provenance");
        AssertEqual("박스 프롬프트", WpfCandidateReviewPresenter.FormatConfidence(result.Candidate, "P1"));

        var viewModel = new WpfCanvasPanelViewModel();
        bool invoked = false;
        viewModel.ConfigureSmartMaskCommand(() => invoked = true);
        viewModel.SetSmartMaskState(isVisible: true, isEnabled: true, isBusy: false, "candidate only");
        AssertEqual(System.Windows.Visibility.Visible, viewModel.SmartMaskVisibility);
        AssertTrue(viewModel.IsSmartMaskEnabled, "smart-mask command should be enabled only when its prompt/runtime gates pass");
        viewModel.CreateSmartMaskCommand.Execute(null);
        AssertTrue(invoked, "smart-mask command should cross the ViewModel command boundary");
        viewModel.SetSmartMaskState(isVisible: true, isEnabled: true, isBusy: true, "running");
        AssertTrue(!viewModel.IsSmartMaskEnabled, "smart-mask command should disable while inference is running");

        string shellSource = ReadWpfLabelingShellWindowSources();
        AssertTrue(shellSource.Contains("clearConfirmed: false", StringComparison.Ordinal), "smart-mask assist should preserve already confirmed candidates");
        AssertTrue(shellSource.Contains("manualRois[currentPromptIndex] != promptBounds", StringComparison.Ordinal), "smart-mask result should compare the current rectangle with the requested prompt bounds");
        AssertTrue(shellSource.Contains("프롬프트 박스가 변경되어 후보를 적용하지 않았습니다", StringComparison.Ordinal), "smart-mask result should fail closed when its prompt changes");
        TestMobileSamUsabilityMetric();
    }

    private static void ApplyVisualSmokeSmartMaskCandidate(
        WpfLabelingShellWindow window,
        Size imageSize,
        string promptBoxText)
    {
        ApplyVisualSmokeSmartMaskPrompt(window, imageSize, promptBoxText);
        InvokePrivateResult<object>(window, "ExecuteCreateSmartMaskCandidateCommand");
        DateTime deadline = DateTime.UtcNow.AddSeconds(50);
        while (DateTime.UtcNow < deadline)
        {
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(250));
            WpfCandidateReviewStateService state = GetPrivateField<WpfCandidateReviewStateService>(window, "candidateReviewState");
            if (state.PendingCount > 0)
            {
                AssertEqual("smart-mask", state.PendingCandidates[0].CandidateType);
                return;
            }
            if (!string.Equals(window.CanvasPanelViewModel.SmartMaskActionText, "마스크 생성 중...", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "smart-mask visual smoke stopped without a candidate: "
                    + window.CanvasPanelViewModel.SmartMaskToolTip);
            }
        }

        throw new InvalidOperationException("smart-mask visual smoke did not produce a candidate within 50 seconds");
    }

    private static void ApplyVisualSmokeSmartMaskPrompt(
        WpfLabelingShellWindow window,
        Size imageSize,
        string promptBoxText)
    {
        List<Rectangle> prompts = GetPrivateField<List<Rectangle>>(window, "manualRois");
        if (prompts.Count == 0)
        {
            throw new InvalidOperationException("smart-mask visual smoke requires a rectangle prompt");
        }

        if (TryParsePromptBox(promptBoxText, imageSize, out Rectangle promptBounds))
        {
            prompts[prompts.Count - 1] = promptBounds;
            InvokePrivate(window, "RedrawReviewRois");
            InvokePrivate(window, "RefreshObjectList");
            InvokePrivateResult<object>(
                window,
                "SetModelStatus",
                $"스마트 마스크 프롬프트: {WpfCandidateReviewPresenter.FormatBoundsCompact(promptBounds)}");
        }
        InvokePrivateResult<object>(window, "RefreshSmartMaskCommandState", string.Empty);
    }

    private static bool TryParsePromptBox(string value, Size imageSize, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        string[] parts = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4
            || !int.TryParse(parts[0], out int x)
            || !int.TryParse(parts[1], out int y)
            || !int.TryParse(parts[2], out int width)
            || !int.TryParse(parts[3], out int height))
        {
            return false;
        }

        bounds = Rectangle.Intersect(
            new Rectangle(x, y, width, height),
            new Rectangle(Point.Empty, imageSize));
        return !bounds.IsEmpty;
    }
}
