using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestModelAdapterCatalog()
    {
        var catalog = ModelAdapterCatalogService.BuildCatalog();

        AssertEqual(5, catalog.Count);
        AssertEqual(
            "recipe-interchange,yolov5-detect,yolov8-local,onnx-inference,yolo11-blocked",
            string.Join(",", catalog.Select(item => item.AdapterKey)));
        AssertTrue(
            catalog.All(item => !string.IsNullOrWhiteSpace(item.DisplayName)
                                && !string.IsNullOrWhiteSpace(item.AvailabilityText)
                                && !string.IsNullOrWhiteSpace(item.TaskContractText)
                                && !string.IsNullOrWhiteSpace(item.DataContractText)
                                && !string.IsNullOrWhiteSpace(item.RuntimeContractText)
                                && !string.IsNullOrWhiteSpace(item.EvidenceContractText)
                                && !string.IsNullOrWhiteSpace(item.NextActionText)),
            "every adapter must declare task, data, runtime, evidence, and next-action contracts");

        ModelAdapterCatalogItem interchange = catalog.Single(item => item.AdapterKey == "recipe-interchange");
        AssertTrue(interchange.DataContractText.Contains("COCO", StringComparison.Ordinal), "implemented interchange should name COCO export");
        AssertTrue(interchange.DataContractText.Contains("Pascal VOC", StringComparison.Ordinal), "implemented interchange should name Pascal VOC export");
        AssertTrue(interchange.DataContractText.Contains("Label Studio", StringComparison.Ordinal), "implemented interchange should name Label Studio export");
        AssertTrue(interchange.DataContractText.Contains("CVAT", StringComparison.Ordinal), "implemented interchange should name CVAT export");
        AssertTrue(interchange.RuntimeContractText.Contains("모델 런타임이 아닙니다", StringComparison.Ordinal), "data export must not be represented as a runnable model runtime");

        ModelAdapterCatalogItem yolov5 = catalog.Single(item => item.AdapterKey == "yolov5-detect");
        AssertTrue(yolov5.TaskContractText.Contains("객체 탐지만", StringComparison.Ordinal), "YOLOv5 contract should stay detection-only");
        AssertTrue(yolov5.DataContractText.Contains("정규화 xywh", StringComparison.Ordinal), "YOLOv5 contract should retain label geometry requirements");

        ModelAdapterCatalogItem yolov8 = catalog.Single(item => item.AdapterKey == "yolov8-local");
        AssertTrue(yolov8.TaskContractText.Contains("세그멘테이션", StringComparison.Ordinal), "YOLOv8 contract should expose segmentation separately from detection");
        AssertTrue(yolov8.DataContractText.Contains("native data.yaml", StringComparison.OrdinalIgnoreCase), "YOLOv8 contract should describe native YAML intake provenance");

        ModelAdapterCatalogItem onnx = catalog.Single(item => item.AdapterKey == "onnx-inference");
        AssertTrue(onnx.AvailabilityText.Contains("Inference-only", StringComparison.OrdinalIgnoreCase), "ONNX contract should not imply application-owned training");
        AssertTrue(onnx.NextActionText.Contains("보지 마세요", StringComparison.Ordinal), "ONNX contract should prevent conversion from being mistaken for training evidence");

        ModelAdapterCatalogItem yolo11 = catalog.Single(item => item.AdapterKey == "yolo11-blocked");
        AssertTrue(yolo11.AvailabilityText.EndsWith("차단됨", StringComparison.Ordinal), "YOLO11 must remain visibly blocked");
        AssertTrue(yolo11.DataContractText.Contains("내보내거나 변환하지 마세요", StringComparison.Ordinal), "YOLO11 must not invite unsupported recipe conversion");

        var viewModel = new WpfYoloModelSettingsPanelViewModel();
        viewModel.LoadFrom(new PythonModelSettings
        {
            ModelEngine = PythonModelSettings.EngineYoloV8
        });
        AssertEqual(catalog.Count, viewModel.ModelAdapterCatalogItems.Count);
        AssertTrue(
            viewModel.ModelAdapterCatalogItems.Any(item => item.AdapterKey == "yolo11-blocked"),
            "model settings panel should keep the blocked YOLO11 boundary visible");

        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfYoloModelSettingsPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        AssertNamedXamlElement(xaml, xName, "Expander", "ModelAdapterCatalogExpander");
        AssertNamedXamlBinding(xaml, xName, "ModelAdapterCatalogBoundaryText", "Text", "ModelAdapterCatalogBoundaryText");
        AssertNamedXamlBinding(xaml, xName, "ModelAdapterCatalogItems", "ItemsSource", "ModelAdapterCatalogItems");
    }
}
