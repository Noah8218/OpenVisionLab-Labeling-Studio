using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class ModelAdapterCatalogService
    {
        public static IReadOnlyList<ModelAdapterCatalogItem> BuildCatalog()
        {
            string interchangeFormats = string.Join(
                ", ",
                DatasetExportCapabilityService.BuildImplementedCapabilities()
                    .Where(item => string.Equals(item.Direction, "export", StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

            return new[]
            {
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "recipe-interchange",
                    DisplayName = "데이터셋 내보내기 포맷",
                    AvailabilityText = "데이터셋 포맷 변환 지원",
                    TaskContractText = "현재 레시피 목적에 맞춰 객체 탐지·세그멘테이션 데이터를 내보냅니다.",
                    DataContractText = "지원하는 내보내기 포맷: " + interchangeFormats + ".",
                    RuntimeContractText = "데이터 포맷을 변환합니다. 모델 학습·추론을 실행하는 기능은 아닙니다.",
                    EvidenceContractText = "포맷별 조건을 확인해 변환합니다. 변환에 성공해도 검사 모델로 지정할 수 있다는 뜻은 아닙니다.",
                    NextActionText = "사용할 데이터 포맷과 작업을 먼저 확인하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "yolov5-detect",
                    DisplayName = "YOLOv5 객체 탐지",
                    AvailabilityText = "로컬 학습·추론 지원",
                    TaskContractText = "객체 탐지만 지원합니다.",
                    DataContractText = "레시피의 YOLO 객체 탐지 데이터를 사용합니다. 클래스 순서, 정규화 xywh 박스와 데이터 분할이 일치해야 합니다.",
                    RuntimeContractText = "실행 전에 YOLOv5의 Python, 실행 환경 폴더, 통신 스크립트와 모델 파일 경로를 확인하세요.",
                    EvidenceContractText = "학습·추론 결과 확인·비교는 같은 데이터 분할과 원본 확인 정보를 사용해야 합니다. 실행 환경과 모델 파일의 식별 정보도 유지합니다.",
                    NextActionText = "학습이나 비교 전에 데이터셋 준비 상태를 점검하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "yolov8-local",
                    DisplayName = "YOLOv8 로컬 실행 환경",
                    AvailabilityText = "로컬 실행 지원",
                    TaskContractText = "객체 탐지·세그멘테이션·이미지 단위 이상탐지는 작업별로 지원 조건이 다릅니다.",
                    DataContractText = "레시피에서 내보낸 YOLO 데이터나, 원본 확인 정보가 일치하고 사용하도록 지정한 native data.yaml을 사용합니다.",
                    RuntimeContractText = "로컬 Ultralytics 실행 환경, 작업에 맞는 학습 요청과 정확한 모델 파일 식별 정보가 필요합니다.",
                    EvidenceContractText = "실행 성공, 별도 평가 데이터의 결과 확인, 검사 모델 지정은 각각 확인해야 합니다.",
                    NextActionText = "작업을 먼저 선택하고 학습 전에 데이터와 실행 환경을 확인하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "unet-segmentation",
                    DisplayName = "U-Net 세그멘테이션",
                    AvailabilityText = "로컬 PyTorch 학습·추론·비교 지원",
                    TaskContractText = "세그멘테이션만 지원합니다. 객체 탐지 박스나 이상탐지 OK/NG 판정에는 사용하지 않습니다.",
                    DataContractText = "레시피 마스크·폴리곤 또는 사용하도록 지정한 native YOLO segmentation data.yaml을 앱의 마스크 데이터로 변환합니다. 원본은 변경하지 않습니다.",
                    RuntimeContractText = $"별도 U-Net PyTorch 환경과 번들 {Path.GetFileName(PythonModelRuntimeBundledWorkerService.UnetWorkerRelativePath)}를 사용합니다. 시작 가중치 없이 학습한 호환 best.pt만 추론에 사용합니다.",
                    EvidenceContractText = "U-Net과 YOLO-seg는 같은 테스트 마스크의 Dice/IoU로 비교합니다. 박스 mAP와 섞지 않으며 검사 모델로 자동 지정하지 않습니다.",
                    NextActionText = "세그멘테이션 레시피와 U-Net 실행 환경을 확인한 뒤 학습이나 U-Net/YOLO-seg 비교를 시작하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "patchcore-anomaly",
                    DisplayName = "PatchCore 정상 이미지 학습 이상탐지",
                    AvailabilityText = "로컬 PyTorch 정상 이미지 학습·추론 지원",
                    TaskContractText = "이상탐지 레시피의 정상 이미지만 학습합니다. 이미지 OK/NG 판정과 결함 위치 후보를 함께 보여줍니다.",
                    DataContractText = "정상으로 판정한 학습 이미지만 메모리 뱅크에 사용합니다. 정상 검증 이미지는 판정 기준 보정에 쓰며, NG 이미지는 학습 특징에 포함하지 않습니다.",
                    RuntimeContractText = $"번들 {Path.GetFileName(PythonModelRuntimeBundledWorkerService.PatchCoreWorkerRelativePath)}와 torch·torchvision·numpy·Pillow를 사용합니다. 학습한 best.pt에 특징 추출기와 메모리 뱅크가 포함됩니다.",
                    EvidenceContractText = "히트맵·위치 후보는 결과 확인을 돕습니다. 라벨을 자동 저장하거나 현장 성능을 보장하지 않습니다. 같은 별도 평가 데이터에서 YOLO 분류와 비교하세요.",
                    NextActionText = "정상 학습 이미지 2장 이상과 가능하면 별도 정상 검증 이미지를 준비하세요. 학습 후 확정 전 위치 후보와 히트맵을 확인하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "onnx-inference",
                    DisplayName = "ONNX 추론",
                    AvailabilityText = "추론 전용",
                    TaskContractText = "현재 이미지·일괄 추론만 지원합니다. 앱에서 ONNX 모델을 학습하는 기능은 없습니다.",
                    DataContractText = "외부 모델은 입력 크기, 출력 형식, 클래스 연결과 신뢰도 의미가 호환되어야 합니다.",
                    RuntimeContractText = "확인된 ONNX 실행 환경과 사용할 모델 파일을 연결하세요.",
                    EvidenceContractText = "추론 결과를 레시피의 정답 라벨과 비교해 확인한 뒤 비교나 검사 모델 지정을 결정하세요.",
                    NextActionText = "데이터셋 포맷을 변환해도 ONNX 모델의 학습·비교 지원 여부는 별도로 확인해야 합니다."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "yolo11-local",
                    DisplayName = "YOLO11",
                    AvailabilityText = "로컬 Ultralytics 객체 탐지·세그멘테이션·이상탐지 실행 환경",
                    TaskContractText = "객체 탐지·세그멘테이션·이미지 단위 이상탐지의 학습·평가·추론과 YOLOv8 비교를 지원합니다.",
                    DataContractText = "객체 탐지·세그멘테이션은 작업에 맞는 native data.yaml과 YOLO 라벨을 씁니다. 이상탐지는 OK/NG로 판정한 레시피 이미지를 normal/abnormal 폴더로 내보냅니다. 원본은 덮어쓰지 않습니다.",
                    RuntimeContractText = "연결한 로컬 Ultralytics 환경에서 작업에 맞춰 yolo11n.pt, yolo11n-seg.pt 또는 yolo11n-cls.pt를 사용합니다.\r\n외부 YOLO11 포크는 지원 미확인입니다.",
                    EvidenceContractText = "같은 데이터 분할·모델 파일·실행 환경의 결과만 비교하세요.\r\n현재 이상탐지 후보의 신뢰도 0.8 결과는 82/104로 보류 상태이며 검사 모델로 자동 지정하지 않습니다.",
                    NextActionText = "레시피 작업을 확인한 뒤 같은 데이터셋 버전으로 YOLOv8과 학습·평가 결과를 비교하세요."
                }
            };
        }
    }
}
