using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using CvScalar = OpenCvSharp.Scalar;

namespace LabelingApplication.Tests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--real-yolo-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Real YOLO TCP workflow detects, overlays, confirms, and saves labels", TestRealYoloDetectionWorkflowSmoke);
        }

        var tests = new (string Name, Action Test)[]
        {
            ("YOLO path normalization uses forward slashes", TestNormalizeYamlPath),
            ("Class catalog normalizes names and rejects duplicates", TestClassCatalogService),
            ("CData creates YOLO dataset directories and data.yaml", TestCreateYoloDataset),
            ("YOLO dataset validator rejects invalid training configuration", TestYoloDatasetValidatorConfiguration),
            ("YOLO dataset validator accepts saved training files", TestYoloDatasetValidatorTrainingFiles),
            ("YOLO dataset validator rejects invalid label file contents", TestYoloDatasetValidatorInvalidLabels),
            ("YOLO dataset validator reports saved dataset statistics", TestYoloDatasetStatistics),
            ("YOLO dataset readiness report combines validation and statistics", TestYoloDatasetReadinessReport),
            ("YOLO dataset diagnostics report operator-ready issues", TestYoloDatasetDiagnosticsReport),
            ("YOLO annotation lines use normalized coordinates", TestYoloAnnotationLines),
            ("YOLO annotation service loads saved label rectangles", TestYoloAnnotationLoad),
            ("YOLO image label status reports saved object counts", TestYoloImageLabelStatusService),
            ("YOLO image review status tracks labels, candidates, and next unlabeled image", TestYoloImageReviewStatusService),
            ("YOLO annotation service writes image and label files", TestYoloAnnotationFileWrite),
            ("Segmentation annotation service writes masks and polygons", TestSegmentationAnnotationFileWrite),
            ("Segmentation geometry converts boxes into editable polygons", TestSegmentationGeometryBoxConversion),
            ("Training parameter alias preserves legacy configuration", TestTrainingParamAlias),
            ("Training settings mirror legacy training parameters", TestTrainingSettingsMirror),
            ("Learning protocol builds normalized training packet", TestLearningProtocol),
            ("Learning communication can be created without opening TCP listener", TestLearningCommunicationDeferredStart),
            ("Learning communication closes safely while accept callback is pending", TestLearningCommunicationCloseDuringAccept),
            ("Learning communication can restart listener after close", TestLearningCommunicationRestartAfterClose),
            ("Learning communication sends StartDefect and applies TCP ResultDefect", TestLearningCommunicationDetectionRoundTrip),
            ("Python model settings default to YOLOv5 client paths", TestPythonModelSettingsDefaults),
            ("Python model settings validator reports missing weights", TestPythonModelSettingsValidator),
            ("YOLOv5 Python client start info validates script path", TestYoloPythonClientStartInfo),
            ("YOLO detection workflow validates settings before sending", TestYoloDetectionWorkflowValidation),
            ("Python message framer rebuilds split TCP messages", TestPythonMessageFramer),
            ("Python model status protocol parses training progress", TestPythonModelStatusProtocol),
            ("TCP receive queue decodes configured UTF-8 strings", TestTcpReceiveQueueEncoding),
            ("Python ResultDefect protocol parses detection boxes", TestPythonDetectionResultProtocol),
            ("Python ResultDefect protocol reports invalid payloads", TestPythonDetectionResultProtocolFailures),
            ("OpenVisionLab ImageSpace tracks active labeling image", TestLabelingImageWorkspace),
            ("OpenVisionLab log adapter accepts formatted messages", TestAppLog),
            ("Screen capture path is sanitized and created without opening UI", TestScreenCapturePath),
            ("Image list uses supported image file filtering", TestImageListSupportedFiles),
            ("FormImageList uses labeling dataset command bar instead of visible ToolStrip", TestFormImageListDatasetCommandBar),
            ("FormImageList queues 1000 image shells without blocking", TestFormImageListLargeQueuePerformance),
            ("FormMainFrame groups toolbar commands around labeling workflow", TestFormMainFrameLabelingCommandBar),
            ("FormMainFrame surfaces training progress and stop state", TestFormMainFrameTrainingStatusUi),
            ("FormTrainingPanel exposes Python Worker controls", TestFormTrainingPanelPythonWorkerControls),
            ("Auxiliary labeling windows use labeling terminology", TestAuxiliaryLabelingUiText),
            ("YOLO model settings window uses labeling terminology", TestYoloModelSettingsUiText),
            ("ROI geometry clamps dragged rectangles to image bounds", TestRoiGeometry),
            ("OpenGL image geometry maps image coordinates consistently", TestOpenGlImageGeometry),
            ("ROI interaction selects and removes rectangles", TestRoiInteraction),
            ("CViewer stores ROI rectangles without opening UI", TestCViewerRoiApi),
            ("CViewer stores segmentation polygons without opening UI", TestCViewerSegmentationApi),
            ("CViewer saves default Defect segmentation labels", TestCViewerDefaultDefectSegmentationSave),
            ("CViewer converts display images to OpenGL texture format", TestCViewerImageCopy),
            ("CViewer main image load preserves active image metadata", TestCViewerMainImageWorkspace),
            ("CViewer attach replaces and disposes previous OpenGL canvas", TestCViewerAttachLifecycle),
            ("CViewer exposes labeling mode chrome without legacy menu text", TestCViewerLabelingModeChrome),
            ("FormLayerDisplay disposes hosted OpenGL viewer", TestFormLayerDisplayDisposeLifecycle),
            ("FormTeachingVision initializes fixed labeling workbench layout", TestFormTeachingVisionWorkbenchLayout),
            ("FormClassList focuses on labels and classes", TestFormClassListReviewSegments),
            ("Detection review panel hosts AI candidate workflow", TestFormDetectionReviewPanel),
            ("CDisplayManager hosts OpenGL display directly in canvas panel", TestDisplayManagerDirectCanvasHost),
            ("CDisplayManager owns replaced image source mats", TestDisplayManagerImageSourceOwnership),
            ("CDisplayManager exposes Dev-style layer catalog APIs", TestDisplayManagerLayerCatalog),
            ("CDisplayManager routes detection overlays to the exact layer", TestDisplayManagerDetectionOverlayRouting),
            ("Labeling workflow applies selected class and lists ROI rows", TestLabelingWorkflowService),
            ("Labeling workflow commits current annotations to YOLO files", TestLabelingWorkflowCommitAnnotations),
            ("Labeling workflow defaults unclassified ROI to Defect", TestLabelingWorkflowDefaultDefectClass),
            ("Labeling workflow reloads saved YOLO labels into Main layer", TestLabelingWorkflowReloadsSavedAnnotations),
            ("Detection result service applies Python boxes as Main candidates", TestDetectionResultApplicationService),
            ("Detection result service marshals Python boxes onto UI thread", TestDetectionResultApplicationServiceBackgroundThread),
            ("Detection result service keeps Main active after applying boxes", TestDetectionResultKeepsMainActive),
            ("Detection result service clears candidate state on empty Python boxes", TestDetectionResultApplicationServiceClearsEmptyResult),
            ("Detection result service ignores late boxes after pending cancellation", TestDetectionResultCancelPendingIgnoresLateBoxes),
            ("Detection result service times out pending Python requests", TestDetectionResultPendingRequestTimeout),
            ("Detection result service confirms boxes as Main labels", TestDetectionResultConfirmAsLabels),
            ("Detection result service can confirm boxes as segmentation masks", TestDetectionResultConfirmAsSegmentationMasks),
            ("Detection result service uses minimum confidence for confirmation availability", TestDetectionResultCanCommitUsesMinimumConfidence),
            ("Detection result service exposes review candidates with confirmability", TestDetectionResultReviewCandidates),
            ("Detection result service confirms selected candidate and keeps remaining candidates", TestDetectionResultConfirmsSelectedCandidateOnly),
            ("Detection result service skips selected candidate and reindexes overlays", TestDetectionResultSkipsSelectedCandidate),
            ("Detection result service confirms all candidates despite selection", TestDetectionResultConfirmsAllCandidatesDespiteSelection),
            ("Detection result service filters low-confidence boxes on confirm", TestDetectionResultFiltersLowConfidence),
            ("Detection result service adds missing model classes on confirm", TestDetectionResultAddsMissingClasses),
            ("Detection result service ignores boxes for a stale image", TestDetectionResultRejectsStaleImage),
            ("Detection result service rejects stale boxes during confirmation", TestDetectionResultRejectsStaleConfirmation),
            ("Measurement geometry calculates distance and vertical projection", TestMeasurementGeometry),
            ("Detection results build OpenGL overlays", TestDetectionOverlays),
            ("CViewer renders detection overlay labels and disposes textures", TestCViewerDetectionOverlayRenderLifecycle)
        };

        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        return 0;
    }

    private static int RunSingleSmoke(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            return 1;
        }
    }

    private static void TestNormalizeYamlPath()
    {
        string normalized = CYolov5.NormalizeYamlPath(@"C:\datasets\train\images");
        AssertEqual("C:/datasets/train/images", normalized);
    }

    private static void TestClassCatalogService()
    {
        var data = new CData();

        AssertTrue(ClassCatalogService.TryAddClass(data, " Part ", out CClassItem first), "class was not added");
        AssertEqual("Part", first.Text);
        AssertEqual(Color.Green, first.DrawColor);
        AssertTrue(!ClassCatalogService.TryAddClass(data, "part", out CClassItem _), "duplicate class was accepted");
        AssertTrue(!ClassCatalogService.TryAddClass(data, " ", out CClassItem _), "empty class was accepted");
        AssertTrue(ClassCatalogService.RemoveClass(data, "PART"), "class was not removed case-insensitively");
        AssertEqual(0, data.ClassNamedList.Count);
    }

    private static void TestCreateYoloDataset()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.Red });

            data.SaveYoloDataYaml();

            AssertTrue(Directory.Exists(data.TrainImagesPath), "train images directory was not created");
            AssertTrue(Directory.Exists(Path.Combine(data.OutputRootPath, "data", "train", "labels")), "train labels directory was not created");
            AssertTrue(Directory.Exists(data.ValidImagesPath), "valid images directory was not created");
            AssertTrue(File.Exists(data.DataYamlFilePath), "data.yaml was not created");

            string yaml = File.ReadAllText(data.DataYamlFilePath);
            AssertTrue(yaml.Contains("nc: 2"), "data.yaml does not contain class count");
            AssertTrue(yaml.Contains("OK"), "data.yaml does not contain first class name");
            AssertTrue(yaml.Contains("NG"), "data.yaml does not contain second class name");
            AssertTrue(!yaml.Contains("\\"), "data.yaml contains backslashes");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetValidatorConfiguration()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "ok", DrawColor = Color.Red });
            data.ProjectSettings.YoloDataset.ValidationPercent = 120;
            data.TranningParam.batch = 0;

            YoloDatasetValidationResult result = YoloDatasetValidator.ValidateConfiguration(data);

            AssertTrue(!result.IsValid, "invalid training configuration was accepted");
            AssertTrue(result.Errors.Any(error => error.Contains("Duplicate class name")), "duplicate class name was not reported");
            AssertTrue(result.Errors.Any(error => error.Contains("Validation split percent")), "invalid validation split was not reported");
            AssertTrue(result.Errors.Any(error => error.IndexOf("batch", StringComparison.OrdinalIgnoreCase) >= 0), "invalid batch was not reported");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetValidatorTrainingFiles()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(10, 10, 20, 20), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap image = new Bitmap(80, 60))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", image, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", image, rois, data.ClassNamedList, data);
            }

            YoloDatasetValidationResult configuration = YoloDatasetValidator.ValidateConfiguration(data);
            YoloDatasetValidationResult files = YoloDatasetValidator.ValidateTrainingFiles(data);

            AssertTrue(configuration.IsValid, configuration.Summary);
            AssertTrue(files.IsValid, files.Summary);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetValidatorInvalidLabels()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.EnsureYoloOutputDirectories();
            data.SaveYoloDataYaml();

            using (Bitmap image = new Bitmap(20, 20))
            {
                image.Save(Path.Combine(data.TrainImagesPath, "sample.jpeg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(Path.Combine(data.ValidImagesPath, "sample.jpeg"), System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            string trainLabel = Path.Combine(root, "data", "train", "labels", "sample.txt");
            string validLabel = Path.Combine(root, "data", "valid", "labels", "sample.txt");
            File.WriteAllText(trainLabel, "1 0.5 0.5 0.2 0.2");
            File.WriteAllText(validLabel, "0 1.2 0.5 0.2 0.2");

            YoloDatasetValidationResult result = YoloDatasetValidator.ValidateTrainingFiles(data);

            AssertTrue(!result.IsValid, "invalid label contents were accepted");
            AssertTrue(result.Errors.Any(error => error.Contains("invalid class index")), "invalid class index was not reported");
            AssertTrue(result.Errors.Any(error => error.Contains("out-of-range normalized value")), "out-of-range value was not reported");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetStatistics()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(5, 5, 10, 10), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap image = new Bitmap(40, 40))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", image, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", image, rois, data.ClassNamedList, data);
            }

            YoloDatasetStatistics statistics = YoloDatasetValidator.BuildStatistics(data);

            AssertEqual(1, statistics.TrainImageCount);
            AssertEqual(1, statistics.ValidImageCount);
            AssertEqual(1, statistics.TrainLabelCount);
            AssertEqual(1, statistics.ValidLabelCount);
            AssertEqual(2, statistics.TotalObjectCount);
            AssertEqual(2, statistics.ObjectCountByClass["OK"]);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetReadinessReport()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(5, 5, 10, 10), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap image = new Bitmap(40, 40))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", image, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", image, rois, data.ClassNamedList, data);
            }

            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml: true);

            AssertTrue(report.IsReady, string.Join(Environment.NewLine, report.Errors));
            AssertEqual(1, report.Statistics.TrainImageCount);
            AssertEqual(1, report.Statistics.ValidImageCount);
            AssertTrue(report.SummaryLines.Any(line => line.Contains("TrainImages:1")), "readiness summary did not include train image count");
            AssertTrue(report.SummaryLines.Any(line => line.Contains("OK:2")), "readiness summary did not include class object count");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetDiagnosticsReport()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);

            IReadOnlyList<string> lines = YoloDatasetDiagnosticsService.BuildOperatorReport(data, refreshYaml: false);

            AssertTrue(lines.Any(line => line.Contains("NOT READY")), "diagnostics did not report not-ready state");
            AssertTrue(lines.Any(line => line.Contains("YOLO output root")), "diagnostics did not include output root");
            AssertTrue(lines.Any(line => line.Contains("At least one class")), "diagnostics did not include class issue");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestTrainingSettingsMirror()
    {
        var data = new CData();
        data.TrainingParam.imageSize = 640;
        data.TrainingParam.batch = 8;
        data.TrainingParam.epoch = 100;
        data.TrainingParam.cfg = CYolov5TrainingParam.Cfg.yolov5m;
        data.TrainingParam.weight = CYolov5TrainingParam.Weight.yolov5m;

        TrainingSettings settings = data.GetTrainingSettings();

        AssertEqual(640, settings.ImageSize);
        AssertEqual(8, settings.Batch);
        AssertEqual(100, settings.Epoch);
        AssertEqual("yolov5m", settings.Cfg);
        AssertEqual("yolov5m", settings.Weight);
    }

    private static void TestTrainingParamAlias()
    {
        var data = new CData();
        data.TrainingParam.imageSize = 512;

        AssertTrue(ReferenceEquals(data.TranningParam, data.TrainingParam), "TrainingParam alias should use the legacy XML-backed object");
        AssertEqual(512, data.TranningParam.imageSize);

        data.TrainingParam = null;
        AssertTrue(data.TrainingParam != null, "TrainingParam alias should reject null assignment");
    }

    private static void TestYoloAnnotationLines()
    {
        var classes = new List<CClassItem>
        {
            new CClassItem { Text = "OK", DrawColor = Color.Green },
            new CClassItem { Text = "NG", DrawColor = Color.Red }
        };
        var rois = new Dictionary<string, List<CRectangleObject>>
        {
            ["NG"] = new List<CRectangleObject>
            {
                new CRectangleObject { Roi = new Rectangle(10, 20, 30, 40), cClassItem = classes[1] }
            }
        };

        List<string> lines = YoloAnnotationService.BuildAnnotationLines(rois, classes, new Size(100, 200));

        AssertEqual(1, lines.Count);
        AssertEqual("1 0.25 0.2 0.3 0.2", lines[0]);

        var data = new CData();
        string root = CreateTempRoot();
        try
        {
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            IReadOnlyList<string> targetLabelPaths = YoloAnnotationService.GetTargetLabelPaths("part-001.png", data);

            AssertEqual(1, targetLabelPaths.Count);
            AssertEqual(Path.Combine(root, "data", "train", "labels", "part-001.txt"), targetLabelPaths[0]);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloAnnotationLoad()
    {
        string root = CreateTempRoot();
        try
        {
            string labelPath = Path.Combine(root, "sample.txt");
            File.WriteAllLines(labelPath, new[]
            {
                "1 0.25 0.2 0.3 0.2",
                "bad line",
                "2 0.5 0.5 0.1 0.1"
            });

            var classes = new List<CClassItem>
            {
                new CClassItem { Text = "OK", DrawColor = Color.Green },
                new CClassItem { Text = "NG", DrawColor = Color.Red }
            };

            IReadOnlyDictionary<string, List<Rectangle>> loaded = YoloAnnotationService.LoadAnnotationRectangles(
                labelPath,
                classes,
                new Size(100, 200));

            AssertEqual(1, loaded.Count);
            AssertTrue(loaded.ContainsKey("NG"), "NG label was not loaded");
            AssertEqual(new Rectangle(10, 20, 30, 40), loaded["NG"][0]);

            AssertTrue(YoloAnnotationService.TryParseYoloLine("0 0.5 0.5 1 1", new Size(20, 10), out int classIndex, out Rectangle roi), "valid YOLO line did not parse");
            AssertEqual(0, classIndex);
            AssertEqual(new Rectangle(0, 0, 20, 10), roi);
            AssertTrue(!YoloAnnotationService.TryParseYoloLine("0 1.2 0.5 0.1 0.1", new Size(20, 10), out _, out _), "out-of-range YOLO line was accepted");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloImageLabelStatusService()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            data.EnsureYoloOutputDirectories();

            string imagePath = Path.Combine(root, "source", "sample.png");
            YoloImageLabelStatus missing = YoloImageLabelStatusService.Build(imagePath, new Size(20, 20), data);
            AssertTrue(!missing.HasLabelFile, "missing label status should not report a label file");
            AssertEqual("No Label", missing.Text);

            string labelPath = Path.Combine(root, "data", "train", "labels", "sample.txt");
            File.WriteAllLines(labelPath, new[]
            {
                "0 0.5 0.5 0.5 0.5",
                "9 0.5 0.5 0.5 0.5",
                "bad line"
            });

            YoloImageLabelStatus status = YoloImageLabelStatusService.Build(imagePath, new Size(20, 20), data);
            AssertTrue(status.HasLabelFile, "label status did not locate the saved label file");
            AssertEqual(labelPath, status.LabelPath);
            AssertEqual(1, status.ObjectCount);
            AssertEqual(2, status.InvalidLineCount);
            AssertEqual("Label 1 / Invalid 2", status.Text);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloImageReviewStatusService()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            data.EnsureYoloOutputDirectories();

            string sourceDirectory = Path.Combine(root, "source");
            Directory.CreateDirectory(sourceDirectory);

            string labeledImagePath = Path.Combine(sourceDirectory, "labeled.png");
            string candidateImagePath = Path.Combine(sourceDirectory, "candidate.png");
            string failedImagePath = Path.Combine(sourceDirectory, "failed.png");
            string skippedImagePath = Path.Combine(sourceDirectory, "skipped.png");
            var imagePaths = new List<string> { labeledImagePath, candidateImagePath, failedImagePath, skippedImagePath };

            string labelPath = Path.Combine(root, "data", "train", "labels", "labeled.txt");
            File.WriteAllText(labelPath, "0 0.5 0.5 0.25 0.25");

            var service = new YoloImageReviewStatusService();
            service.SetImages(imagePaths);
            service.RefreshLabelStatus(labeledImagePath, new Size(100, 100), data);
            service.RefreshLabelStatus(candidateImagePath, new Size(100, 100), data);
            service.RefreshLabelStatus(failedImagePath, new Size(100, 100), data);
            service.RefreshLabelStatus(skippedImagePath, new Size(100, 100), data);

            AssertEqual(1, service.GetLabeledCount());
            AssertTrue(service.TryFindNextUnlabeled(imagePaths, labeledImagePath, out string nextImagePath), "next unlabeled image was not found");
            AssertEqual(candidateImagePath, nextImagePath);

            YoloImageReviewStatus requested = service.SetDetectionRequested(candidateImagePath);
            AssertEqual("Requested", requested.DetectionText);

            YoloImageReviewStatus candidates = service.SetDetectionCandidates(string.Empty, "candidate", 2);
            AssertEqual(candidateImagePath, candidates.ImagePath);
            AssertEqual(2, candidates.DetectionCandidateCount);
            AssertEqual("Candidate 2", candidates.DetectionText);

            string candidateLabelPath = Path.Combine(root, "data", "train", "labels", "candidate.txt");
            File.WriteAllText(candidateLabelPath, "0 0.5 0.5 0.25 0.25");
            YoloImageReviewStatus candidateWithActiveBoxes = service.RefreshLabelStatusAndReviewState(candidateImagePath, new Size(100, 100), data, hasActiveCandidates: true);
            AssertEqual("Candidate 2", candidateWithActiveBoxes.DetectionText);
            AssertTrue(candidateWithActiveBoxes.IsLabeled, "saved label status should be preserved while active candidates remain");
            AssertEqual("Label 1", candidateWithActiveBoxes.LabelText);

            YoloImageReviewStatus noCandidates = service.SetDetectionNoCandidates(candidateImagePath, "candidate");
            AssertEqual("No Candidate", noCandidates.DetectionText);

            YoloImageReviewStatus cleared = service.ClearDetectionStatus(candidateImagePath);
            AssertEqual(string.Empty, cleared.DetectionText);

            YoloImageReviewStatus failedRequest = service.SetDetectionRequested(failedImagePath);
            AssertEqual(1, failedRequest.DetectionAttemptCount);

            YoloImageReviewStatus failed = service.SetDetectionFailed(failedImagePath, string.Empty, "Batch detection timed out.");
            AssertEqual("Failed", failed.DetectionText);
            AssertEqual(1, failed.DetectionAttemptCount);
            AssertTrue(failed.DetectionDetailText.Contains("Batch detection timed out."), "failed detection detail did not include the failure reason");

            service.SetDetectionRequested(failedImagePath);
            YoloImageReviewStatus retriedFailure = service.SetDetectionFailed(failedImagePath, string.Empty, "Detection request failed.");
            AssertEqual(2, retriedFailure.DetectionAttemptCount);
            AssertTrue(retriedFailure.DetectionDetailText.Contains("Attempt 2"), "retry count was not reflected in detection detail text");

            YoloImageReviewStatus skipped = service.MarkSkipped(skippedImagePath);
            AssertEqual("Skipped", skipped.DetectionText);
            AssertEqual(0, skipped.DetectionCandidateCount);
            AssertTrue(skipped.DetectionDetailText.Contains("Candidate skipped."), "skipped review detail did not include the skip reason");

            YoloImageReviewStatus confirmed = service.MarkConfirmed(labeledImagePath);
            AssertEqual("Confirmed", confirmed.DetectionText);
            AssertEqual(0, confirmed.DetectionCandidateCount);
            AssertTrue(confirmed.DetectionDetailText.Contains("Candidates confirmed."), "confirmed review detail did not include the confirmation reason");

            YoloImageReviewStatus confirmedBySavedLabel = service.RefreshLabelStatusAndReviewState(labeledImagePath, new Size(100, 100), data, hasActiveCandidates: false);
            AssertEqual("Confirmed", confirmedBySavedLabel.DetectionText);

            service.SetDetectionCandidates(candidateImagePath, "candidate", 2);
            service.SaveReviewStatus(data);

            string reviewStatusPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(data);
            AssertTrue(File.Exists(reviewStatusPath), "review status file was not saved");
            string reviewStatusJson = File.ReadAllText(reviewStatusPath);
            AssertTrue(reviewStatusJson.Contains("\"ReviewStateName\": \"Confirmed\""), "review status file did not include a readable confirmed state name");
            AssertTrue(reviewStatusJson.Contains("\"ReviewStateName\": \"Candidate\""), "review status file did not include a readable candidate state name");

            var restored = new YoloImageReviewStatusService();
            restored.LoadReviewStatus(data, imagePaths);

            AssertEqual("Confirmed", restored.GetOrCreate(labeledImagePath).DetectionText);
            AssertEqual("Candidate 2", restored.GetOrCreate(candidateImagePath).DetectionText);
            AssertEqual("Failed", restored.GetOrCreate(failedImagePath).DetectionText);
            AssertEqual(2, restored.GetOrCreate(failedImagePath).DetectionAttemptCount);
            AssertEqual("Detection request failed.", restored.GetOrCreate(failedImagePath).LastDetectionMessage);
            AssertEqual("Skipped", restored.GetOrCreate(skippedImagePath).DetectionText);

            string namedOnlyStatusJson = reviewStatusJson.Replace("\"ReviewState\": 4,", "\"ReviewState\": 999,");
            File.WriteAllText(reviewStatusPath, namedOnlyStatusJson);
            var restoredFromName = new YoloImageReviewStatusService();
            restoredFromName.LoadReviewStatus(data, imagePaths);
            AssertEqual("Confirmed", restoredFromName.GetOrCreate(labeledImagePath).DetectionText);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloAnnotationFileWrite()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(25, 25, 50, 50), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap image = new Bitmap(100, 100))
            {
                YoloAnnotationService.SaveAnnotations("sample.png", image, rois, data.ClassNamedList, data);
            }

            string trainLabel = Path.Combine(root, "data", "train", "labels", "sample.txt");
            string validLabel = Path.Combine(root, "data", "valid", "labels", "sample.txt");
            string trainImage = Path.Combine(root, "data", "train", "images", "sample.jpeg");
            AssertTrue(File.Exists(trainLabel), "train label file was not created");
            AssertTrue(!File.Exists(validLabel), "valid label file should not be created when validation split is 0%");
            AssertTrue(File.Exists(trainImage), "train image file was not created");
            AssertEqual("0 0.5 0.5 0.5 0.5", File.ReadAllText(trainLabel).Trim());

            rois["OK"][0].Roi = new Rectangle(10, 20, 30, 40);
            DateTime imageStampBefore = File.GetLastWriteTimeUtc(trainImage).AddMinutes(-5);
            File.SetLastWriteTimeUtc(trainImage, imageStampBefore);
            imageStampBefore = File.GetLastWriteTimeUtc(trainImage);
            using (Bitmap image = new Bitmap(100, 100))
            {
                YoloAnnotationService.SaveAnnotations("sample.png", image, rois, data.ClassNamedList, data);
            }

            AssertEqual(imageStampBefore, File.GetLastWriteTimeUtc(trainImage));
            AssertEqual("0 0.25 0.4 0.3 0.4", File.ReadAllText(trainLabel).Trim());

            IReadOnlyDictionary<string, List<Rectangle>> loaded = YoloAnnotationService.LoadAnnotationRectanglesForImage(
                trainImage,
                data.ClassNamedList,
                data,
                new Size(100, 100));

            AssertEqual(new Rectangle(10, 20, 30, 40), loaded["OK"][0]);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestSegmentationAnnotationFileWrite()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.Red });

            using var image = new Bitmap(20, 20);
            var segments = new Dictionary<string, List<LabelingSegmentationObject>>
            {
                ["Defect"] = new List<LabelingSegmentationObject>
                {
                    new LabelingSegmentationObject(
                        new[]
                        {
                            new Point(2, 2),
                            new Point(12, 2),
                            new Point(12, 12),
                            new Point(2, 12)
                        },
                        data.ClassNamedList[1])
                    {
                        CutoutPolygons = new List<List<Point>>
                        {
                            new List<Point>
                            {
                                new Point(6, 6),
                                new Point(9, 6),
                                new Point(9, 9),
                                new Point(6, 9)
                            }
                        }
                    }
                }
            };

            YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                "seg-sample.png",
                image,
                segments,
                data.ClassNamedList,
                data);

            string maskPath = Path.Combine(root, "data", "train", "masks", "seg-sample.png");
            string segmentPath = Path.Combine(root, "data", "train", "segments", "seg-sample.json");
            AssertTrue(File.Exists(maskPath), "segmentation mask file was not saved");
            AssertTrue(File.Exists(segmentPath), "segmentation polygon json was not saved");

            using (var mask = new Bitmap(maskPath))
            {
                AssertEqual(2, mask.GetPixel(5, 5).R);
                AssertEqual(0, mask.GetPixel(7, 7).R);
                AssertEqual(0, mask.GetPixel(18, 18).R);
            }

            IReadOnlyDictionary<string, List<List<Point>>> loaded = YoloSegmentationAnnotationService.LoadSegmentationPolygons(
                segmentPath,
                data.ClassNamedList,
                image.Size);
            AssertTrue(loaded.ContainsKey("Defect"), "segmentation polygon class was not restored");
            AssertEqual(1, loaded["Defect"].Count);
            AssertEqual(new Point(2, 2), loaded["Defect"][0][0]);
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> loadedObjects = YoloSegmentationAnnotationService.LoadSegmentationObjects(
                segmentPath,
                data.ClassNamedList,
                image.Size);
            AssertEqual(1, loadedObjects["Defect"][0].CutoutPolygons.Count);
            SegmentationAnnotationFile annotationFile = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            AssertEqual(2, annotationFile.Version);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestSegmentationGeometryBoxConversion()
    {
        List<Point> polygon = SegmentationGeometry.RectangleToPolygon(new Rectangle(-2, 3, 12, 8), new Size(20, 20));

        AssertEqual(4, polygon.Count);
        AssertEqual(new Rectangle(0, 3, 10, 8), SegmentationGeometry.GetBounds(polygon));
        AssertTrue(SegmentationGeometry.ContainsPoint(polygon, new Point(5, 5)), "box polygon should contain its center");
        AssertTrue(!SegmentationGeometry.ContainsPoint(polygon, new Point(12, 5)), "box polygon should not contain outside pixels");

        List<Point> noisyLine = SegmentationGeometry.NormalizePolygon(
            new[]
            {
                new Point(1, 1),
                new Point(2, 1),
                new Point(3, 1),
                new Point(10, 1),
                new Point(10, 8),
                new Point(1, 8)
            },
            new Size(20, 20),
            minimumDistance: 1,
            simplificationTolerance: 2D);
        AssertTrue(noisyLine.Count < 6, "segmentation simplification should remove redundant drag points");

        List<Point> brush = SegmentationGeometry.CircleToPolygon(new Point(10, 10), 4, new Size(30, 30));
        AssertTrue(brush.Count >= 8, "brush polygon should create editable circle points");
        AssertTrue(SegmentationGeometry.IntersectsCircle(brush, new Point(10, 10), 3), "brush polygon should intersect its center circle");
        List<List<Point>> brushStroke = SegmentationGeometry.BrushStrokeToPolygons(
            new[]
            {
                new Point(10, 10),
                new Point(14, 10),
                new Point(18, 10)
            },
            4,
            new Size(40, 30));
        AssertEqual(1, brushStroke.Count);
        AssertTrue(SegmentationGeometry.GetBounds(brushStroke[0]).Contains(new Point(18, 10)), "brush stroke polygon should cover the last stroke point");
        List<Point> eraseSource = SegmentationGeometry.RectangleToPolygon(new Rectangle(5, 5, 20, 12), new Size(40, 30));
        List<List<Point>> edgeErased = SegmentationGeometry.EraseCircleFromPolygon(eraseSource, new Point(23, 11), 5, new Size(40, 30));
        AssertEqual(1, edgeErased.Count);
        AssertTrue(!SegmentationGeometry.ContainsPoint(edgeErased[0], new Point(23, 11)), "edge eraser should remove the brush center from the segment");
        List<Point> splitSource = SegmentationGeometry.RectangleToPolygon(new Rectangle(2, 2, 30, 12), new Size(40, 30));
        List<List<Point>> splitErased = SegmentationGeometry.EraseCircleFromPolygon(splitSource, new Point(17, 8), 8, new Size(40, 30));
        AssertTrue(splitErased.Count >= 2, "eraser should split separated remaining mask components");
        List<Point> mergedHull = SegmentationGeometry.MergePolygonsToHull(
            new[]
            {
                SegmentationGeometry.RectangleToPolygon(new Rectangle(1, 1, 5, 5), new Size(30, 30)),
                SegmentationGeometry.RectangleToPolygon(new Rectangle(12, 8, 4, 4), new Size(30, 30))
            },
            new Size(30, 30));
        AssertTrue(mergedHull.Count >= 3, "merged segmentation hull should create an editable polygon");
        AssertTrue(SegmentationGeometry.GetBounds(mergedHull).Contains(new Point(12, 8)), "merged hull should include the second segment bounds");

        using var image = new Bitmap(40, 40);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.FromArgb(180, 180, 180));
            using var brushFill = new SolidBrush(Color.FromArgb(25, 25, 25));
            graphics.FillEllipse(brushFill, 14, 15, 9, 8);
        }

        List<Point> extracted = SegmentationGeometry.ExtractDarkRegionPolygon(image, new Rectangle(8, 8, 24, 24));
        Rectangle extractedBounds = SegmentationGeometry.GetBounds(extracted);
        AssertTrue(extracted.Count >= 3, "dark region extraction should create a polygon");
        AssertTrue(extractedBounds.Contains(new Point(18, 18)), "dark region extraction should include the defect center");
    }

    private static void TestLearningProtocol()
    {
        byte[] packet = LearningProtocol.BuildTrainingPacket(
            "StartTraining",
            "640",
            "8",
            "100",
            @"models\yolov5m.yaml",
            @"weights\yolov5m.pt",
            @"C:\데이터\data.yaml");

        string text = Encoding.UTF8.GetString(packet);
        string[] parts = text.Split(LearningProtocol.PacketSeparator);
        AssertEqual(2, parts.Length);
        AssertEqual("StartTraining", parts[0]);

        var request = JsonConvert.DeserializeObject<YoloTrainingRequest>(parts[1]);
        AssertEqual("models/yolov5m.yaml", request.cfg);
        AssertEqual("weights/yolov5m.pt", request.weight);
        string healthJson = Encoding.UTF8.GetString(LearningProtocol.BuildHealthCheckPacket("req-health")).Trim();
        AssertTrue(healthJson.Contains("\"type\":\"HealthCheck\""), "health check packet should use JSON lines protocol");
        AssertTrue(healthJson.Contains("\"requestId\":\"req-health\""), "health check packet should include requestId");

        string modelJson = Encoding.UTF8.GetString(LearningProtocol.BuildModelStatusPacket("req-model", ensureLoaded: true)).Trim();
        AssertTrue(modelJson.Contains("\"type\":\"ModelStatus\""), "model status packet should use JSON lines protocol");
        AssertTrue(modelJson.Contains("\"ensureLoaded\":true"), "model status packet should request model loading");

        string detectJson = Encoding.UTF8.GetString(LearningProtocol.BuildDetectImagePacket(
            "req-detect",
            "image-001",
            @"C:\images\part.png",
            0.25F)).Trim();
        AssertTrue(detectJson.Contains("\"type\":\"DetectImage\""), "detect image packet should use DetectImage type");
        AssertTrue(detectJson.Contains("\"requestId\":\"req-detect\""), "detect image packet should include requestId");
        AssertTrue(detectJson.Contains("\"imageId\":\"image-001\""), "detect image packet should include imageId");
        AssertTrue(detectJson.Contains("\"imagePath\":\"C:\\\\images\\\\part.png\""), "detect image packet should include imagePath");
        AssertEqual("C:/데이터/data.yaml", request.dataYaml);
    }

    private static void TestLearningCommunicationDeferredStart()
    {
        using var communication = new CCommunicationLearning(startListen: false);
        PythonCommunicationStatus status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "deferred communication should not start the TCP listener");
        AssertTrue(!status.IsClientConnected, "deferred communication should not report a connected client");

        using var bitmap = new Bitmap(4, 4);
        AssertTrue(!communication.SendData(CCommunicationLearning.CommandLearning.StartDefect.ToString(), bitmap), "image packet send should fail without a connected Python client");
        AssertTrue(!communication.SendTrainingData(CCommunicationLearning.CommandLearning.StartTraining.ToString(), "320", "1", "1", "yolov5m.yaml", "best.pt", "data.yaml"), "training packet send should fail without a connected Python client");

        communication.Close();
        status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed communication should not report a listening TCP listener");
        AssertEqual("stopped", status.LastWorkerState);
        AssertEqual("stopped", status.LastModelState);
        AssertTrue(!status.LastModelLoaded, "closed communication should clear model loaded state");
    }

    private static void TestLearningCommunicationCloseDuringAccept()
    {
        int port = GetAvailableTcpPort();
        using var communication = new CCommunicationLearning(startListen: true, port: port);
        using var client = new TcpClient();

        Task connectTask = client.ConnectAsync(IPAddress.Loopback, port);
        Thread.Sleep(20);
        communication.Close();

        try
        {
            connectTask.Wait(1000);
        }
        catch
        {
            // The connection can legitimately fail if Close wins the race.
        }

        Thread.Sleep(100);
        PythonCommunicationStatus status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed communication should not report a listening TCP listener");
        AssertTrue(!status.IsClientConnected, "closed communication should not report a connected client");
        communication.Close();
    }

    private static void TestLearningCommunicationRestartAfterClose()
    {
        int port = GetAvailableTcpPort();
        using var communication = new CCommunicationLearning(startListen: false, port: port);

        AssertTrue(communication.Start(), "initial TCP listener did not start");
        PythonCommunicationStatus status = communication.GetStatusSnapshot();
        AssertTrue(status.IsListening, "initial communication should report a listening TCP listener");
        AssertEqual("listening", status.LastWorkerState);

        communication.Close();
        status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed communication should not report a listening TCP listener before restart");
        AssertEqual("stopped", status.LastWorkerState);

        AssertTrue(WaitUntil(() => communication.Start(), TimeSpan.FromSeconds(2)), "TCP listener did not restart after close");

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        AssertTrue(WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(2)), "client did not connect to restarted listener");

        communication.Close();
        status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed restarted communication should not report a listening TCP listener");
        AssertTrue(!status.IsClientConnected, "closed restarted communication should not report a connected client");
    }

    private static void TestLearningCommunicationDetectionRoundTrip()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        int port = GetAvailableTcpPort();
        string root = CreateTempRoot();
        DetectionResultApplicationService service = CGlobal.Inst.DetectionResults;
        EventHandler<DetectionCandidatesUpdatedEventArgs> candidateHandler = null;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        using var communication = new CCommunicationLearning(startListen: false, port: port);
        try
        {
            AssertTrue(communication.Start(), "test TCP listener did not start");

            var data = new CData
            {
                LastSelectImageName = "tcp-round-trip",
                LastSelectImagePath = Path.Combine(Path.GetTempPath(), "tcp-round-trip.bmp")
            };
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ProjectSettings.PythonModel.WeightsPath = Path.Combine(root, "best.pt");
            File.WriteAllText(data.ProjectSettings.PythonModel.WeightsPath, "");
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.Red });
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);
            service.ApplyToDetectLayer(Array.Empty<DefectInfo>());

            using var receivedRequest = new ManualResetEventSlim(false);
            Task mockClient = Task.Run(() => RunMockDetectionClient(port, receivedRequest));
            AssertTrue(WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(5)), "mock client did not connect to listener");

            int candidateEventCount = -1;
            candidateHandler = (_, e) => Volatile.Write(ref candidateEventCount, e.CandidateCount);
            service.DetectionCandidatesUpdated += candidateHandler;

            AssertTrue(service.TrySendCurrentImageForDetection(communication), "StartDefect packet was not sent");
            AssertTrue(receivedRequest.Wait(TimeSpan.FromSeconds(5)), "mock client did not receive StartDefect");
            AssertTrue(WaitUntil(() => service.GetLastDefects().Count == 1, TimeSpan.FromSeconds(5)), "ResultDefect was not applied");
            AssertTrue(WaitUntil(() => candidateEventCount == 1, TimeSpan.FromSeconds(2)), "detection candidate event was not raised");

            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data, minimumConfidence: 0.5F);
            FormLayerDisplay mainDisplay = CDisplayManager.GetMainDisplayOrNull();

            AssertEqual(1, candidates.Count);
            AssertEqual("NG", candidates[0].ClassName);
            AssertTrue(candidates[0].IsConfirmable, "mock detection candidate should be confirmable");
            AssertEqual(1, mainDisplay.GetDetectionOverlays().Count);
            AssertTrue(service.CanCommitLastDetection(data, minimumConfidence: 0.5F), "TCP detection result should be confirmable");
            AssertTrue(service.SelectDetectionCandidate(1, data), "TCP detection candidate was not selectable");
            AssertTrue(service.CommitLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F), "TCP detection candidate was not confirmed");
            AssertTrue(WaitUntil(() => service.GetLastDefects().Count == 0, TimeSpan.FromSeconds(2)), "confirmed TCP detection result was not cleared");

            IReadOnlyList<LabelingRoiListItem> committedItems = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "tcp-round-trip.txt");
            AssertEqual(1, committedItems.Count);
            AssertEqual("NG", committedItems[0].ClassName);
            AssertEqual(new Rectangle(4, 5, 12, 9), committedItems[0].Roi);
            AssertEqual(0, mainDisplay.GetDetectionOverlays().Count);
            AssertTrue(File.Exists(labelPath), "TCP detection label file was not saved");
            AssertEqual(1, File.ReadAllLines(labelPath).Length);
            AssertTrue(mockClient.Wait(TimeSpan.FromSeconds(5)), "mock client did not finish");
            if (mockClient.IsFaulted)
            {
                if (mockClient.Exception != null)
                {
                    throw mockClient.Exception;
                }

                throw new InvalidOperationException("mock client failed");
            }
        }
        finally
        {
            if (candidateHandler != null)
            {
                service.DetectionCandidatesUpdated -= candidateHandler;
            }

            CGlobal.Inst.DetectionResults.ApplyToDetectLayer(Array.Empty<DefectInfo>());
            communication.Close();
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestRealYoloDetectionWorkflowSmoke()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        int port = GetAvailableTcpPort();
        int timeoutSeconds = GetEnvironmentInt("LABELING_SMOKE_TIMEOUT_SECONDS", 240);
        string artifactRoot = CreateRealYoloSmokeArtifactRoot();
        string datasetRoot = Path.Combine(artifactRoot, "dataset");
        Directory.CreateDirectory(datasetRoot);

        RealYoloSmokeSettings smokeSettings = BuildRealYoloSmokeSettings(artifactRoot);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        DetectionResultApplicationService service = CGlobal.Inst.DetectionResults;
        EventHandler<DetectionCandidatesUpdatedEventArgs> candidateHandler = null;
        Process pythonProcess = null;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        using var communication = new CCommunicationLearning(startListen: false, port: port);
        try
        {
            AssertRealYoloSmokeInputs(smokeSettings);
            AssertTrue(communication.Start(), "real YOLO smoke TCP listener did not start");

            var data = new CData
            {
                LastSelectImageName = Path.GetFileNameWithoutExtension(smokeSettings.ImagePath),
                LastSelectImagePath = smokeSettings.ImagePath
            };
            data.ConfigureOutputRoot(datasetRoot);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ProjectSettings.PythonModel.PythonExecutablePath = smokeSettings.PythonExecutablePath;
            data.ProjectSettings.PythonModel.ProjectRootPath = smokeSettings.ProjectRootPath;
            data.ProjectSettings.PythonModel.ClientScriptPath = smokeSettings.ClientScriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = smokeSettings.WeightsPath;
            data.ProjectSettings.PythonModel.ImageRootPath = smokeSettings.ImageRootPath;
            data.ProjectSettings.PythonModel.MinimumDetectionConfidence = smokeSettings.MinimumConfidence;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.LimeGreen });
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.OrangeRed });
            CGlobal.Inst.Data = data;

            using CvMat sourceMat = OpenCvSharp.Cv2.ImRead(smokeSettings.ImagePath);
            AssertTrue(sourceMat != null && !sourceMat.Empty(), $"smoke image could not be loaded by OpenCV: {smokeSettings.ImagePath}");
            CDisplayManager.ImageSrc = sourceMat.Clone();

            using var image = new Bitmap(smokeSettings.ImagePath);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);

            int candidateEventCount = -1;
            candidateHandler = (_, e) => candidateEventCount = e.CandidateCount;
            service.DetectionCandidatesUpdated += candidateHandler;

            pythonProcess = StartRealYoloClient(smokeSettings, port, timeoutSeconds, stdout, stderr);
            bool started = CGlobal.Inst.DetectionWorkflow.TryStartCurrentImageDetection(
                data,
                communication,
                service,
                () => WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(timeoutSeconds)));

            AssertTrue(started, BuildRealYoloSmokeFailure("real YOLO StartDefect request was not sent", stdout, stderr));
            AssertTrue(
                WaitUntil(
                    () =>
                        Volatile.Read(ref candidateEventCount) > 0 &&
                        service.GetLastCandidateReviewItems(data, smokeSettings.MinimumConfidence).Count > 0,
                    TimeSpan.FromSeconds(timeoutSeconds)),
                BuildRealYoloSmokeFailure($"real YOLO ResultDefect did not produce candidates and event. EventCount:{Volatile.Read(ref candidateEventCount)}", stdout, stderr));

            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data, smokeSettings.MinimumConfidence);
            FormLayerDisplay mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            AssertTrue(mainDisplay != null, "Main display was not available after real YOLO detection");
            AssertTrue(mainDisplay.GetDetectionOverlays().Count > 0, "real YOLO candidates were not rendered as OpenGL overlays");
            AssertTrue(service.CanCommitLastDetection(data, smokeSettings.MinimumConfidence), "real YOLO candidates were not confirmable");
            AssertTrue(service.CommitAllLastDetectionToMainLabels(data, new CSystem(), smokeSettings.MinimumConfidence), "real YOLO candidates were not confirmed into labels");
            AssertTrue(WaitUntil(() => service.GetLastDefects().Count == 0, TimeSpan.FromSeconds(2)), "confirmed real YOLO detection result was not cleared");
            AssertEqual(0, mainDisplay.GetDetectionOverlays().Count);

            IReadOnlyList<LabelingRoiListItem> committedItems = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(datasetRoot, "data", "train", "labels", $"{data.LastSelectImageName}.txt");
            AssertTrue(committedItems.Count > 0, "real YOLO confirmed candidates did not create Main ROI labels");
            AssertTrue(File.Exists(labelPath), $"real YOLO label file was not saved: {labelPath}");
            AssertTrue(File.ReadAllLines(labelPath).Length > 0, $"real YOLO label file was empty: {labelPath}");
            string reviewStatusPath = VerifyRealYoloReviewStatus(data, smokeSettings.ImagePath, image.Size);

            WriteRealYoloSmokeSummary(artifactRoot, smokeSettings, candidates, committedItems, labelPath, reviewStatusPath, stdout, stderr);
            AssertRealYoloClientExitedCleanly(pythonProcess, stdout, stderr);
        }
        finally
        {
            if (candidateHandler != null)
            {
                service.DetectionCandidatesUpdated -= candidateHandler;
            }

            StopRealYoloClient(pythonProcess);
            WriteRealYoloProcessLog(artifactRoot, stdout, stderr);
            CGlobal.Inst.DetectionResults.CancelPendingDetection();
            communication.Close();
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestPythonModelSettingsDefaults()
    {
        var settings = new PythonModelSettings
        {
            ProjectRootPath = "",
            ClientScriptPath = "",
            WeightsPath = ""
        };

        settings.EnsureDefaults();

        AssertEqual(PythonModelSettings.GetDefaultProjectRootPath(), settings.ProjectRootPath);
        AssertEqual(Path.Combine(settings.ProjectRootPath, "labelling_tcp_client.py"), settings.ClientScriptPath);
        AssertEqual(Path.Combine(settings.ProjectRootPath, "best.pt"), settings.WeightsPath);
        AssertEqual(PythonModelSettings.GetDefaultImageRootPath(), settings.ImageRootPath);
        AssertEqual(0.25F, settings.MinimumDetectionConfidence);
        AssertEqual(30, settings.DetectionTimeoutSeconds);
        AssertTrue(settings.AutoStartClient, "YOLOv5 client auto-start should be enabled by default");
    }

    private static void TestPythonModelSettingsValidator()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            File.WriteAllText(scriptPath, "print('ready')");

            var settings = new PythonModelSettings
            {
                ProjectRootPath = root,
                ClientScriptPath = scriptPath,
                WeightsPath = Path.Combine(root, "missing.pt")
            };

            PythonModelValidationResult trainingValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(trainingValidation.IsValid, trainingValidation.Summary);
            AssertTrue(trainingValidation.Warnings.Any(line => line.Contains("weight file")), "missing weights should be a training warning");

            PythonModelValidationResult detectionValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: true);
            AssertTrue(!detectionValidation.IsValid, "missing weights should block detection");
            AssertTrue(detectionValidation.Errors.Any(line => line.Contains("weight file")), "missing weights error was not reported");

            settings.MinimumDetectionConfidence = 1.2F;
            PythonModelValidationResult confidenceValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(!confidenceValidation.IsValid, "out-of-range confidence should be invalid");
            AssertTrue(confidenceValidation.Errors.Any(line => line.Contains("confidence")), "confidence error was not reported");

            settings.MinimumDetectionConfidence = 0.25F;
            settings.DetectionTimeoutSeconds = 0;
            PythonModelValidationResult timeoutValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(!timeoutValidation.IsValid, "out-of-range detection timeout should be invalid");
            AssertTrue(timeoutValidation.Errors.Any(line => line.Contains("timeout")), "detection timeout error was not reported");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloPythonClientStartInfo()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            string weightsPath = Path.Combine(root, "best.pt");
            string modelRootPath = Path.Combine(root, "yolov5Master");
            File.WriteAllText(scriptPath, "print('ready')");
            Directory.CreateDirectory(modelRootPath);

            var settings = new PythonModelSettings
            {
                ProjectRootPath = root,
                ClientScriptPath = scriptPath,
                WeightsPath = weightsPath,
                ImageRootPath = root,
                MinimumDetectionConfidence = 0.15F
            };

            AssertTrue(YoloPythonClientProcessService.TryCreateStartInfo(settings, out var startInfo, out string error), error);
            AssertEqual(root, startInfo.WorkingDirectory);
            AssertTrue(startInfo.CreateNoWindow, "Python client should start without opening a console window");
            AssertTrue(startInfo.ArgumentList.Contains(scriptPath), "client script was not passed to Python");
            AssertTrue(startInfo.ArgumentList.Contains("--retry"), "retry flag was not passed to Python");
            AssertTrue(startInfo.ArgumentList.Contains(weightsPath), "weights path was not passed to Python");
            AssertArgumentValue(startInfo.ArgumentList, "--model-root", modelRootPath);
            AssertArgumentValue(startInfo.ArgumentList, "--image-root", root);
            AssertArgumentValue(startInfo.ArgumentList, "--conf", "0.15");

            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string firstSignature, out error), error);
            settings.MinimumDetectionConfidence = 0.2F;
            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string changedConfidenceSignature, out error), error);
            AssertTrue(
                !string.Equals(firstSignature, changedConfidenceSignature, StringComparison.Ordinal),
                "confidence changes should change Python client start signature");

            settings.MinimumDetectionConfidence = 0.15F;
            settings.WeightsPath = Path.Combine(root, "other.pt");
            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string changedWeightsSignature, out error), error);
            AssertTrue(
                !string.Equals(firstSignature, changedWeightsSignature, StringComparison.Ordinal),
                "weight path changes should change Python client start signature");

            settings.ClientScriptPath = Path.Combine(root, "missing.py");
            AssertTrue(!YoloPythonClientProcessService.TryCreateStartInfo(settings, out _, out error), "missing client script was accepted");
            AssertTrue(error.Contains("not found"), "missing client script error was not descriptive");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDetectionWorkflowValidation()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            string weightsPath = Path.Combine(root, "best.pt");
            File.WriteAllText(scriptPath, "print('ready')");

            var data = new CData();
            data.ProjectSettings.PythonModel.ProjectRootPath = root;
            data.ProjectSettings.PythonModel.ClientScriptPath = scriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = Path.Combine(root, "missing.pt");
            data.ProjectSettings.PythonModel.ImageRootPath = root;

            using var communication = new CCommunicationLearning(startListen: false);
            var workflow = new YoloDetectionWorkflowService();
            bool ensureCalled = false;
            bool invalidStarted = workflow.TryStartCurrentImageDetection(
                data,
                communication,
                new DetectionResultApplicationService(),
                () =>
                {
                    ensureCalled = true;
                    return true;
                });

            AssertTrue(!invalidStarted, "detection workflow should reject missing weights");
            AssertTrue(!ensureCalled, "Python client should not be prepared when detection settings are invalid");

            File.WriteAllText(weightsPath, "");
            data.ProjectSettings.PythonModel.WeightsPath = weightsPath;
            bool readyFailureStarted = workflow.TryStartCurrentImageDetection(
                data,
                communication,
                new DetectionResultApplicationService(),
                () =>
                {
                    ensureCalled = true;
                    return false;
                });

            AssertTrue(!readyFailureStarted, "detection workflow should stop when the Python client is not ready");
            AssertTrue(ensureCalled, "Python client readiness was not checked for valid settings");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestPythonMessageFramer()
    {
        var framer = new PythonMessageFramer();

        IReadOnlyList<string> first = framer.Append("ResultDefect [{\"ClassName\":\"NG\",");
        AssertEqual(0, first.Count);

        IReadOnlyList<string> second = framer.Append("\"Confidence\":0.9,\"X\":1,\"Y\":2,\"Width\":3,\"Height\":4}]");
        AssertEqual(1, second.Count);
        AssertTrue(second[0].StartsWith("ResultDefect ", StringComparison.Ordinal), "split ResultDefect was not rebuilt");

        DetectionResultParseResult result = PythonDetectionResultProtocol.Parse(second[0]);
        AssertEqual(DetectionResultParseStatus.Parsed, result.Status);
        AssertEqual("NG", result.Defects[0].ClassName);

        var separatedFramer = new PythonMessageFramer();
        IReadOnlyList<string> separated = separatedFramer.Append("ResultDefect\n\n[{\"ClassName\":\"OK\",\"Confidence\":0.8,\"X\":1,\"Y\":2,\"Width\":3,\"Height\":4}]");
        AssertEqual(1, separated.Count);
        AssertEqual(DetectionResultParseStatus.Parsed, PythonDetectionResultProtocol.Parse(separated[0]).Status);

        IReadOnlyList<string> commands = framer.Append("StartTraining\nStopTraining\n");
        AssertEqual(2, commands.Count);
        AssertEqual("StartTraining", commands[0]);
        AssertEqual("StopTraining", commands[1]);

        var envelopeFramer = new PythonMessageFramer();
        IReadOnlyList<string> envelopeFirst = envelopeFramer.Append("{\"type\":\"ResultDefect\",\"items\":[");
        AssertEqual(0, envelopeFirst.Count);

        IReadOnlyList<string> envelopeSecond = envelopeFramer.Append("{\"className\":\"OK\",\"confidence\":0.7,\"x\":1,\"y\":2,\"width\":3,\"height\":4}]}");
        AssertEqual(1, envelopeSecond.Count);
        AssertEqual(DetectionResultParseStatus.Parsed, PythonDetectionResultProtocol.Parse(envelopeSecond[0]).Status);
    }

    private static void TestPythonModelStatusProtocol()
    {
        string statusJson = "{\"type\":\"TrainingStatus\",\"version\":1,\"state\":\"running\",\"message\":\"epoch\",\"progressPercent\":42,\"epoch\":2,\"totalEpochs\":5}";

        PythonModelStatusParseResult result = PythonModelStatusProtocol.Parse(statusJson);

        AssertEqual(PythonModelStatusParseStatus.Parsed, result.Status);
        AssertEqual("running", result.Message.State);
        AssertEqual(42, result.Message.ProgressPercent.Value);
        AssertEqual(2, result.Message.Epoch.Value);
        AssertEqual(5, result.Message.TotalEpochs.Value);
        AssertTrue(!result.Message.IsError, "running status should not be an error");

        string healthJson = "{\"type\":\"HealthCheckResult\",\"version\":1,\"requestId\":\"req-health\",\"ok\":true,\"state\":\"ready\",\"worker\":{\"pid\":123}}";
        PythonModelStatusParseResult health = PythonModelStatusProtocol.Parse(healthJson);
        AssertEqual(PythonModelStatusParseStatus.Parsed, health.Status);
        AssertEqual(PythonModelStatusProtocol.HealthCheckResultType, health.Message.Type);
        AssertEqual("ready", health.Message.State);
        AssertTrue(health.Message.Ok == true, "health check ok state was not parsed");

        string modelJson = "{\"type\":\"ModelStatusResult\",\"version\":1,\"requestId\":\"req-model\",\"ok\":true,\"model\":{\"state\":\"ready\",\"loaded\":true,\"weightsPath\":\"C:/Git/yolov5/best.pt\"}}";
        PythonModelStatusParseResult model = PythonModelStatusProtocol.Parse(modelJson);
        AssertEqual(PythonModelStatusParseStatus.Parsed, model.Status);
        AssertEqual(PythonModelStatusProtocol.ModelStatusResultType, model.Message.Type);
        AssertEqual("ready", model.Message.State);
        AssertTrue(model.Message.Loaded == true, "model loaded state was not parsed");

        PythonModelStatusParseResult ignored = PythonModelStatusProtocol.Parse("{\"type\":\"Other\"}");
        AssertEqual(PythonModelStatusParseStatus.NotStatus, ignored.Status);
    }

    private static void TestTcpReceiveQueueEncoding()
    {
        var tcp = new CTCPAsync
        {
            TextEncoding = Encoding.UTF8
        };

        tcp.listRcvData.Add(Encoding.UTF8.GetBytes("class-\uB370\uC774\uD130"));

        AssertTrue(tcp.GetStringData(out string message), "queued string data was not returned");
        AssertEqual("class-\uB370\uC774\uD130", message);
        AssertTrue(!tcp.GetStringData(out string _), "queue should be empty after dequeue");
    }

    private static void TestPythonDetectionResultProtocol()
    {
        string message = "ResultDefect [{\"ClassName\":\"NG\",\"Confidence\":0.91,\"X\":12.5,\"Y\":20,\"Width\":30,\"Height\":40}]";

        DetectionResultParseResult result = PythonDetectionResultProtocol.Parse(message);

        AssertTrue(result.IsDetectionResult, "ResultDefect message was not recognized");
        AssertEqual(DetectionResultParseStatus.Parsed, result.Status);
        AssertEqual(1, result.Defects.Count);
        AssertEqual("NG", result.Defects[0].ClassName);
        AssertEqual(12.5F, result.Defects[0].X);
        AssertEqual(40F, result.Defects[0].Height);

        string envelope = "{\"type\":\"ResultDefect\",\"version\":1,\"imageId\":\"sample-001\",\"items\":[{\"className\":\"OK\",\"confidence\":0.82,\"x\":3,\"y\":4,\"width\":5,\"height\":6}]}";
        DetectionResultParseResult envelopeResult = PythonDetectionResultProtocol.Parse(envelope);

        AssertEqual(DetectionResultParseStatus.Parsed, envelopeResult.Status);
        AssertEqual(1, envelopeResult.Defects.Count);
        AssertEqual("OK", envelopeResult.Defects[0].ClassName);
        AssertEqual(5F, envelopeResult.Defects[0].Width);
        AssertEqual("sample-001", envelopeResult.ImageId);

        string detectResult = "{\"type\":\"DetectImageResult\",\"version\":1,\"requestId\":\"req-123\",\"imageId\":\"sample-002\",\"ok\":true,\"candidates\":[{\"className\":\"NG\",\"confidence\":0.72,\"x\":7,\"y\":8,\"width\":9,\"height\":10}]}";
        DetectionResultParseResult detectParse = PythonDetectionResultProtocol.Parse(detectResult);
        AssertEqual(DetectionResultParseStatus.Parsed, detectParse.Status);
        AssertEqual("req-123", detectParse.RequestId);
        AssertEqual("sample-002", detectParse.ImageId);
        AssertEqual("NG", detectParse.Defects[0].ClassName);
        AssertEqual(9F, detectParse.Defects[0].Width);
    }

    private static void TestPythonDetectionResultProtocolFailures()
    {
        DetectionResultParseResult ignored = PythonDetectionResultProtocol.Parse("StartTraining");
        AssertEqual(DetectionResultParseStatus.NotDetectionResult, ignored.Status);
        AssertTrue(!ignored.IsDetectionResult, "non-detection command was recognized as detection");

        DetectionResultParseResult empty = PythonDetectionResultProtocol.Parse("ResultDefect ");
        AssertEqual(DetectionResultParseStatus.EmptyPayload, empty.Status);
        AssertTrue(empty.IsDetectionResult, "empty ResultDefect command should still be handled");

        DetectionResultParseResult invalid = PythonDetectionResultProtocol.Parse("ResultDefect { bad json");
        AssertEqual(DetectionResultParseStatus.InvalidPayload, invalid.Status);
        AssertTrue(!string.IsNullOrWhiteSpace(invalid.ErrorMessage), "invalid payload did not include an error message");

        DetectionResultParseResult envelopeError = PythonDetectionResultProtocol.Parse("{\"type\":\"ResultDefect\",\"version\":1,\"error\":\"weights missing\",\"items\":[]}");
        AssertEqual(DetectionResultParseStatus.InvalidPayload, envelopeError.Status);
        AssertTrue(envelopeError.ErrorMessage.Contains("weights missing"), "detection envelope error was not surfaced");

        DetectionResultParseResult structuredError = PythonDetectionResultProtocol.Parse("{\"type\":\"DetectImageResult\",\"version\":1,\"requestId\":\"req-bad\",\"imageId\":\"img-bad\",\"ok\":false,\"error\":{\"code\":\"DetectImageFailed\",\"message\":\"file missing\"},\"candidates\":[]}");
        AssertEqual(DetectionResultParseStatus.InvalidPayload, structuredError.Status);
        AssertEqual("req-bad", structuredError.RequestId);
        AssertTrue(structuredError.ErrorMessage.Contains("file missing"), "structured detection error was not surfaced");
    }

    private static void TestLabelingImageWorkspace()
    {
        var workspace = new LabelingImageWorkspace();
        using Bitmap image = new Bitmap(32, 24);

        workspace.SetActiveImage("part-001", @"C:\images\part-001.png", image);

        AssertEqual("part-001", workspace.ActiveImageName);
        AssertEqual(@"C:\images\part-001.png", workspace.ActiveImagePath);
        AssertTrue(ReferenceEquals(image, workspace.ActiveImage), "active image was not stored in ImageSpace");
        AssertTrue(ReferenceEquals(image, workspace.GetMainImage()), "main image was not stored in ImageSpace");
        AssertTrue(workspace.MainImageChanged, "main image changed flag was not set");

        workspace.AcceptMainImageChange();
        AssertTrue(!workspace.MainImageChanged, "main image changed flag was not accepted");
    }

    private static void TestAppLog()
    {
        AppLog.NORMAL("Smoke log {0}", 1);
        AppLog.ABNORMAL("Smoke error {0}", 2);
        AppLog.COMM("Smoke communication {0}", 3);
    }

    private static void TestScreenCapturePath()
    {
        string root = CreateTempRoot();
        try
        {
            string path = ScreenCaptureService.CreateCaptureFilePath(
                root,
                "Main:Window?",
                new DateTime(2026, 6, 17, 10, 58, 10));

            AssertTrue(Directory.Exists(Path.Combine(root, "CAPTURE")), "capture directory was not created");
            AssertEqual("Main_Window__20260617_105810.jpeg", Path.GetFileName(path));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestImageListSupportedFiles()
    {
        string root = CreateTempRoot();
        try
        {
            using (Bitmap image = new Bitmap(4, 3))
            {
                image.Save(Path.Combine(root, "b.PNG"), System.Drawing.Imaging.ImageFormat.Png);
                image.Save(Path.Combine(root, "a.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(Path.Combine(root, "c.tiff"), System.Drawing.Imaging.ImageFormat.Tiff);
            }

            File.WriteAllText(Path.Combine(root, "ignore.txt"), "not an image");

            using var form = new FormImageList();
            List<string> files = form.GetImageFiles(root);

            AssertEqual(3, files.Count);
            AssertEqual("a.jpg", Path.GetFileName(files[0]));
            AssertEqual("b.PNG", Path.GetFileName(files[1]));
            AssertEqual("c.tiff", Path.GetFileName(files[2]));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestFormImageListDatasetCommandBar()
    {
        using var form = new FormImageList
        {
            Size = new Size(340, 720)
        };

        _ = form.Handle;
        Application.DoEvents();

        var toolStripContainer = GetPrivateField<ToolStripContainer>(form, "toolStripContainer1");
        var legacyToolStrip = GetPrivateField<ToolStrip>(form, "toolStrip1");
        var datasetCommandPanel = GetPrivateField<Panel>(form, "datasetCommandPanel");
        var datasetCommandFlow = GetPrivateField<FlowLayoutPanel>(form, "datasetCommandFlow");
        var batchStatusPanel = GetPrivateField<Panel>(form, "batchStatusPanel");
        var batchStatusTitleLabel = GetPrivateField<Label>(form, "batchStatusTitleLabel");
        var batchStatusDetailLabel = GetPrivateField<Label>(form, "batchStatusDetailLabel");
        var batchProgressTrack = GetPrivateField<Panel>(form, "batchProgressTrack");
        var batchProgressFill = GetPrivateField<Panel>(form, "batchProgressFill");
        var btnDetectSelected = GetPrivateField<Control>(form, "btnDetectSelected");
        var btnDetectBatch = GetPrivateField<Control>(form, "btnDetectBatch");
        var btnStopBatchDetection = GetPrivateField<Control>(form, "btnStopBatchDetection");
        var btnReviewFilter = GetPrivateField<Control>(form, "btnReviewFilter");
        var btnThumbnailSize = GetPrivateField<Control>(form, "btnThumbnailSize");
        var reviewFilterMenu = GetPrivateField<ContextMenuStrip>(form, "reviewFilterMenu");
        var imageGridView = GetPrivateField<DataGridView>(form, "imageGridView");

        InvokePrivate(form, "ConfigureImageGrid");
        InvokePrivate(form, "ApplyResponsiveLayout");
        AssertTrue(toolStripContainer != null, "ToolStripContainer was not available");
        AssertTrue(legacyToolStrip != null, "legacy ToolStrip was not available");
        AssertTrue(datasetCommandPanel != null, "dataset command panel was not created");
        AssertTrue(datasetCommandFlow != null, "dataset command flow was not created");
        AssertTrue(batchStatusPanel != null, "batch detection progress strip was not created");
        AssertTrue(batchStatusTitleLabel != null, "batch detection progress title was not created");
        AssertTrue(batchStatusDetailLabel != null, "batch detection progress detail was not created");
        AssertTrue(batchProgressTrack != null, "batch detection progress track was not created");
        AssertTrue(batchProgressFill != null, "batch detection progress fill was not created");
        AssertEqual(DockStyle.Top, datasetCommandPanel.Dock);
        AssertEqual(DockStyle.Top, batchStatusPanel.Dock);
        AssertTrue(!batchStatusPanel.Visible, "batch detection progress strip should be hidden while idle");
        AssertTrue(!toolStripContainer.TopToolStripPanelVisible, "legacy ToolStrip top panel should be hidden");
        AssertTrue(!legacyToolStrip.Visible, "legacy ToolStrip should not be visible");
        AssertTrue(!datasetCommandFlow.Controls.Contains(btnDetectSelected), "image queue should not host the single-image detection command");
        AssertTrue(!datasetCommandFlow.Controls.Contains(btnDetectBatch), "image queue should not host the batch detection command");
        AssertTrue(!datasetCommandFlow.Controls.Contains(btnStopBatchDetection), "image queue should not host the batch stop command");
        AssertTrue(datasetCommandFlow.Controls.Contains(btnReviewFilter), "filter command button was not hosted");
        AssertTrue(datasetCommandFlow.Controls.Contains(btnThumbnailSize), "thumbnail size command button was not hosted");
        AssertTrue(reviewFilterMenu != null && reviewFilterMenu.Items.Count > 0, "review filter menu was not initialized");
        AssertEqual("\uD30C\uC77C", imageGridView.Columns["NameColumn"].HeaderText);
        AssertEqual("\uB77C\uBCA8", imageGridView.Columns["LabelStatusColumn"].HeaderText);
        AssertEqual("AI", imageGridView.Columns["DetectStatusColumn"].HeaderText);
        AssertEqual("YOLO \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC", imageGridView.Columns["LabelStatusColumn"].ToolTipText);
        AssertEqual("AI \uAC80\uCD9C \uD6C4\uBCF4 \uC0C1\uD0DC", imageGridView.Columns["DetectStatusColumn"].ToolTipText);

        AssertEqual("\uC5C6\uC74C", InvokePrivateResult<string>(form, "FormatLabelStatusForGrid", "No Label"));
        AssertEqual("\uBE48\uAC12", InvokePrivateResult<string>(form, "FormatLabelStatusForGrid", "Empty Label"));
        AssertEqual("2\uAC1C / \uC624\uB958 1", InvokePrivateResult<string>(form, "FormatLabelStatusForGrid", "Label 2 / Invalid 1"));

        var reviewStatusService = GetPrivateField<YoloImageReviewStatusService>(form, "imageReviewStatus");
        YoloImageReviewStatus candidateStatus = reviewStatusService.SetDetectionCandidates(@"C:\images\a.jpg", "a", 3);
        YoloImageReviewStatus requestedStatus = reviewStatusService.SetDetectionRequested(@"C:\images\b.jpg", "b");
        YoloImageReviewStatus failedStatus = reviewStatusService.SetDetectionFailed(@"C:\images\c.jpg", "c", "Detection failed.");
        YoloImageReviewStatus idleStatus = reviewStatusService.GetOrCreate(@"C:\images\d.jpg");

        AssertEqual("\uD6C4\uBCF4 3", InvokePrivateResult<string>(form, "FormatDetectionStatusForGrid", candidateStatus));
        AssertEqual("\uC694\uCCAD\uC911", InvokePrivateResult<string>(form, "FormatDetectionStatusForGrid", requestedStatus));
        AssertEqual("\uC2E4\uD328", InvokePrivateResult<string>(form, "FormatDetectionStatusForGrid", failedStatus));
        AssertEqual("\uB300\uAE30", InvokePrivateResult<string>(form, "FormatDetectionStatusForGrid", idleStatus));

        string root = CreateTempRoot();
        CData previousData = CGlobal.Inst.Data;
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.EnsureYoloOutputDirectories();
            CGlobal.Inst.Data = data;

            string sourceDirectory = Path.Combine(root, "source");
            Directory.CreateDirectory(sourceDirectory);
            string imagePath = Path.Combine(sourceDirectory, "confirmed.png");
            using (var bitmap = new Bitmap(20, 20))
            {
                bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            InvokePrivateResult<object>(form, "ShowImageDgv", new List<string> { imagePath });
            string labelPath = Path.Combine(root, "data", "train", "labels", "confirmed.txt");
            File.WriteAllText(labelPath, "0 0.5 0.5 0.25 0.25");

            InvokePrivateResult<object>(
                form,
                "RefreshDetectionCandidateStatus",
                new DetectionCandidatesUpdatedEventArgs(
                    "confirmed",
                    imagePath,
                    0,
                    DetectionCandidateUpdateReason.CandidatesConfirmed));

            YoloImageReviewStatus confirmedStatus = reviewStatusService.GetOrCreate(imagePath);
            AssertEqual("Confirmed", confirmedStatus.DetectionText);
            AssertEqual("Label 1", confirmedStatus.LabelText);
            AssertEqual(1, confirmedStatus.LabelStatus.ObjectCount);
        }
        finally
        {
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }

        AssertEqual(
            "이미지 12 / 라벨 4",
            InvokePrivateStaticResult<string>(typeof(FormImageList), "BuildListStatusText", 12, 12, 4, GetNestedEnumValue<FormImageList>("ImageReviewFilter", "All"), false, 0, 0, ""));
        AssertEqual(
            "이미지 3/12 / 라벨 4 / 필터 후보 / 일괄 검출 2/5 표시 행",
            InvokePrivateStaticResult<string>(typeof(FormImageList), "BuildListStatusText", 3, 12, 4, GetNestedEnumValue<FormImageList>("ImageReviewFilter", "Candidate"), true, 2, 5, "표시 행"));

        batchProgressTrack.Width = 100;
        SetPrivateField(form, "isBatchDetectionRunning", true);
        SetPrivateField(form, "batchDetectionTotalCount", 5);
        SetPrivateField(form, "batchDetectionCompletedCount", 2);
        SetPrivateField(form, "batchDetectionCurrentPath", @"C:\images\a.jpg");
        SetPrivateField(form, "batchDetectionScopeText", "표시 행");
        InvokePrivate(form, "UpdateBatchStatusPanel");

        AssertTrue(batchStatusPanel.Parent != null, "batch detection progress strip should stay hosted in the dataset panel");
        AssertEqual("3/5", batchStatusTitleLabel.Text);
        AssertTrue(batchStatusDetailLabel.Text.Contains("a.jpg"), "batch progress detail should include the current image");
        AssertTrue(batchProgressFill.Width > 0, "batch progress fill should reflect completed work");

        SetPrivateField(form, "isBatchDetectionRunning", false);
        InvokePrivate(form, "UpdateBatchStatusPanel");
        AssertEqual("일괄 검출", batchStatusTitleLabel.Text);
        AssertEqual(string.Empty, batchStatusDetailLabel.Text);
        AssertEqual(0, batchProgressFill.Width);
    }

    private static void TestFormImageListLargeQueuePerformance()
    {
        string root = CreateTempRoot();
        try
        {
            byte[] imageBytes;
            using (var bitmap = new Bitmap(8, 8))
            using (var stream = new MemoryStream())
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(80, 80, 80));
                    graphics.FillEllipse(Brushes.LightGray, 1, 1, 6, 6);
                }

                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                imageBytes = stream.ToArray();
            }

            var paths = new List<string>(capacity: 1000);
            for (int i = 0; i < 1000; i++)
            {
                string path = Path.Combine(root, $"queue_{i:0000}.png");
                File.WriteAllBytes(path, imageBytes);
                paths.Add(path);
            }

            using var form = new FormImageList
            {
                Size = new Size(360, 720)
            };
            _ = form.Handle;
            Application.DoEvents();

            Stopwatch stopwatch = Stopwatch.StartNew();
            InvokePrivateResult<object>(form, "ShowImageDgv", paths);
            stopwatch.Stop();

            var imageGridView = GetPrivateField<DataGridView>(form, "imageGridView");
            AssertEqual(1000, imageGridView.Rows.Count);
            AssertTrue(stopwatch.ElapsedMilliseconds < 5000, $"1000 image shell queue loading took too long: {stopwatch.ElapsedMilliseconds}ms");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestFormMainFrameLabelingCommandBar()
    {
        using var form = new FormMainFrame(null)
        {
            WindowState = FormWindowState.Normal,
            Size = new Size(1600, 900)
        };

        _ = form.Handle;
        form.WindowState = FormWindowState.Normal;
        form.ClientSize = new Size(1600, 900);
        form.PerformLayout();
        InvokePrivate(form, "ApplyMainTheme");
        InvokePrivate(form, "ApplyResponsiveLayout");
        Application.DoEvents();

        var classSelectorPanel = GetPrivateField<Panel>(form, "classSelectorPanel");
        var workspaceModePanel = GetPrivateField<Panel>(form, "workspaceModePanel");
        var workflowCommandPanel = GetPrivateField<Panel>(form, "workflowCommandPanel");
        var utilityCommandPanel = GetPrivateField<Panel>(form, "utilityCommandPanel");
        var outputPathPanel = GetPrivateField<Panel>(form, "outputPathPanel");
        var cbClassMenu = GetPrivateField<Control>(form, "cbClassMenu");
        var btnClassSettings = GetPrivateField<Control>(form, "btnClassSettings");
        var btnDetectCurrentImage = GetPrivateField<Control>(form, "btnDetectCurrentImage");
        var btnAcceptDetection = GetPrivateField<Control>(form, "btnAcceptDetection");
        var btnSaveProject = GetPrivateField<Control>(form, "btnSaveProject");
        var btnYoloConnect = GetPrivateField<Control>(form, "btnYoloConnect");
        var btnYoloStop = GetPrivateField<Control>(form, "btnYoloStop");
        var btnStartTraining = GetPrivateField<Control>(form, "btnStartTraining");
        var btnUserOptions = GetPrivateField<Control>(form, "btnUserOptions");
        var btnScreenCapture = GetPrivateField<Control>(form, "btnScreenCapture");
        var btnTeachingWorkspace = GetPrivateField<Control>(form, "btnTeachingWorkspace");
        var btnDetectionReviewWorkspace = GetPrivateField<Control>(form, "btnDetectionReviewWorkspace");
        var btnTrainingWorkspace = GetPrivateField<Control>(form, "btnTrainingWorkspace");
        var lblClassSelector = GetPrivateField<Control>(form, "lblClassSelector");
        var captureFolderItem = GetPrivateField<ToolStripItem>(form, "iconMenuItem1");

        AssertTrue(classSelectorPanel != null, "class selector command group was not created");
        AssertTrue(workspaceModePanel != null, "workspace mode command group was not created");
        AssertTrue(workflowCommandPanel != null, "workflow command group was not created");
        AssertTrue(utilityCommandPanel != null, "utility command group was not created");
        AssertTrue(workspaceModePanel.Controls.Contains(btnTeachingWorkspace), "teaching workspace button was not hosted in the workspace mode group");
        AssertTrue(workspaceModePanel.Controls.Contains(btnDetectionReviewWorkspace), "detection review workspace button was not hosted in the workspace mode group");
        AssertTrue(workspaceModePanel.Controls.Contains(btnTrainingWorkspace), "training workspace button was not hosted in the workspace mode group");
        AssertTrue(classSelectorPanel.Controls.Contains(cbClassMenu), "class combo was not hosted in the class command group");
        AssertTrue(classSelectorPanel.Controls.Contains(btnClassSettings), "class settings button was not hosted in the class command group");
        AssertTrue(workflowCommandPanel.Controls.Contains(btnDetectCurrentImage), "detect button was not hosted in the workflow command group");
        AssertTrue(workflowCommandPanel.Controls.Contains(btnAcceptDetection), "accept button was not hosted in the workflow command group");
        AssertTrue(workflowCommandPanel.Controls.Contains(btnSaveProject), "save button was not hosted in the workflow command group");
        AssertTrue(utilityCommandPanel.Controls.Contains(btnYoloConnect), "YOLO connect button was not hosted in the utility command group");
        AssertTrue(utilityCommandPanel.Controls.Contains(btnYoloStop), "YOLO stop button was not hosted in the utility command group");
        AssertTrue(utilityCommandPanel.Controls.Contains(btnStartTraining), "training button was not hosted in the utility command group");
        AssertTrue(utilityCommandPanel.Controls.Contains(btnUserOptions), "model settings button was not hosted in the utility command group");
        AssertTrue(utilityCommandPanel.Controls.Contains(btnScreenCapture), "capture button was not hosted in the utility command group");
        AssertEqual("AI 검출", btnDetectCurrentImage.Text);
        AssertEqual("후보 확정", btnAcceptDetection.Text);
        AssertEqual("YOLO", btnYoloConnect.Text);
        AssertEqual("중지", btnYoloStop.Text);
        AssertEqual("모델", btnUserOptions.Text);
        AssertEqual("클래스 메뉴", lblClassSelector.Text);
        AssertEqual("캡처 폴더 열기", captureFolderItem.Text);
        AssertEqual("데이터 경로 미설정", InvokePrivateStaticResult<string>(typeof(FormMainFrame), "FormatCompactPath", ""));
        AssertTrue(outputPathPanel.Width >= 180, "data path panel should remain available on a desktop width");
        AssertTrue(workflowCommandPanel.Right < utilityCommandPanel.Left, "workflow commands should be grouped before utility commands");
    }

    private static void TestFormMainFrameTrainingStatusUi()
    {
        using var form = new FormMainFrame(null)
        {
            WindowState = FormWindowState.Normal,
            Size = new Size(1600, 900)
        };

        _ = form.Handle;
        InvokePrivate(form, "ApplyMainTheme");
        Application.DoEvents();

        var runningStatus = new PythonCommunicationStatus
        {
            LastTrainingState = "running",
            LastTrainingMessage = "epoch update",
            LastTrainingProgressPercent = 42,
            LastTrainingEpoch = 2,
            LastTrainingTotalEpochs = 5,
            LastTrainingStatusAtUtc = DateTime.UtcNow
        };

        AssertTrue(InvokePrivateStaticResult<bool>(typeof(FormMainFrame), "IsTrainingActive", runningStatus), "running training status was not considered active");
        string runningText = InvokePrivateStaticResult<string>(
            typeof(FormMainFrame),
            "BuildTrainingStatusText",
            runningStatus,
            null,
            DateTime.UtcNow);
        AssertTrue(runningText.Contains("42") && runningText.Contains("2/5"), "training status text should include progress and epoch");

        InvokePrivateResult<object>(form, "UpdateTrainingStatus", runningStatus);
        var btnStartTraining = GetPrivateField<Control>(form, "btnStartTraining");
        var lbTrainingStatus = GetPrivateField<Control>(form, "lbTrainingStatus");
        AssertEqual("중지", btnStartTraining.Text);
        AssertTrue(lbTrainingStatus.Text.Contains("42"), "training status label should show progress");

        var completedStatus = new PythonCommunicationStatus
        {
            LastTrainingState = "completed",
            LastTrainingProgressPercent = 100,
            LastTrainingStatusAtUtc = DateTime.UtcNow
        };

        AssertTrue(!InvokePrivateStaticResult<bool>(typeof(FormMainFrame), "IsTrainingActive", completedStatus), "completed training status was considered active");
        InvokePrivateResult<object>(form, "UpdateTrainingStatus", completedStatus);
        AssertEqual("학습", btnStartTraining.Text);
    }

    private static void TestFormTrainingPanelPythonWorkerControls()
    {
        using var panel = new FormTrainingPanel
        {
            Size = new Size(380, 620)
        };

        _ = panel.Handle;
        Application.DoEvents();

        var lblTitle = GetPrivateField<Control>(panel, "lblTitle");
        var lblPythonState = GetPrivateField<Control>(panel, "lblPythonState");
        var lblPythonDetail = GetPrivateField<Control>(panel, "lblPythonDetail");
        var btnRefresh = GetPrivateField<Control>(panel, "btnRefresh");
        var btnHealthCheck = GetPrivateField<Control>(panel, "btnHealthCheck");
        var btnModelStatus = GetPrivateField<Control>(panel, "btnModelStatus");
        var btnRestartPython = GetPrivateField<Control>(panel, "btnRestartPython");
        var btnStopPython = GetPrivateField<Control>(panel, "btnStopPython");
        var btnOpenModelSettings = GetPrivateField<Control>(panel, "btnOpenModelSettings");

        AssertEqual("학습 준비", panel.Text);
        AssertEqual("학습 준비", lblTitle.Text);
        AssertEqual("새로고침", btnRefresh.Text);
        AssertEqual("진단", btnHealthCheck.Text);
        AssertEqual("모델", btnModelStatus.Text);
        AssertEqual("재시작", btnRestartPython.Text);
        AssertEqual("종료", btnStopPython.Text);
        AssertEqual("설정", btnOpenModelSettings.Text);
        AssertTrue(lblPythonState.Text.Contains("Python") || lblPythonState.Text.Contains("모델"), "python state should be operator-readable");
        AssertTrue(lblPythonDetail.Text.Contains("Listener") && lblPythonDetail.Text.Contains("TCP"), "python detail should show listener and connection state");
        AssertTrue(btnHealthCheck.Top == btnRefresh.Top, "worker command buttons should be grouped in the first command row");
        AssertTrue(btnRestartPython.Top > btnRefresh.Top, "restart/stop controls should be placed in a second command row on compact panels");
    }

    private static void TestAuxiliaryLabelingUiText()
    {
        using var log = new FormLog();
        AssertEqual("로그", log.Text);

        using var classMenu = new FormVision_ClassMenu();
        var deleteButton = GetPrivateField<Control>(classMenu, "btnDelete");
        var classLabel = GetPrivateField<Control>(classMenu, "rjLabel1");
        var pathLabel = GetPrivateField<Control>(classMenu, "rjLabel2");
        var classColumn = GetPrivateField<DataGridViewColumn>(classMenu, "Column1");

        AssertEqual("클래스 설정", classMenu.Text);
        AssertEqual("삭제", deleteButton.Text);
        AssertEqual("클래스", classLabel.Text);
        AssertEqual("저장 경로", pathLabel.Text);
        AssertEqual("클래스", classColumn.HeaderText);
    }

    private static void TestYoloModelSettingsUiText()
    {
        using var form = new RJCodeUI_M1.RJForms.FormVision_Yolov5ParamSetting();

        var settingsTabs = GetPrivateField<TabControl>(form, "settingsTabs");
        var btnApplyChanges = GetPrivateField<Control>(form, "btnApplyChanges");
        var closeButton = GetPrivateField<Control>(form, "rjButton1");
        var chkPythonAutoStart = GetPrivateField<CheckBox>(form, "chkPythonAutoStart");
        var lblPythonValidationStatus = GetPrivateField<Control>(form, "lblPythonValidationStatus");
        var tbPythonDetectionTimeoutSeconds = GetPrivateField<Control>(form, "tbPythonDetectionTimeoutSeconds");

        AssertEqual("모델 설정", form.Text);
        AssertTrue(settingsTabs != null && settingsTabs.TabPages.Count == 2, "model settings tabs were not created");
        AssertEqual("학습 설정", settingsTabs.TabPages[0].Text);
        AssertEqual("검출 연동", settingsTabs.TabPages[1].Text);
        AssertEqual("설정 저장", btnApplyChanges.Text);
        AssertEqual("닫기", closeButton.Text);
        AssertEqual("검출 요청 시 Python 클라이언트 자동 시작", chkPythonAutoStart.Text);
        AssertTrue(lblPythonValidationStatus != null, "python validation status label was not created");
        AssertTrue(tbPythonDetectionTimeoutSeconds != null, "python detection timeout textbox was not created");
        AssertTrue(ContainsControlText(settingsTabs.TabPages[1], "Python 검출 연동"), "python detection settings title was not created");
        AssertTrue(ContainsControlText(settingsTabs.TabPages[1], "YOLOv5 프로젝트"), "project root label was not localized");
        AssertTrue(ContainsControlText(settingsTabs.TabPages[1], "통신 스크립트"), "client script label was not localized");
        AssertTrue(ContainsControlText(settingsTabs.TabPages[1], "검출 가중치"), "weights label was not localized");
        AssertTrue(ContainsControlText(settingsTabs.TabPages[1], "확정 기준 신뢰도 (0~1)"), "confidence label was not localized");

        string errorStatus = InvokePrivateStaticResult<string>(
            typeof(RJCodeUI_M1.RJForms.FormVision_Yolov5ParamSetting),
            "BuildPythonValidationStatusText",
            new PythonModelValidationResult(
                new[] { "YOLOv5 TCP client script was not found: missing.py" },
                Array.Empty<string>()));
        AssertTrue(errorStatus.Contains("확인 필요") && errorStatus.Contains("통신 스크립트"), "python validation error was not translated for operators");

        string warningStatus = InvokePrivateStaticResult<string>(
            typeof(RJCodeUI_M1.RJForms.FormVision_Yolov5ParamSetting),
            "BuildPythonValidationStatusText",
            new PythonModelValidationResult(
                Array.Empty<string>(),
                new[] { "YOLOv5 weight file was not found: missing.pt" }));
        AssertTrue(warningStatus.Contains("사용 가능") && warningStatus.Contains("검출 가중치"), "python validation warning was not translated for operators");
    }

    private static void TestRoiGeometry()
    {
        Rectangle roi = RoiGeometry.CreateBoundedRectangle(
            new Point(50, 60),
            new Point(-10, 200),
            new Rectangle(0, 0, 100, 100));

        AssertEqual(new Rectangle(0, 60, 50, 40), roi);
    }

    private static void TestOpenGlImageGeometry()
    {
        AssertEqual(new Point(40, 70), OpenGlImageGeometry.RobotToImage(new Point(40, 10), new Size(100, 80)));
        AssertEqual(new Point(0, 79), OpenGlImageGeometry.RobotToImage(new Point(-10, -5), new Size(100, 80)));

        RectangleF glRect = OpenGlImageGeometry.ToOpenGlRectangle(new Rectangle(10, 20, 30, 15), 100);
        AssertEqual(new RectangleF(10, 65, 30, 15), glRect);

        List<Point> handles = OpenGlImageGeometry.GetHandlePoints(new Rectangle(10, 20, 30, 40)).ToList();
        AssertEqual(new Point(10, 20), handles[0]);
        AssertEqual(new Point(40, 60), handles[4]);

        AssertEqual(2, OpenGlImageGeometry.GetHandleSize(0.2F));
        AssertEqual(24, OpenGlImageGeometry.GetHandleSize(2F));
    }

    private static void TestRoiInteraction()
    {
        var rois = new List<CRectangleObject>
        {
            new CRectangleObject { Roi = new Rectangle(10, 10, 20, 20) }
        };

        bool selected = RoiInteractionController.TrySelect(
            rois,
            new Point(15, 15),
            4,
            out int selectedIndex,
            out var operation);

        AssertTrue(selected, "ROI was not selected");
        AssertEqual(0, selectedIndex);
        AssertEqual("SizeAll", operation.ToString());
        AssertTrue(rois[0].Selected, "ROI selected flag was not set");

        var roiGroups = new Dictionary<string, List<CRectangleObject>>
        {
            ["OK"] = rois
        };

        RoiInteractionController.ApplyImageBounds(roiGroups, new Size(200, 100));
        AssertEqual(200, rois[0]._MaxX);
        AssertEqual(100, rois[0]._MaxY);
        AssertEqual(new Size(200, 100), rois[0].OriginalSize);

        AssertTrue(RoiInteractionController.TryRemoveSelected(roiGroups, "OK"), "selected ROI was not removed");
        AssertEqual(0, rois.Count);

        rois.Add(new CRectangleObject { Roi = new Rectangle(0, 0, 40, 40) });
        rois.Add(new CRectangleObject { Roi = new Rectangle(5, 5, 40, 40) });
        selected = RoiInteractionController.TrySelect(
            rois,
            new Point(10, 10),
            8,
            out selectedIndex,
            out operation);
        AssertTrue(selected, "overlapped ROI was not selected");
        AssertEqual(1, selectedIndex);
    }

    private static void TestCViewerRoiApi()
    {
        using var viewer = new CViewer();

        viewer.SetRoiRectangles(new[]
        {
            new Rectangle(1, 2, 30, 40),
            Rectangle.Empty,
            new Rectangle(10, 10, 0, 20)
        });

        List<Rectangle> rois = viewer.GetRoiRectangles();
        AssertEqual(1, rois.Count);
        AssertEqual(new Rectangle(1, 2, 30, 40), rois[0]);

        viewer.SetRoiRectangles(new[]
        {
            new Rectangle(4, 5, 20, 21),
            new Rectangle(40, 45, 22, 23)
        });
        AssertTrue(viewer.SelectAnnotationListItem(2), "ROI list item was not selected");
        viewer.SetModeSegmentation();
        AssertTrue(viewer.DeleteSelectedAnnotation(), "selected ROI was not deleted outside rectangle mode");
        rois = viewer.GetRoiRectangles();
        AssertEqual(1, rois.Count);
        AssertEqual(new Rectangle(4, 5, 20, 21), rois[0]);
        AssertEqual(-1, viewer.SelectedAnnotationListIndex);
        AssertTrue(viewer.UndoAnnotationChange(), "ROI delete should be undoable");
        AssertEqual(2, viewer.GetRoiRectangles().Count);

        viewer.ResetAnnotations();
        AssertEqual(0, viewer.GetRoiRectangles().Count);
    }

    private static void TestCViewerSegmentationApi()
    {
        using var viewer = new CViewer();
        using var source = new Bitmap(40, 30);
        var classItem = new CClassItem { Text = "Defect", DrawColor = Color.Red };

        viewer.SetDisplayImage(source);
        viewer.SetSegmentationPolygons(
            new[]
            {
                new[]
                {
                    new Point(3, 4),
                    new Point(20, 4),
                    new Point(20, 18),
                    new Point(3, 18)
                }
            },
            classItem,
            reset: true);

        List<List<Point>> polygons = viewer.GetSegmentationPolygons();
        AssertEqual(1, polygons.Count);
        AssertEqual(new Point(3, 4), polygons[0][0]);
        AssertEqual(1, viewer.GetRoiListItems().Count);
        AssertEqual(new Rectangle(3, 4, 18, 15), viewer.GetRoiListItems()[0].Roi);
        AssertTrue(!viewer.CanUndoAnnotationChange, "loading segmentation data should not populate edit undo history");

        viewer.SegmentationBrushRadius = 100;
        AssertEqual(48, viewer.SegmentationBrushRadius);
        viewer.SegmentationBrushRadius = 1;
        AssertEqual(2, viewer.SegmentationBrushRadius);
        viewer.SegmentationBrushRadius = 10;

        int brushAdded = viewer.AddSegmentationBrushStamps(new[] { new Point(30, 20) }, 4, classItem);
        AssertEqual(1, brushAdded);
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "brush stamp should add a segment polygon");
        AssertTrue(viewer.CanUndoAnnotationChange, "brush edit should create an undo snapshot");
        AssertTrue(viewer.UndoAnnotationChange(), "brush edit undo failed");
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.CanRedoAnnotationChange, "undo should create a redo snapshot");
        AssertTrue(viewer.RedoAnnotationChange(), "brush edit redo failed");
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "redo should restore the brush segment");
        int erased = viewer.EraseSegmentationAt(new Point(30, 20), 5);
        AssertTrue(erased > 0, "eraser should remove a brush segment");
        AssertTrue(viewer.UndoAnnotationChange(), "eraser undo failed");
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "eraser undo should restore removed segment");

        viewer.ResetAnnotations();
        viewer.AddSegmentationRectangles(
            new[]
            {
                new Rectangle(2, 2, 8, 8),
                new Rectangle(18, 10, 8, 8)
            },
            classItem);
        int merged = viewer.MergeSegmentationSegments("Defect");
        AssertTrue(merged >= 2, "segment merge should consume multiple segment polygons");
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.UndoAnnotationChange(), "segment merge undo failed");
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "segment merge undo should restore original segments");

        viewer.ResetAnnotations();
        int strokeAdded = viewer.AddSegmentationBrushStamps(
            new[]
            {
                new Point(10, 10),
                new Point(14, 10),
                new Point(18, 10),
                new Point(22, 10)
            },
            5,
            classItem);
        AssertEqual(1, strokeAdded);
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        AssertTrue(SegmentationGeometry.GetBounds(viewer.GetSegmentationPolygons()[0]).Contains(new Point(22, 10)), "viewer brush stroke should be saved as one merged segment");
        int overlappingBrushAdded = viewer.AddSegmentationBrushStamps(new[] { new Point(24, 10), new Point(27, 10) }, 5, classItem);
        AssertEqual(1, overlappingBrushAdded);
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        int strokeErased = viewer.EraseSegmentationStroke(
            new[]
            {
                new Point(10, 10),
                new Point(14, 10),
                new Point(18, 10),
                new Point(22, 10),
                new Point(24, 10),
                new Point(27, 10)
            },
            7);
        AssertEqual(1, strokeErased);
        AssertEqual(0, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.UndoAnnotationChange(), "brush stroke eraser undo failed");
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);

        viewer.ResetAnnotations();
        int rectangleSegmentAdded = viewer.AddSegmentationRectangles(new[] { new Rectangle(5, 5, 20, 12) }, classItem);
        AssertEqual(1, rectangleSegmentAdded);
        Rectangle beforeEraseBounds = SegmentationGeometry.GetBounds(viewer.GetSegmentationPolygons()[0]);
        int partialEraseCount = viewer.EraseSegmentationAt(new Point(23, 11), 5);
        AssertEqual(1, partialEraseCount);
        List<Point> afterErasePolygon = viewer.GetSegmentationPolygons()[0];
        AssertTrue(!SegmentationGeometry.ContainsPoint(afterErasePolygon, new Point(23, 11)), "viewer eraser should trim part of a segment instead of deleting the whole object");
        AssertTrue(viewer.UndoAnnotationChange(), "partial eraser undo failed");
        AssertEqual(beforeEraseBounds, SegmentationGeometry.GetBounds(viewer.GetSegmentationPolygons()[0]));
        int innerEraseCount = viewer.EraseSegmentationAt(new Point(14, 11), 3);
        AssertEqual(1, innerEraseCount);
        AssertEqual(1, viewer.GetSegmentationCutoutPolygons().Count);
        AssertTrue(SegmentationGeometry.ContainsPoint(viewer.GetSegmentationCutoutPolygons()[0], new Point(14, 11)), "internal eraser should preserve a cutout polygon");
        AssertTrue(viewer.UndoAnnotationChange(), "internal eraser undo failed");
        AssertEqual(0, viewer.GetSegmentationCutoutPolygons().Count);

        using var defectImage = new Bitmap(40, 30);
        using (Graphics graphics = Graphics.FromImage(defectImage))
        {
            graphics.Clear(Color.FromArgb(170, 170, 170));
            using var defectBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
            graphics.FillEllipse(defectBrush, 24, 12, 6, 6);
        }

        viewer.SetDisplayImage(defectImage);
        viewer.ResetAnnotations();
        viewer.SetRoiRectangles(new[] { new Rectangle(20, 8, 14, 14) }, classItem);
        int skippedAuto = viewer.AddAutoSegmentationFromRois(classItem, onlySelected: true);
        AssertEqual(0, skippedAuto);
        AssertEqual(0, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.SelectAnnotationListItem(1), "ROI list selection should select the rectangle for auto segmentation");
        int autoAdded = viewer.AddAutoSegmentationFromRois(classItem, onlySelected: true);
        AssertEqual(1, autoAdded);
        AssertTrue(viewer.GetSegmentationPolygons().Count == 1, "auto ROI extraction should add a segment polygon");

        viewer.ResetAnnotations();
        AssertEqual(0, viewer.GetSegmentationPolygons().Count);
    }

    private static void TestCViewerDefaultDefectSegmentationSave()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "default-defect.png";

            using var viewer = new CViewer();
            using var source = new Bitmap(20, 20);
            viewer.SetDisplayImage(source);

            int added = viewer.AddSegmentationRectangles(new[] { new Rectangle(2, 3, 7, 6) });
            bool saved = new LabelingWorkflowService().CommitCurrentAnnotations(viewer, data, new CSystem());
            string maskPath = Path.Combine(root, "data", "train", "masks", "default-defect.png");

            AssertEqual(1, added);
            AssertTrue(saved, "default Defect segmentation was not saved");
            AssertEqual("Defect", data.ClassNamedList[0].Text);
            AssertTrue(File.Exists(maskPath), "default Defect mask file was not created");

            using var mask = new Bitmap(maskPath);
            AssertEqual(1, mask.GetPixel(4, 4).R);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestCViewerImageCopy()
    {
        using var viewer = new CViewer();
        using var source = new Bitmap(12, 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        viewer.SetDisplayImage(source);

        AssertTrue(!ReferenceEquals(source, viewer.CurrentImage), "viewer should keep an internal image copy");
        AssertEqual(source.Size, viewer.CurrentImage.Size);
        AssertEqual(System.Drawing.Imaging.PixelFormat.Format24bppRgb, viewer.CurrentImage.PixelFormat);
    }

    private static void TestCViewerMainImageWorkspace()
    {
        using var viewer = new CViewer();
        using var source = new Bitmap(12, 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        viewer.LoadMainImage(source, "sample", @"C:\images\sample.png");

        AssertEqual("sample", CGlobal.Inst.ImageWorkspace.ActiveImageName);
        AssertEqual(@"C:\images\sample.png", CGlobal.Inst.ImageWorkspace.ActiveImagePath);
        AssertTrue(ReferenceEquals(viewer.CurrentImage, CGlobal.Inst.ImageWorkspace.ActiveImage), "workspace should reference the viewer-owned image copy");
        AssertTrue(!ReferenceEquals(source, CGlobal.Inst.ImageWorkspace.ActiveImage), "workspace should not reference the temporary source bitmap");
    }

    private static void TestCViewerAttachLifecycle()
    {
        using var viewer = new CViewer();
        using var host = new Panel();

        Control firstCanvas = viewer.AttachTo(host);
        AssertEqual(1, host.Controls.Count);
        AssertTrue(ReferenceEquals(firstCanvas, host.Controls[0]), "first canvas was not attached");

        Control secondCanvas = viewer.AttachTo(host);
        AssertEqual(1, host.Controls.Count);
        AssertTrue(!ReferenceEquals(firstCanvas, secondCanvas), "second attach reused the old canvas");
        AssertTrue(firstCanvas.IsDisposed, "first canvas was not disposed");
        AssertTrue(ReferenceEquals(secondCanvas, host.Controls[0]), "second canvas was not attached");

        viewer.Dispose();
        AssertTrue(secondCanvas.IsDisposed, "second canvas was not disposed with viewer");
        AssertEqual(0, host.Controls.Count);
    }

    private static void TestCViewerLabelingModeChrome()
    {
        using var viewer = new CViewer();

        AssertEqual("\uBAA8\uB4DC ROI", viewer.ModeDisplayText);
        viewer.SetModeDrag();
        AssertEqual(LabelingRoiMode.Drag, viewer.CurrentMode);
        AssertEqual("\uBAA8\uB4DC \uC774\uB3D9", viewer.ModeDisplayText);
        viewer.SetModeMultiRoi();
        AssertEqual(LabelingRoiMode.Rectangle, viewer.CurrentMode);
        AssertEqual("\uBAA8\uB4DC ROI", viewer.ModeDisplayText);
        viewer.SetModeSegmentation();
        AssertEqual("\uBAA8\uB4DC \uD3F4\uB9AC\uACE4", viewer.ModeDisplayText);
        viewer.SetModeSegmentationBrush();
        AssertEqual("\uBAA8\uB4DC \uBE0C\uB7EC\uC2DC", viewer.ModeDisplayText);
        viewer.SetModeSegmentationEraser();
        AssertEqual("\uBAA8\uB4DC \uC9C0\uC6B0\uAC1C", viewer.ModeDisplayText);

        var readableItemImageLoad = GetPrivateField<ToolStripItem>(viewer, "iconMenuItem1");
        var readableItemCross = GetPrivateField<ToolStripItem>(viewer, "iconMenuItem6");
        var contextMenu = GetPrivateField<ContextMenuStrip>(viewer, "ddmImageMenu");
        ToolStripItem[] menuItems = contextMenu.Items.Cast<ToolStripItem>().ToArray();

        AssertEqual("\uC774\uBBF8\uC9C0 \uC5F4\uAE30", readableItemImageLoad.Text);
        AssertTrue(readableItemImageLoad.Tag != null, "image load command should use a tag instead of text switching");
        AssertEqual("\uC2ED\uC790\uC120", readableItemCross.Text);
        AssertTrue(readableItemCross.Tag != null, "crosshair command should use a tag instead of text switching");
        AssertTrue(menuItems.All(item => item.Tag is not LabelingRoiMode), "right-click menu should not expose labeling mode commands");
        AssertTrue(!menuItems.Any(item => string.Equals(item.Text, "\uCE94\uBC84\uC2A4 \uBAA8\uB4DC", StringComparison.Ordinal)), "right-click menu should not expose a canvas mode group");
    }

    private static void TestFormLayerDisplayDisposeLifecycle()
    {
        using var image = new Bitmap(10, 10);
        var displays = new List<FormLayerDisplay>();
        var display = new FormLayerDisplay(image, 0, displays, false, "Main");
        Control canvas = display.ImageViewer;
        var modeLabel = GetPrivateField<Control>(display, "lbMODE");

        AssertTrue(canvas != null, "OpenGL canvas was not created");
        AssertEqual("\uC791\uC5C5 \uCE94\uBC84\uC2A4", display.TabText);
        AssertEqual("\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8 ROI\uB97C \uD3B8\uC9D1\uD569\uB2C8\uB2E4.", display.ToolTipText);
        AssertEqual("\uBAA8\uB4DC \uC774\uB3D9", modeLabel.Text);

        display.SetLabelingMode(LabelingRoiMode.Segmentation);
        AssertEqual(LabelingRoiMode.Segmentation, display.CurrentLabelingMode);
        display.SetLabelingMode(LabelingRoiMode.Rectangle);
        AssertEqual(LabelingRoiMode.Rectangle, display.CurrentLabelingMode);
        display.SetLabelingMode(LabelingRoiMode.SegmentationBrush);
        AssertEqual(LabelingRoiMode.SegmentationBrush, display.CurrentLabelingMode);
        display.SetLabelingMode(LabelingRoiMode.SegmentationEraser);
        AssertEqual(LabelingRoiMode.SegmentationEraser, display.CurrentLabelingMode);
        display.SetLabelingMode(LabelingRoiMode.Drag);
        AssertEqual(LabelingRoiMode.Drag, display.CurrentLabelingMode);
        display.SegmentationBrushRadius = 24;
        AssertEqual(24, display.SegmentationBrushRadius);
        display.SegmentationBrushRadius = 100;
        AssertEqual(48, display.SegmentationBrushRadius);
        AssertTrue(!display.CanUndoAnnotationChange, "new layer display should not expose undo history");
        AssertTrue(!display.CanRedoAnnotationChange, "new layer display should not expose redo history");

        display.Dispose();

        AssertTrue(canvas.IsDisposed, "hosted OpenGL canvas was not disposed with layer display");
    }

    private static void TestFormTeachingVisionWorkbenchLayout()
    {
        using var form = new FormTeachingVision
        {
            Size = new Size(1600, 900)
        };

        _ = form.Handle;

        InvokePrivate(form, "InitializeLabelingWorkspaceLayout");
        InvokePrivate(form, "ApplyWorkspaceLayout");
        Application.DoEvents();

        var workspaceLogSplitContainer = GetPrivateField<SplitContainer>(form, "workspaceLogSplitContainer");
        var workspaceSplitContainer = GetPrivateField<SplitContainer>(form, "workspaceSplitContainer");
        var workbenchSplitContainer = GetPrivateField<SplitContainer>(form, "workbenchSplitContainer");
        var datasetHostPanel = GetPrivateField<Panel>(form, "datasetHostPanel");
        var canvasHostPanel = GetPrivateField<Panel>(form, "canvasHostPanel");
        var inspectorHostPanel = GetPrivateField<Panel>(form, "inspectorHostPanel");
        var logHostPanel = GetPrivateField<Panel>(form, "logHostPanel");
        var canvasStatusLabel = GetPrivateField<Label>(form, "canvasStatusLabel");
        var toolStateLabel = GetPrivateField<Label>(form, "toolStateLabel");
        var canvasToolPanel = GetPrivateField<FlowLayoutPanel>(form, "canvasToolPanel");
        var btnToolMove = GetPrivateField<Button>(form, "btnToolMove");
        var btnToolRoi = GetPrivateField<Button>(form, "btnToolRoi");
        var btnToolSegment = GetPrivateField<Button>(form, "btnToolSegment");
        var btnToolBrush = GetPrivateField<Button>(form, "btnToolBrush");
        var btnToolEraser = GetPrivateField<Button>(form, "btnToolEraser");
        var btnToolAuto = GetPrivateField<Button>(form, "btnToolAuto");
        var btnToolMerge = GetPrivateField<Button>(form, "btnToolMerge");
        var btnToolDelete = GetPrivateField<Button>(form, "btnToolDelete");
        var btnToolUndo = GetPrivateField<Button>(form, "btnToolUndo");
        var btnToolRedo = GetPrivateField<Button>(form, "btnToolRedo");
        var nudBrushRadius = GetPrivateField<NumericUpDown>(form, "nudBrushRadius");
        var inspectorTitleLabel = GetPrivateField<Label>(form, "inspectorTitleLabel");
        var inspectorStatusLabel = GetPrivateField<Label>(form, "inspectorStatusLabel");

        AssertTrue(workspaceLogSplitContainer != null, "workspace/log split container was not created");
        AssertTrue(workspaceSplitContainer != null, "workspace split container was not created");
        AssertTrue(workbenchSplitContainer != null, "workbench split container was not created");
        AssertTrue(datasetHostPanel != null, "dataset host was not created");
        AssertTrue(canvasHostPanel != null, "canvas host was not created");
        AssertTrue(inspectorHostPanel != null, "inspector host was not created");
        AssertTrue(logHostPanel != null, "log host was not created");
        AssertTrue(canvasStatusLabel != null, "canvas status label was not created");
        AssertTrue(toolStateLabel != null, "teaching tool state label was not created");
        AssertTrue(canvasToolPanel != null && canvasToolPanel.Controls.Count == 12, "teaching tool panel was not created in the canvas header");
        AssertEqual(LabelingRoiMode.Drag, btnToolMove.Tag);
        AssertEqual(LabelingRoiMode.Rectangle, btnToolRoi.Tag);
        AssertEqual(LabelingRoiMode.Segmentation, btnToolSegment.Tag);
        AssertEqual(LabelingRoiMode.SegmentationBrush, btnToolBrush.Tag);
        AssertEqual(LabelingRoiMode.SegmentationEraser, btnToolEraser.Tag);
        AssertEqual("AutoSegment", btnToolAuto.Tag);
        AssertEqual("MergeSegments", btnToolMerge.Tag);
        AssertEqual("DeleteAnnotation", btnToolDelete.Tag);
        AssertEqual("UndoAnnotation", btnToolUndo.Tag);
        AssertEqual("RedoAnnotation", btnToolRedo.Tag);
        AssertEqual(8M, nudBrushRadius.Value);
        AssertTrue(toolStateLabel.Text.Contains("\uB3C4\uAD6C") && toolStateLabel.Text.Contains("\uC774\uB3D9"), "tool state label should show the current move tool");
        AssertEqual(2, btnToolMove.FlatAppearance.BorderSize);
        AssertEqual(1, btnToolRoi.FlatAppearance.BorderSize);
        InvokePrivateResult<object>(form, "TeachingToolButton_Click", btnToolBrush, EventArgs.Empty);
        Application.DoEvents();
        AssertTrue(toolStateLabel.Text.Contains("\uBE0C\uB7EC\uC2DC") && toolStateLabel.Text.Contains("\uBC18\uACBD 8"), "brush mode should show brush radius in the tool state label");
        AssertEqual(2, btnToolBrush.FlatAppearance.BorderSize);
        AssertEqual(1, btnToolMove.FlatAppearance.BorderSize);
        nudBrushRadius.Value = 17M;
        Application.DoEvents();
        AssertTrue(toolStateLabel.Text.Contains("\uBC18\uACBD 17"), "tool state label should refresh when brush radius changes");
        AssertTrue(!btnToolUndo.Enabled, "undo command should be disabled before an edit exists");
        AssertTrue(!btnToolRedo.Enabled, "redo command should be disabled before an edit exists");
        AssertTrue(inspectorTitleLabel != null, "inspector title label was not created");
        AssertTrue(inspectorStatusLabel != null, "inspector status label was not created");

        AssertTrue(form.TeachingPanel.Controls.Contains(workspaceLogSplitContainer), "teaching panel does not host the workspace/log split");
        AssertTrue(workspaceLogSplitContainer.Panel1.Controls.Contains(workspaceSplitContainer), "workspace/log split does not host the main workspace");
        AssertTrue(workspaceSplitContainer.Panel2.Controls.Contains(workbenchSplitContainer), "workspace does not host the canvas/inspector split");
        AssertEqual(Orientation.Horizontal, workspaceLogSplitContainer.Orientation);
        AssertEqual(Orientation.Vertical, workspaceSplitContainer.Orientation);
        AssertEqual(Orientation.Vertical, workbenchSplitContainer.Orientation);

        AssertTrue(ContainsLabelText(workspaceSplitContainer.Panel1, "이미지 큐"), "dataset region caption was not created");
        AssertTrue(ContainsLabelText(workbenchSplitContainer.Panel1, "캔버스"), "canvas region caption was not created");
        AssertTrue(canvasStatusLabel.Text.Contains("라벨") && canvasStatusLabel.Text.Contains("AI 후보"), "canvas status label should summarize labeling state");
        AssertEqual("라벨 패널", inspectorTitleLabel.Text);
        AssertEqual("라벨 객체 / 클래스", inspectorStatusLabel.Text);
        AssertTrue(ContainsLabelText(workspaceLogSplitContainer.Panel2, "로그"), "log region caption was not created");
        AssertEqual(Color.FromArgb(15, 17, 20), form.TeachingPanel.BackColor);
        AssertEqual(Color.FromArgb(19, 21, 24), datasetHostPanel.BackColor);
        AssertEqual(Color.FromArgb(8, 10, 12), canvasHostPanel.BackColor);
        AssertEqual(Color.FromArgb(19, 21, 24), inspectorHostPanel.BackColor);
        AssertEqual(Color.FromArgb(19, 21, 24), logHostPanel.BackColor);

        AssertTrue(workspaceSplitContainer.Panel1MinSize >= 270, "dataset panel minimum width regressed");
        AssertTrue(workbenchSplitContainer.Panel2MinSize >= 350, "review panel minimum width regressed");
        AssertTrue(workspaceLogSplitContainer.Panel2MinSize >= 96, "log panel minimum height regressed");

        form.Forms[DEFINE.VISION_DOCK_FORM.IMAGELIST] = new FormImageList();
        form.Forms[DEFINE.VISION_DOCK_FORM.CLASSLIST] = new FormClassList();
        form.Forms[DEFINE.VISION_DOCK_FORM.DETECTIONREVIEW] = new FormDetectionReviewPanel();
        form.Forms[DEFINE.VISION_DOCK_FORM.LOG] = new FormLog();
        InvokePrivate(form, "ShowVisionForms");
        Application.DoEvents();

        AssertTrue(logHostPanel.Controls.Count == 1 && logHostPanel.Controls[0] is FormLog, "OpenVisionLab log view was not hosted in the bottom log region");
        AssertTrue(inspectorHostPanel.Controls.Count == 1 && inspectorHostPanel.Controls[0] is FormClassList, "teaching mode should host the label/class panel");

        form.SetWorkspaceMode(DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW);
        Application.DoEvents();
        AssertEqual(DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW, form.CurrentWorkspaceMode);
        AssertTrue(inspectorHostPanel.Controls.Count == 1 && inspectorHostPanel.Controls[0] is FormDetectionReviewPanel, "detection review mode should host the AI candidate panel");
        AssertTrue(inspectorTitleLabel.Text.Contains("AI") && inspectorTitleLabel.Text.Contains("후보"), "detection review title was not applied");

        form.SetWorkspaceMode(DEFINE.LABELING_WORKSPACE_MODE.TRAINING);
        Application.DoEvents();
        AssertEqual(DEFINE.LABELING_WORKSPACE_MODE.TRAINING, form.CurrentWorkspaceMode);
        AssertTrue(inspectorHostPanel.Controls.Count == 1 && inspectorHostPanel.Controls[0] is FormTrainingPanel, "training mode should host the dataset/model readiness panel");
        AssertTrue(inspectorTitleLabel.Text.Contains("학습"), "training title was not applied");
    }

    private static void TestFormClassListReviewSegments()
    {
        using var form = new FormClassList
        {
            Size = new Size(430, 760)
        };

        _ = form.Handle;
        Application.DoEvents();

        var reviewModePanel = GetPrivateField<Panel>(form, "reviewModePanel");
        var reviewContentPanel = GetPrivateField<Panel>(form, "reviewContentPanel");
        var labelPagePanel = GetPrivateField<Panel>(form, "labelPagePanel");
        var classPagePanel = GetPrivateField<Panel>(form, "classPagePanel");
        var lblLabelEmptyState = GetPrivateField<Label>(form, "lblLabelEmptyState");
        var lblClassEmptyState = GetPrivateField<Label>(form, "lblClassEmptyState");
        var btnReviewLabels = GetPrivateField<Button>(form, "btnReviewLabels");
        var btnReviewClasses = GetPrivateField<Button>(form, "btnReviewClasses");

        AssertEqual("라벨 패널", form.Text);
        AssertTrue(reviewModePanel != null, "review mode segment panel was not created");
        AssertTrue(reviewContentPanel != null, "review content panel was not created");
        AssertTrue(labelPagePanel != null, "label page panel was not created");
        AssertTrue(classPagePanel != null, "class page panel was not created");
        AssertTrue(lblLabelEmptyState != null, "label empty state was not created");
        AssertTrue(lblClassEmptyState != null, "class empty state was not created");
        AssertTrue(labelPagePanel.Controls.Contains(lblLabelEmptyState), "label empty state was not hosted in the label page");
        AssertTrue(classPagePanel.Controls.Contains(lblClassEmptyState), "class empty state was not hosted in the class page");
        AssertTrue(btnReviewLabels != null && btnReviewClasses != null, "label/class segment buttons were not created");
        AssertEqual("라벨 객체", btnReviewLabels.Text);
        AssertEqual("클래스", btnReviewClasses.Text);
        AssertTrue(!ContainsControlType<TabControl>(form), "WinForms TabControl should not be used for the labeling review panel");

        AssertTrue(labelPagePanel.Visible, "label panel should be visible by default");
        AssertTrue(!classPagePanel.Visible, "class panel should be hidden by default");

        btnReviewClasses.PerformClick();
        Application.DoEvents();

        AssertTrue(!labelPagePanel.Visible, "label panel was still visible after selecting classes");
        AssertTrue(classPagePanel.Visible, "class panel was not visible after selecting classes");
    }

    private static void TestFormDetectionReviewPanel()
    {
        using var form = new FormDetectionReviewPanel
        {
            Size = new Size(430, 760)
        };

        _ = form.Handle;
        Application.DoEvents();

        var dgvCandidateList = GetPrivateField<DataGridView>(form, "dgvCandidateList");
        var lblSummary = GetPrivateField<Label>(form, "lblSummary");
        var lblEmptyState = GetPrivateField<Label>(form, "lblEmptyState");
        var btnDetectCurrentImage = GetPrivateField<Control>(form, "btnDetectCurrentImage");
        var btnConfirmSelectedCandidate = GetPrivateField<Control>(form, "btnConfirmSelectedCandidate");
        var btnConfirmAllCandidates = GetPrivateField<Control>(form, "btnConfirmAllCandidates");
        var btnSkipSelectedCandidate = GetPrivateField<Control>(form, "btnSkipSelectedCandidate");

        AssertEqual("AI 후보 검수", form.Text);
        AssertTrue(dgvCandidateList != null, "candidate grid was not created");
        AssertEqual(3, dgvCandidateList.Columns.Count);
        AssertTrue(lblSummary != null && lblSummary.Text.Contains("후보"), "candidate summary label was not created");
        AssertTrue(lblEmptyState != null && lblEmptyState.Parent != null, "candidate empty state should be hosted when there are no candidates");
        AssertTrue(lblEmptyState.Text.Contains("AI") && lblEmptyState.Text.Contains("후보"), "candidate empty state text should describe AI candidates");
        AssertTrue(btnDetectCurrentImage != null, "detect current image button was not created");
        AssertTrue(btnConfirmSelectedCandidate != null, "confirm selected candidate button was not created");
        AssertTrue(btnConfirmAllCandidates != null, "confirm all candidates button was not created");
        AssertTrue(btnSkipSelectedCandidate != null, "skip candidate button was not created");
        AssertTrue(!ContainsControlType<TabControl>(form), "WinForms TabControl should not be used for the detection review panel");

        var confirmableCandidate = new DetectionCandidateReviewItem(
            1,
            "OK",
            0.8F,
            new Rectangle(1, 2, 30, 40),
            new Rectangle(1, 2, 30, 40),
            isConfidenceAccepted: true,
            isInImageBounds: true,
            isSelected: true);
        var lowConfidenceCandidate = new DetectionCandidateReviewItem(
            2,
            "NG",
            0.2F,
            new Rectangle(10, 20, 30, 40),
            new Rectangle(10, 20, 30, 40),
            isConfidenceAccepted: false,
            isInImageBounds: true,
            isSelected: false);

        string lowState = InvokePrivateStaticResult<string>(typeof(FormDetectionReviewPanel), "FormatCandidateState", lowConfidenceCandidate, 0.5F);
        string lowBounds = InvokePrivateStaticResult<string>(typeof(FormDetectionReviewPanel), "FormatCandidateBounds", lowConfidenceCandidate, 0.5F);
        string selectedClass = InvokePrivateStaticResult<string>(typeof(FormDetectionReviewPanel), "FormatCandidateClass", confirmableCandidate, 0.5F);
        AssertTrue(lowState.Contains("신뢰도") && lowState.Contains("50"), "low confidence candidate state should include the active threshold");
        AssertTrue(lowBounds.Contains("기준") && lowBounds.Contains("50"), "low confidence candidate bounds should include the active threshold");
        AssertTrue(selectedClass.Contains("선택") && selectedClass.Contains("50"), "selected candidate class text should include the active threshold");
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(method != null, $"private method was not found: {methodName}");
        try
        {
            method.Invoke(instance, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static T InvokePrivateResult<T>(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(method != null, $"private method was not found: {methodName}");
        try
        {
            object result = method.Invoke(instance, args);
            return result is T typed ? typed : default;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static T InvokePrivateStaticResult<T>(Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        AssertTrue(method != null, $"private static method was not found: {methodName}");
        try
        {
            object result = method.Invoke(null, args);
            return result is T typed ? typed : default;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static object GetNestedEnumValue<TDeclaringType>(string enumName, string valueName)
    {
        Type enumType = typeof(TDeclaringType).GetNestedType(enumName, BindingFlags.NonPublic);
        AssertTrue(enumType != null && enumType.IsEnum, $"private nested enum was not found: {enumName}");
        return Enum.Parse(enumType, valueName);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(field != null, $"private field was not found: {fieldName}");
        return field.GetValue(instance) as T;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(field != null, $"private field was not found: {fieldName}");
        field.SetValue(instance, value);
    }

    private static bool ContainsLabelText(Control root, string text)
    {
        if (root == null)
        {
            return false;
        }

        foreach (Control control in root.Controls)
        {
            if (control is Label label && string.Equals(label.Text, text, StringComparison.Ordinal))
            {
                return true;
            }

            if (ContainsLabelText(control, text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsControlText(Control root, string text)
    {
        if (root == null)
        {
            return false;
        }

        foreach (Control control in root.Controls)
        {
            if (string.Equals(control.Text, text, StringComparison.Ordinal))
            {
                return true;
            }

            if (ContainsControlText(control, text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsControlType<T>(Control root)
        where T : Control
    {
        if (root == null)
        {
            return false;
        }

        foreach (Control control in root.Controls)
        {
            if (control is T)
            {
                return true;
            }

            if (ContainsControlType<T>(control))
            {
                return true;
            }
        }

        return false;
    }

    private static void TestDisplayManagerImageSourceOwnership()
    {
        var first = new CvMat(2, 2, CvMatType.CV_8UC1, CvScalar.Black);
        var second = new CvMat(3, 3, CvMatType.CV_8UC1, CvScalar.White);

        CDisplayManager.ImageSrc = first;
        CDisplayManager.ImageSrc = second;

        AssertTrue(first.IsDisposed, "previous image source Mat was not disposed");
        AssertTrue(ReferenceEquals(second, CDisplayManager.ImageSrc), "new image source Mat was not stored");

        CDisplayManager.ImageSrc = null;

        AssertTrue(second.IsDisposed, "replaced image source Mat was not disposed");
        AssertTrue(CDisplayManager.ImageSrc != null, "image source fallback Mat was not created");
    }

    private static void TestDisplayManagerDirectCanvasHost()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        using var owner = new Form();
        using var hostPanel = new Panel
        {
            Size = new Size(320, 240)
        };
        owner.Controls.Add(hostPanel);
        _ = owner.Handle;

        CDisplayManager.SetForm(owner);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayPanel(hostPanel);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            using var image = new Bitmap(24, 18);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            Application.DoEvents();

            AssertEqual(1, hostPanel.Controls.Count);
            AssertTrue(hostPanel.Controls[0] is FormLayerDisplay, "direct canvas host did not receive a layer display");

            var display = (FormLayerDisplay)hostPanel.Controls[0];
            AssertTrue(!display.TopLevel, "directly hosted display must not be a top-level form");
            AssertEqual(DockStyle.Fill, display.Dock);
            AssertEqual("Main", CDisplayManager.GetSelectedDisplayOrNull()?.Text);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayPanel(null);
            CDisplayManager.SetForm(null);
            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestDisplayManagerLayerCatalog()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            using var image = new Bitmap(12, 8);

            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.CreateLayerDisplay(image, "Detect", true);

            AssertEqual(2, CDisplayManager.LayerCount);
            AssertEqual("Main", CDisplayManager.GetLayerTitle(0));
            AssertEqual("Detect", CDisplayManager.GetLayerTitle(1));
            AssertEqual(1, CDisplayManager.FindIndex("detect"));
            AssertTrue(ReferenceEquals(CDisplayManager.GetMainDisplayOrNull(), CDisplayManager.GetLayerDisplayOrNull("Main")), "main display lookup was inconsistent");

            CDisplayManager.SelecteItem = "Detect";
            AssertEqual("Detect", CDisplayManager.GetSelectedDisplayOrNull().Text);

            AssertTrue(CDisplayManager.IsLayerImageChanged("Detect"), "new layer image change flag was not set");
            CDisplayManager.AcceptLayerImageChanged("Detect");
            AssertTrue(!CDisplayManager.IsLayerImageChanged("Detect"), "layer image change flag was not accepted");

            IReadOnlyList<DisplayLayerInfo> infos = CDisplayManager.GetLayerInfos();
            AssertEqual(2, infos.Count);
            AssertEqual("Main", infos[0].Title);
            AssertEqual("Detect", infos[1].Title);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestLabelingWorkflowService()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);

            var classItem = new CClassItem { Text = "Part", DrawColor = Color.Blue };
            CGlobal.Inst.LabelingWorkflow.ApplySelectedClass(classItem);

            FormLayerDisplay mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            mainDisplay.SetRoiRectangles(new[] { new Rectangle(2, 3, 10, 11) }, reset: true);

            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();

            AssertEqual(1, items.Count);
            AssertEqual(1, items[0].Index);
            AssertEqual("Part", items[0].ClassName);
            AssertEqual(new Rectangle(2, 3, 10, 11), items[0].Roi);
            AssertEqual(LabelingAnnotationKind.Rectangle, items[0].Kind);
            AssertTrue(!items[0].IsSelected, "ROI should not be selected before explicit list selection");

            AssertTrue(CGlobal.Inst.LabelingWorkflow.SelectMainRoiItem(1), "main ROI list selection failed");
            AssertEqual(1, CGlobal.Inst.LabelingWorkflow.GetMainSelectedRoiListIndex());
            items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            AssertTrue(items[0].IsSelected, "selected ROI list item was not reflected back from the viewer");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestDisplayManagerDetectionOverlayRouting()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            using var image = new Bitmap(12, 8);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.CreateLayerDisplay(image, "Detect", true);

            var overlays = new[]
            {
                new DetectionOverlayItem
                {
                    ClassName = "Part",
                    Confidence = 0.95F,
                    Bounds = new RectangleF(1, 2, 3, 4),
                    Color = Color.Red
                }
            };

            AssertTrue(CDisplayManager.SetDetectionOverlays("Main", overlays), "Main overlay update failed");
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
            AssertEqual(0, CDisplayManager.GetLayerDisplayOrNull("Detect").DetectionOverlayCount);

            AssertTrue(!CDisplayManager.SetDetectionOverlays("Missing", overlays), "missing layer overlay update should not fall back to the first layer");
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
            AssertEqual(0, CDisplayManager.GetLayerDisplayOrNull("Detect").DetectionOverlayCount);

            AssertTrue(CDisplayManager.SetDetectionOverlays("Main", null), "Main overlay clear failed");
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestLabelingWorkflowCommitAnnotations()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var viewer = new CViewer();
            using var image = new Bitmap(20, 20);
            viewer.SetDisplayImage(image);
            viewer.SetSelectedClass(data.ClassNamedList[0]);
            viewer.SetRoiRectangles(new[] { new Rectangle(5, 5, 10, 10) }, data.ClassNamedList[0], reset: true);

            var workflow = new LabelingWorkflowService();
            bool committed = workflow.CommitCurrentAnnotations(viewer, data, new CSystem());

            AssertTrue(committed, "annotation commit failed");
            string trainLabel = Path.Combine(root, "data", "train", "labels", "sample.txt");
            AssertTrue(File.Exists(trainLabel), "committed label file was not created");
            AssertEqual("0 0.5 0.5 0.5 0.5", File.ReadAllText(trainLabel).Trim());

            AssertTrue(!workflow.CommitCurrentAnnotations(viewer, null, new CSystem()), "commit with null data should fail without exception");

            var previousDisplays = CDisplayManager.Displays.ToList();
            try
            {
                using var displayImage = new Bitmap(20, 20);
                CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());
                CDisplayManager.CreateLayerDisplay(displayImage, "Main", true);
                FormLayerDisplay mainDisplay = CDisplayManager.GetMainDisplayOrNull();
                mainDisplay.SetSelectedClass(data.ClassNamedList[0]);
                mainDisplay.SetRoiRectangles(new[] { new Rectangle(2, 2, 4, 4) }, data.ClassNamedList[0], reset: true);
                data.LastSelectImageName = "display-sample";

                AssertTrue(workflow.CommitMainAnnotations(data, new CSystem()), "main display annotation commit failed");
                string displayTrainLabel = Path.Combine(root, "data", "train", "labels", "display-sample.txt");
                AssertTrue(File.Exists(displayTrainLabel), "main display committed label file was not created");
            }
            finally
            {
                foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
                {
                    display.Dispose();
                }

                CDisplayManager.SetDisplayLayerList(previousDisplays);
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestLabelingWorkflowDefaultDefectClass()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "unclassified";

            using var viewer = new CViewer();
            using var image = new Bitmap(40, 30);
            viewer.SetDisplayImage(image);
            viewer.SetRoiRectangles(new[] { new Rectangle(4, 6, 20, 12) }, classItem: null, reset: true);

            var workflow = new LabelingWorkflowService();
            AssertTrue(workflow.CommitCurrentAnnotations(viewer, data, new CSystem()), "unclassified ROI commit failed");
            AssertEqual(1, data.ClassNamedList.Count);
            AssertEqual("Defect", data.ClassNamedList[0].Text);

            string trainLabel = Path.Combine(root, "data", "train", "labels", "unclassified.txt");
            AssertTrue(File.Exists(trainLabel), "default Defect label file was not created");
            string line = File.ReadAllText(trainLabel).Trim();
            AssertTrue(line.StartsWith("0 ", StringComparison.Ordinal), "default Defect class should be written as class index 0");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultApplicationService()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => candidateEventCount = e.CandidateCount;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) => lastReason = e.Reason;
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "NG", Confidence = 0.77F, X = 2, Y = 3, Width = 8, Height = 9 }
            });

            AssertTrue(applied, "detection result was not applied");
            AssertEqual(1, candidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.ResultCompleted, lastReason);
            AssertEqual(1, CDisplayManager.LayerCount);
            AssertEqual("Main", CDisplayManager.GetLayerTitle(0));
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultApplicationServiceBackgroundThread()
    {
        bool previousCrossThreadCheck = Control.CheckForIllegalCrossThreadCalls;
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        Form owner = null;
        Panel canvasPanel = null;

        Control.CheckForIllegalCrossThreadCalls = true;
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            owner = new Form();
            canvasPanel = new Panel { Dock = DockStyle.Fill };
            owner.Controls.Add(canvasPanel);
            _ = owner.Handle;
            _ = canvasPanel.Handle;
            CDisplayManager.SetForm(owner);
            CDisplayManager.SetDisplayPanel(canvasPanel);

            using (Bitmap image = new Bitmap(20, 20))
            {
                CDisplayManager.CreateLayerDisplay(image, "Main", false);
            }

            var service = new DetectionResultApplicationService();
            Task<bool> applyTask = Task.Run(() => service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "NG", Confidence = 0.91F, X = 2, Y = 3, Width = 8, Height = 9 }
            }));

            AssertTrue(PumpUntil(() => applyTask.IsCompleted, TimeSpan.FromSeconds(3)), "background detection result did not return to the caller");
            AssertTrue(applyTask.GetAwaiter().GetResult(), "background detection result was not applied");
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetForm(null);
            CDisplayManager.SetDisplayPanel(null);
            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            canvasPanel?.Dispose();
            owner?.Dispose();
            Control.CheckForIllegalCrossThreadCalls = previousCrossThreadCheck;
        }
    }

    private static void TestDetectionResultKeepsMainActive()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.SelecteItem = "Main";
            CDisplayManager.FocusItem = "Main";

            var service = new DetectionResultApplicationService();
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "NG", Confidence = 0.77F, X = 2, Y = 3, Width = 8, Height = 9 }
            });

            AssertTrue(applied, "detection result was not applied");
            AssertEqual(1, CDisplayManager.LayerCount);
            AssertEqual("Main", CDisplayManager.SelecteItem);
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultApplicationServiceClearsEmptyResult()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", true);
            CDisplayManager.SetDetectionOverlays("Main", new[]
            {
                new DetectionOverlayItem
                {
                    ClassName = "NG",
                    Confidence = 0.9F,
                    Bounds = new RectangleF(1, 1, 4, 4),
                    Color = Color.Red
                }
            });

            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => candidateEventCount = e.CandidateCount;

            bool applied = service.ApplyToDetectLayer(Array.Empty<DefectInfo>());

            AssertTrue(!applied, "empty detection result should not create a new layer");
            AssertEqual(0, candidateEventCount);
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultCancelPendingIgnoresLateBoxes()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            var data = new CData
            {
                LastSelectImageName = "pending-cancel",
                LastSelectImagePath = Path.Combine(root, "pending-cancel.bmp")
            };
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", true);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(data, image.Size);
            service.CancelPendingDetection();

            int candidateEventCount = 0;
            service.DetectionCandidatesUpdated += (_, _) => candidateEventCount++;

            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Late", Confidence = 0.9F, X = 1, Y = 1, Width = 8, Height = 8 }
            });

            AssertTrue(!applied, "late detection boxes should be ignored after pending cancellation");
            AssertEqual(0, candidateEventCount);
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultPendingRequestTimeout()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "timeout-sample",
                LastSelectImagePath = Path.Combine(root, "timeout-sample.bmp")
            };
            CGlobal.Inst.Data = data;

            var service = new DetectionResultApplicationService();
            using var timeoutEvent = new ManualResetEventSlim(false);
            DetectionCandidateUpdateReason timeoutReason = DetectionCandidateUpdateReason.CandidatesChanged;
            int timeoutCandidateCount = -1;

            service.DetectionCandidatesUpdated += (_, e) =>
            {
                if (e.Reason != DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    return;
                }

                timeoutReason = e.Reason;
                timeoutCandidateCount = e.CandidateCount;
                timeoutEvent.Set();
            };

            service.RegisterPendingDetectionImage(data, new Size(40, 30), detectionTimeoutSeconds: 1);

            AssertTrue(timeoutEvent.Wait(TimeSpan.FromSeconds(3)), "pending detection request did not time out");
            AssertEqual(DetectionCandidateUpdateReason.RequestTimedOut, timeoutReason);
            AssertEqual(0, timeoutCandidateCount);
            AssertEqual(0, service.GetLastDefects().Count);

            bool appliedLateResult = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Late", Confidence = 0.9F, X = 1, Y = 1, Width = 8, Height = 8 }
            });

            AssertTrue(!appliedLateResult, "late detection boxes should be ignored after timeout");
            AssertEqual(0, service.GetLastDefects().Count);
        }
        finally
        {
            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultConfirmAsLabels()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "confirm-sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => lastCandidateEventCount = e.CandidateCount;
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            AssertTrue(service.CanCommitLastDetection(data), "fresh detection result should be confirmable");
            AssertEqual(1, lastCandidateEventCount);
            bool confirmed = service.CommitLastDetectionToMainLabels(data, new CSystem());
            bool confirmedAgain = service.CommitLastDetectionToMainLabels(data, new CSystem());
            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "confirm-sample.txt");

            AssertTrue(confirmed, "detection result was not confirmed as labels");
            AssertTrue(!confirmedAgain, "already confirmed detection result should not be confirmed again");
            AssertTrue(!service.CanCommitLastDetection(data), "confirmed detection result should be cleared");
            AssertEqual(0, lastCandidateEventCount);
            AssertEqual(1, items.Count);
            AssertEqual("Part", items[0].ClassName);
            AssertEqual(new Rectangle(5, 6, 10, 8), items[0].Roi);
            AssertTrue(File.Exists(labelPath), "confirmed detection label file was not saved");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultConfirmAsSegmentationMasks()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "confirm-segment";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            bool confirmed = service.CommitLastDetectionToMainLabels(
                data,
                new CSystem(),
                minimumConfidence: 0.5F,
                createSegmentationFromBoxes: true);
            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "confirm-segment.txt");
            string maskPath = Path.Combine(root, "data", "train", "masks", "confirm-segment.png");
            string segmentPath = Path.Combine(root, "data", "train", "segments", "confirm-segment.json");

            AssertTrue(confirmed, "detection result was not confirmed with segmentation masks");
            AssertEqual(2, items.Count);
            AssertTrue(File.Exists(labelPath), "YOLO label file was not saved");
            AssertTrue(File.Exists(maskPath), "segmentation mask file was not saved");
            AssertTrue(File.Exists(segmentPath), "segmentation polygon file was not saved");

            using var mask = new Bitmap(maskPath);
            AssertEqual(1, mask.GetPixel(7, 8).R);
            AssertEqual(0, mask.GetPixel(25, 25).R);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultRejectsStaleImage()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var requestedData = new CData
            {
                LastSelectImageName = "requested-image",
                LastSelectImagePath = Path.Combine(root, "requested-image.bmp")
            };
            var activeData = new CData
            {
                LastSelectImageName = "active-image",
                LastSelectImagePath = Path.Combine(root, "active-image.bmp")
            };

            CGlobal.Inst.Data = activeData;
            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(requestedData, image.Size);
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            AssertTrue(!applied, "stale detection result should not be applied to the active image");
            AssertEqual(1, CDisplayManager.LayerCount);
            AssertEqual(0, service.GetLastDefects().Count);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultAddsMissingClasses()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "auto-class-sample";

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "OK", Confidence = 0.98F, X = 4, Y = 5, Width = 12, Height = 9 }
            });

            bool confirmed = service.CommitLastDetectionToMainLabels(data, new CSystem());
            string labelPath = Path.Combine(root, "data", "train", "labels", "auto-class-sample.txt");

            AssertTrue(confirmed, "detection result with a missing model class was not confirmed");
            AssertEqual(1, data.ClassNamedList.Count);
            AssertEqual("OK", data.ClassNamedList[0].Text);
            AssertTrue(File.Exists(labelPath), "auto-class detection label file was not saved");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultFiltersLowConfidence()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "confidence-sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.2F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.8F, X = 12, Y = 6, Width = 10, Height = 9 }
            });

            bool confirmed = service.CommitLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F);
            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();

            AssertTrue(confirmed, "high-confidence detection result was not confirmed");
            AssertEqual(1, items.Count);
            AssertEqual(new Rectangle(12, 6, 10, 9), items[0].Roi);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultCanCommitUsesMinimumConfidence()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.LastSelectImageName = "confidence-availability";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.2F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.8F, X = 60, Y = 60, Width = 10, Height = 9 }
            });

            AssertTrue(service.CanCommitLastDetection(data), "default threshold should allow the low-confidence in-bounds box");
            AssertTrue(!service.CanCommitLastDetection(data, minimumConfidence: 0.5F), "minimum confidence should disable confirmation when no in-bounds box passes");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultReviewCandidates()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "candidate-review"
            };
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.8F, X = 2, Y = 3, Width = 8, Height = 9 },
                new DefectInfo { ClassName = "Part", Confidence = 0.2F, X = 12, Y = 5, Width = 7, Height = 6 },
                new DefectInfo { ClassName = "Part", Confidence = 0.9F, X = 60, Y = 60, Width = 10, Height = 10 }
            });

            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data, minimumConfidence: 0.5F);

            AssertEqual(3, candidates.Count);
            AssertTrue(candidates[0].IsConfirmable, "high-confidence in-bounds candidate should be confirmable");
            AssertTrue(!candidates[1].IsConfirmable, "low-confidence candidate should not be confirmable");
            AssertTrue(!candidates[1].IsConfidenceAccepted, "low-confidence candidate was not marked as below threshold");
            AssertTrue(!candidates[2].IsConfirmable, "out-of-bounds candidate should not be confirmable");
            AssertTrue(!candidates[2].IsInImageBounds, "out-of-bounds candidate was not marked as outside the image");
            AssertEqual(new Rectangle(2, 3, 8, 9), candidates[0].ClippedBounds);

            AssertTrue(service.SelectDetectionCandidate(2, data), "candidate selection failed");
            IReadOnlyList<DetectionCandidateReviewItem> selectedCandidates = service.GetLastCandidateReviewItems(data, minimumConfidence: 0.5F);
            IReadOnlyList<DetectionOverlayItem> mainOverlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();

            AssertTrue(!selectedCandidates[0].IsSelected, "first candidate should not be selected");
            AssertTrue(selectedCandidates[1].IsSelected, "second candidate should be selected");
            AssertTrue(mainOverlays.Any(overlay => overlay.CandidateIndex == 2 && overlay.IsSelected), "Main overlay did not select candidate 2");
            AssertTrue(!service.CanCommitLastDetection(data, minimumConfidence: 0.5F), "selected low-confidence candidate should not be confirmable");
            AssertTrue(!service.SelectDetectionCandidate(99, data), "missing candidate selection should fail");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestDetectionResultConfirmsSelectedCandidateOnly()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "selected-candidate";
            data.LastSelectImagePath = Path.Combine(root, "selected-candidate.bmp");
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(50, 40);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(40, 50, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => lastCandidateEventCount = e.CandidateCount;
            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.91F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.92F, X = 20, Y = 10, Width = 12, Height = 9 }
            });

            AssertTrue(service.SelectDetectionCandidate(2, data), "second candidate was not selected");
            AssertTrue(service.CommitLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F), "selected candidate was not confirmed");

            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            IReadOnlyList<DefectInfo> remaining = service.GetLastDefects();
            IReadOnlyList<DetectionOverlayItem> mainOverlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();
            string labelPath = Path.Combine(root, "data", "train", "labels", "selected-candidate.txt");

            AssertEqual(1, items.Count);
            AssertEqual(new Rectangle(20, 10, 12, 9), items[0].Roi);
            AssertEqual(1, remaining.Count);
            AssertEqual(1, mainOverlays.Count);
            AssertEqual(1, mainOverlays[0].CandidateIndex);
            AssertTrue(!mainOverlays[0].IsSelected, "remaining candidate should not stay selected after commit");
            AssertEqual(1, lastCandidateEventCount);
            AssertTrue(File.Exists(labelPath), "selected candidate label file was not saved");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultSkipsSelectedCandidate()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "skip-candidate"
            };
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(50, 40);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(40, 50, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) =>
            {
                lastCandidateEventCount = e.CandidateCount;
                lastReason = e.Reason;
            };

            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "A", Confidence = 0.91F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "B", Confidence = 0.92F, X = 20, Y = 10, Width = 12, Height = 9 }
            });

            AssertTrue(service.SelectDetectionCandidate(1, data), "first candidate was not selected");
            AssertTrue(service.CanSkipSelectedDetectionCandidate(data), "selected candidate should be skippable");
            AssertTrue(service.SkipSelectedDetectionCandidate(data), "selected candidate was not skipped");

            IReadOnlyList<DefectInfo> remaining = service.GetLastDefects();
            IReadOnlyList<DetectionOverlayItem> overlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();
            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data);

            AssertEqual(1, remaining.Count);
            AssertEqual("B", remaining[0].ClassName);
            AssertEqual(1, overlays.Count);
            AssertEqual(1, overlays[0].CandidateIndex);
            AssertTrue(!overlays[0].IsSelected, "remaining candidate should not stay selected after skip");
            AssertEqual(1, candidates.Count);
            AssertEqual(1, candidates[0].Index);
            AssertEqual("B", candidates[0].ClassName);
            AssertEqual(1, lastCandidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.CandidatesChanged, lastReason);
            AssertTrue(!service.CanSkipSelectedDetectionCandidate(data), "selection should clear after skip");

            AssertTrue(service.SelectDetectionCandidate(1, data), "remaining candidate was not selected");
            AssertTrue(service.SkipSelectedDetectionCandidate(data), "last candidate was not skipped");
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays().Count);
            AssertEqual(0, lastCandidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.CandidateSkipped, lastReason);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestDetectionResultConfirmsAllCandidatesDespiteSelection()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "all-candidates";
            data.LastSelectImagePath = Path.Combine(root, "all-candidates.bmp");
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(50, 40);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(40, 50, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) =>
            {
                lastCandidateEventCount = e.CandidateCount;
                lastReason = e.Reason;
            };

            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.91F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.92F, X = 20, Y = 10, Width = 12, Height = 9 }
            });

            AssertTrue(service.SelectDetectionCandidate(1, data), "first candidate was not selected");
            AssertTrue(service.CommitAllLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F), "all candidates were not confirmed");

            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "all-candidates.txt");

            AssertEqual(2, items.Count);
            AssertTrue(items.Any(item => item.Roi == new Rectangle(1, 2, 8, 8)), "first candidate was not saved");
            AssertTrue(items.Any(item => item.Roi == new Rectangle(20, 10, 12, 9)), "second candidate was not saved");
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays().Count);
            AssertEqual(0, lastCandidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.CandidatesConfirmed, lastReason);
            AssertTrue(File.Exists(labelPath), "all-candidate label file was not saved");
            AssertEqual(2, File.ReadAllLines(labelPath).Length);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultRejectsStaleConfirmation()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var requestedData = new CData();
            requestedData.ConfigureOutputRoot(root);
            requestedData.ProjectSettings.YoloDataset.ValidationPercent = 0;
            requestedData.LastSelectImageName = "requested-image";
            requestedData.LastSelectImagePath = Path.Combine(root, "requested-image.bmp");
            requestedData.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            var activeData = new CData();
            activeData.ConfigureOutputRoot(root);
            activeData.ProjectSettings.YoloDataset.ValidationPercent = 0;
            activeData.LastSelectImageName = "active-image";
            activeData.LastSelectImagePath = Path.Combine(root, "active-image.bmp");
            activeData.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            CGlobal.Inst.Data = requestedData;
            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(requestedData, image.Size);
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            CGlobal.Inst.Data = activeData;
            bool confirmed = service.CommitLastDetectionToMainLabels(activeData, new CSystem());
            string activeLabelPath = Path.Combine(root, "data", "train", "labels", "active-image.txt");

            AssertTrue(applied, "fresh detection result should be applied before the image changes");
            AssertTrue(!confirmed, "stale detection result should not be confirmed for a different image");
            AssertTrue(!File.Exists(activeLabelPath), "stale detection labels should not be saved under the active image");
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestLabelingWorkflowReloadsSavedAnnotations()
    {
        List<FormLayerDisplay> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<FormLayerDisplay>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["Part"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(2, 4, 10, 8), cClassItem = data.ClassNamedList[0] }
                }
            };

            YoloAnnotationService.SaveAnnotations("sample.png", image, rois, data.ClassNamedList, data);
            string trainImage = Path.Combine(root, "data", "train", "images", "sample.jpeg");

            var service = new LabelingWorkflowService();
            bool loaded = service.LoadSavedAnnotationsToMainDisplay(trainImage, image.Size, data);
            IReadOnlyList<LabelingRoiListItem> items = service.GetMainRoiItems();

            AssertTrue(loaded, "saved YOLO labels were not loaded");
            AssertEqual(1, items.Count);
            AssertEqual("Part", items[0].ClassName);
            AssertEqual(new Rectangle(2, 4, 10, 8), items[0].Roi);

            bool emptyLoaded = service.LoadSavedAnnotationsToMainDisplay(
                Path.Combine(root, "data", "train", "images", "empty.jpeg"),
                image.Size,
                data);
            AssertTrue(!emptyLoaded, "missing labels should report no saved annotations");
            AssertEqual(0, service.GetMainRoiItems().Count);
        }
        finally
        {
            foreach (FormLayerDisplay display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            DeleteTempRoot(root);
        }
    }

    private static void TestMeasurementGeometry()
    {
        AssertEqual(5D, MeasurementGeometry.Distance(new Point(0, 0), new Point(3, 4)));

        bool calculated = MeasurementGeometry.TryCalculateVerticalMeasurement(
            new Point(10, 10),
            new Point(90, 10),
            new Point(50, 50),
            new Size(100, 100),
            out Point verticalPoint,
            out double pixelDistance);

        AssertTrue(calculated, "vertical measurement was not calculated");
        AssertEqual(new Point(50, 10), verticalPoint);
        AssertEqual(40D, pixelDistance);

        using var viewer = new CViewer();
        viewer.SetMeasurementOverlay(new Point(1, 1), new Point(4, 5), new Point(4, 5), new Point(4, 1), 0.5F);
        viewer.ClearMeasurementOverlay();
    }

    private static void TestDetectionOverlays()
    {
        var defects = new List<DefectInfo>
        {
            new DefectInfo { ClassName = "OK", Confidence = 0.9876F, X = 1.234F, Y = 2.345F, Width = 30.456F, Height = 40.567F },
            new DefectInfo { ClassName = "NG", Confidence = 0.1234F, X = 5, Y = 6, Width = 0, Height = 7 }
        };

        List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(
            defects,
            className => string.Equals(className, "OK", StringComparison.OrdinalIgnoreCase) ? Color.Blue : null);

        AssertEqual(1, overlays.Count);
        AssertEqual(1, overlays[0].CandidateIndex);
        AssertEqual("OK", overlays[0].ClassName);
        AssertEqual(0.98F, overlays[0].Confidence);
        AssertEqual(new RectangleF(1.23F, 2.34F, 30.45F, 40.56F), overlays[0].Bounds);
        AssertEqual(Color.Blue, overlays[0].Color);
        AssertTrue(!overlays[0].IsSelected, "overlay should not be selected by default");
        AssertEqual("AI #1 OK 98%", overlays[0].Label);

        List<DetectionOverlayItem> selectedOverlays = PythonDetectionResultProtocol.BuildDetectionOverlays(defects, null, selectedCandidateIndex: 1);
        AssertTrue(selectedOverlays[0].IsSelected, "selected overlay was not marked");
        AssertEqual("AI #1 OK 98%", selectedOverlays[0].Label);

        using var viewer = new CViewer();
        viewer.SetDetectionOverlays(overlays);
        viewer.ClearDetectionOverlays();

        Color fallbackOverlayColor = InvokePrivateStaticResult<Color>(typeof(CViewer), "EnsureReadableOverlayColor", Color.Black);
        AssertEqual(Color.FromArgb(72, 190, 255), fallbackOverlayColor);
    }

    private static void TestCViewerDetectionOverlayRenderLifecycle()
    {
        using var form = new Form
        {
            Size = new Size(640, 480),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, 100),
            ShowInTaskbar = false,
            Text = "OverlayRenderLifecycle"
        };
        using var host = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(host);
        using var viewer = new CViewer();
        viewer.AttachTo(host);

        using var image = new Bitmap(120, 100);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.FromArgb(70, 70, 70));
            using var fill = new SolidBrush(Color.FromArgb(150, 150, 150));
            graphics.FillEllipse(fill, 30, 25, 65, 55);
        }

        form.Show();
        Application.DoEvents();
        viewer.LoadMainImage(image, "overlay_lifecycle.bmp");
        viewer.SetDetectionOverlays(new[]
        {
            new DetectionOverlayItem
            {
                CandidateIndex = 1,
                ClassName = "OK",
                Confidence = 0.95F,
                Bounds = new RectangleF(20, 20, 80, 55),
                Color = Color.LimeGreen
            },
            new DetectionOverlayItem
            {
                CandidateIndex = 2,
                ClassName = "NG",
                Confidence = 0.91F,
                Bounds = new RectangleF(50, 38, 22, 34),
                Color = Color.Red,
                IsSelected = true
            }
        });

        for (int i = 0; i < 8; i++)
        {
            viewer.Canvas.RefreshGL();
            Application.DoEvents();
            Thread.Sleep(20);
        }

        AssertEqual(2, viewer.DetectionOverlayCount);
        viewer.Dispose();
        Application.DoEvents();
        form.Close();
    }

    private static int GetAvailableTcpPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void RunMockDetectionClient(int port, ManualResetEventSlim receivedRequest)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using NetworkStream stream = client.GetStream();
        stream.ReadTimeout = 5000;
        stream.WriteTimeout = 5000;

        byte[] packet = ReadStartDefectPacket(stream);
        string command = Encoding.ASCII.GetString(packet, 0, Math.Min(packet.Length, CCommunicationLearning.CommandLearning.StartDefect.ToString().Length));
        AssertTrue(command.StartsWith(CCommunicationLearning.CommandLearning.StartDefect.ToString(), StringComparison.Ordinal), "mock client received an unexpected command");
        receivedRequest.Set();

        string result = "{\"type\":\"ResultDefect\",\"version\":1,\"imageId\":\"tcp-round-trip\",\"items\":[{\"className\":\"NG\",\"confidence\":0.98,\"x\":4,\"y\":5,\"width\":12,\"height\":9}]}";
        byte[] resultBytes = Encoding.UTF8.GetBytes(result);
        stream.Write(resultBytes, 0, resultBytes.Length);
        stream.Flush();
    }

    private static byte[] ReadStartDefectPacket(NetworkStream stream)
    {
        var buffer = new List<byte>();
        byte[] chunk = new byte[4096];
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow <= deadline)
        {
            int read = stream.Read(chunk, 0, chunk.Length);
            if (read <= 0)
            {
                break;
            }

            buffer.AddRange(chunk.Take(read));
            int markerIndex = IndexOf(buffer, Encoding.ASCII.GetBytes("IEND"));
            if (markerIndex >= 0 && buffer.Count >= markerIndex + 8)
            {
                return buffer.ToArray();
            }
        }

        throw new InvalidOperationException("mock client timed out while reading StartDefect packet");
    }

    private static int IndexOf(List<byte> source, byte[] pattern)
    {
        if (source == null || pattern == null || pattern.Length == 0 || source.Count < pattern.Length)
        {
            return -1;
        }

        for (int i = 0; i <= source.Count - pattern.Length; i++)
        {
            bool matched = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static RealYoloSmokeSettings BuildRealYoloSmokeSettings(string artifactRoot)
    {
        var defaults = new PythonModelSettings();
        defaults.EnsureDefaults();

        string projectRoot = GetEnvironmentValue("LABELING_SMOKE_PROJECT_ROOT", defaults.ProjectRootPath);
        string clientScriptPath = GetEnvironmentValue("LABELING_SMOKE_CLIENT_SCRIPT", Path.Combine(projectRoot, "labelling_tcp_client.py"));
        string weightsPath = GetEnvironmentValue("LABELING_SMOKE_WEIGHTS", Path.Combine(projectRoot, "best.pt"));
        string imageRootPath = GetEnvironmentValue("LABELING_SMOKE_IMAGE_ROOT", defaults.ImageRootPath);
        string imagePath = GetEnvironmentValue("LABELING_SMOKE_IMAGE_PATH", Path.Combine(imageRootPath, "Teaching_0.bmp"));
        string modelRootPath = GetEnvironmentValue("LABELING_SMOKE_MODEL_ROOT", Path.Combine(projectRoot, "yolov5Master"));
        string venvPythonPath = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        string defaultPythonPath = File.Exists(venvPythonPath)
            ? venvPythonPath
            : PythonModelSettingsValidator.ResolvePythonExecutable(defaults);

        return new RealYoloSmokeSettings
        {
            ArtifactRoot = artifactRoot,
            PythonExecutablePath = GetEnvironmentValue("LABELING_SMOKE_PYTHON_EXE", defaultPythonPath),
            ProjectRootPath = projectRoot,
            ClientScriptPath = clientScriptPath,
            ModelRootPath = modelRootPath,
            WeightsPath = weightsPath,
            ImageRootPath = imageRootPath,
            ImagePath = imagePath,
            Device = GetEnvironmentValue("LABELING_SMOKE_DEVICE", "cpu"),
            ImageSize = GetEnvironmentInt("LABELING_SMOKE_IMAGE_SIZE", 640),
            MinimumConfidence = GetEnvironmentFloat("LABELING_SMOKE_CONFIDENCE", defaults.MinimumDetectionConfidence),
            Iou = GetEnvironmentFloat("LABELING_SMOKE_IOU", 0.45F)
        };
    }

    private static void AssertRealYoloSmokeInputs(RealYoloSmokeSettings settings)
    {
        AssertTrue(settings != null, "real YOLO smoke settings were not created");
        AssertTrue(!PythonModelSettingsValidator.LooksLikePath(settings.PythonExecutablePath) || File.Exists(settings.PythonExecutablePath), $"Python executable was not found: {settings.PythonExecutablePath}");
        AssertTrue(Directory.Exists(settings.ProjectRootPath), $"YOLO project root was not found: {settings.ProjectRootPath}");
        AssertTrue(File.Exists(settings.ClientScriptPath), $"YOLO TCP client script was not found: {settings.ClientScriptPath}");
        AssertTrue(Directory.Exists(settings.ModelRootPath), $"YOLO model root was not found: {settings.ModelRootPath}");
        AssertTrue(File.Exists(settings.WeightsPath), $"YOLO weights were not found: {settings.WeightsPath}");
        AssertTrue(Directory.Exists(settings.ImageRootPath), $"YOLO image root was not found: {settings.ImageRootPath}");
        AssertTrue(File.Exists(settings.ImagePath), $"YOLO smoke image was not found: {settings.ImagePath}");
        AssertTrue(settings.ImageSize > 0, $"YOLO smoke image size must be positive: {settings.ImageSize}");
        AssertTrue(settings.MinimumConfidence >= 0F && settings.MinimumConfidence <= 1F, $"YOLO smoke confidence must be between 0 and 1: {settings.MinimumConfidence}");
        AssertTrue(settings.Iou >= 0F && settings.Iou <= 1F, $"YOLO smoke IoU must be between 0 and 1: {settings.Iou}");
    }

    private static Process StartRealYoloClient(
        RealYoloSmokeSettings settings,
        int port,
        int timeoutSeconds,
        StringBuilder stdout,
        StringBuilder stderr)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = settings.PythonExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(settings.ClientScriptPath) ?? settings.ProjectRootPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.ArgumentList.Add(settings.ClientScriptPath);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--timeout");
        startInfo.ArgumentList.Add(timeoutSeconds.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--weights");
        startInfo.ArgumentList.Add(settings.WeightsPath);
        startInfo.ArgumentList.Add("--model-root");
        startInfo.ArgumentList.Add(settings.ModelRootPath);
        startInfo.ArgumentList.Add("--image-root");
        startInfo.ArgumentList.Add(settings.ImageRootPath);
        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add(settings.Device ?? string.Empty);
        startInfo.ArgumentList.Add("--img-size");
        startInfo.ArgumentList.Add(settings.ImageSize.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--conf");
        startInfo.ArgumentList.Add(settings.MinimumConfidence.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--iou");
        startInfo.ArgumentList.Add(settings.Iou.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--once");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) => AppendProcessLine(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => AppendProcessLine(stderr, e.Data);

        AssertTrue(process.Start(), "real YOLO Python client process did not start");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void AssertRealYoloClientExitedCleanly(Process process, StringBuilder stdout, StringBuilder stderr)
    {
        if (process == null)
        {
            return;
        }

        if (!process.WaitForExit(10000))
        {
            throw new InvalidOperationException(BuildRealYoloSmokeFailure("real YOLO Python client did not exit after --once", stdout, stderr));
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildRealYoloSmokeFailure($"real YOLO Python client exited with code {process.ExitCode}", stdout, stderr));
        }
    }

    private static void StopRealYoloClient(Process process)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string CreateRealYoloSmokeArtifactRoot()
    {
        string configuredRoot = GetEnvironmentValue("LABELING_SMOKE_ARTIFACT_ROOT", string.Empty);
        string root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetFullPath("artifacts"), "real-yolo-smoke", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))
            : configuredRoot;

        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteRealYoloSmokeSummary(
        string artifactRoot,
        RealYoloSmokeSettings settings,
        IReadOnlyList<DetectionCandidateReviewItem> candidates,
        IReadOnlyList<LabelingRoiListItem> committedItems,
        string labelPath,
        string reviewStatusPath,
        StringBuilder stdout,
        StringBuilder stderr)
    {
        Directory.CreateDirectory(artifactRoot);
        var lines = new List<string>
        {
            $"createdAt={DateTime.Now:O}",
            $"python={settings.PythonExecutablePath}",
            $"client={settings.ClientScriptPath}",
            $"weights={settings.WeightsPath}",
            $"image={settings.ImagePath}",
            $"labelPath={labelPath}",
            $"reviewStatusPath={reviewStatusPath}",
            $"candidateCount={candidates?.Count ?? 0}",
            $"committedCount={committedItems?.Count ?? 0}"
        };

        if (candidates != null)
        {
            lines.AddRange(candidates.Select(item => $"candidate[{item.Index}]={item.ClassName},{item.Confidence.ToString("0.####", CultureInfo.InvariantCulture)},{item.ClippedBounds}"));
        }

        if (committedItems != null)
        {
            lines.AddRange(committedItems.Select(item => $"label[{item.Index}]={item.ClassName},{item.Roi}"));
        }

        File.WriteAllLines(Path.Combine(artifactRoot, "summary.txt"), lines);
        WriteRealYoloProcessLog(artifactRoot, stdout, stderr);
    }

    private static string VerifyRealYoloReviewStatus(CData data, string imagePath, Size imageSize)
    {
        var reviewStatus = new YoloImageReviewStatusService();
        reviewStatus.SetImages(new[] { imagePath });
        YoloImageReviewStatus confirmed = reviewStatus.RefreshLabelStatusAndReviewState(
            imagePath,
            imageSize,
            data,
            hasActiveCandidates: false);

        AssertTrue(confirmed != null, "real YOLO review status was not created");
        AssertEqual("Confirmed", confirmed.DetectionText);
        AssertTrue(confirmed.IsLabeled, "real YOLO confirmed review status did not include label objects");

        reviewStatus.SaveReviewStatus(data);
        string reviewStatusPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(data);
        AssertTrue(File.Exists(reviewStatusPath), $"real YOLO review status file was not saved: {reviewStatusPath}");

        var restored = new YoloImageReviewStatusService();
        restored.LoadReviewStatus(data, new[] { imagePath });
        AssertEqual("Confirmed", restored.GetOrCreate(imagePath).DetectionText);
        return reviewStatusPath;
    }

    private static void WriteRealYoloProcessLog(string artifactRoot, StringBuilder stdout, StringBuilder stderr)
    {
        if (string.IsNullOrWhiteSpace(artifactRoot))
        {
            return;
        }

        Directory.CreateDirectory(artifactRoot);
        File.WriteAllText(Path.Combine(artifactRoot, "python-stdout.txt"), Snapshot(stdout));
        File.WriteAllText(Path.Combine(artifactRoot, "python-stderr.txt"), Snapshot(stderr));
    }

    private static string BuildRealYoloSmokeFailure(string message, StringBuilder stdout, StringBuilder stderr)
    {
        string stderrText = TrimForMessage(Snapshot(stderr), 1600);
        string stdoutText = TrimForMessage(Snapshot(stdout), 1600);
        return $"{message}. stderr: {stderrText} stdout: {stdoutText}";
    }

    private static void AppendProcessLine(StringBuilder builder, string line)
    {
        if (builder == null || line == null)
        {
            return;
        }

        lock (builder)
        {
            builder.AppendLine(line);
        }
    }

    private static string Snapshot(StringBuilder builder)
    {
        if (builder == null)
        {
            return string.Empty;
        }

        lock (builder)
        {
            return builder.ToString();
        }
    }

    private static string TrimForMessage(string text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(empty)";
        }

        text = text.Trim();
        if (text.Length <= maximumLength)
        {
            return text;
        }

        return text.Substring(text.Length - maximumLength);
    }

    private static string GetEnvironmentValue(string name, string fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int GetEnvironmentInt(string name, int fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    private static float GetEnvironmentFloat(string name, float fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow <= deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    private static bool PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow <= deadline)
        {
            Application.DoEvents();
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        Application.DoEvents();
        return condition();
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "LabelingApplication.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertArgumentValue(System.Collections.Generic.IList<string> arguments, string flag, string expectedValue)
    {
        int index = arguments.IndexOf(flag);
        AssertTrue(index >= 0, $"argument flag was not passed to Python: {flag}");
        AssertTrue(index + 1 < arguments.Count, $"argument flag did not include a value: {flag}");
        AssertEqual(expectedValue, arguments[index + 1]);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private sealed class RealYoloSmokeSettings
    {
        public string ArtifactRoot { get; set; }
        public string PythonExecutablePath { get; set; }
        public string ProjectRootPath { get; set; }
        public string ClientScriptPath { get; set; }
        public string ModelRootPath { get; set; }
        public string WeightsPath { get; set; }
        public string ImageRootPath { get; set; }
        public string ImagePath { get; set; }
        public string Device { get; set; }
        public int ImageSize { get; set; }
        public float MinimumConfidence { get; set; }
        public float Iou { get; set; }
    }
}
