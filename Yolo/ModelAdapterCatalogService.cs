using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class ModelAdapterCatalogItem
    {
        public string AdapterKey { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string AvailabilityText { get; init; } = string.Empty;

        public string TaskContractText { get; init; } = string.Empty;

        public string DataContractText { get; init; } = string.Empty;

        public string RuntimeContractText { get; init; } = string.Empty;

        public string EvidenceContractText { get; init; } = string.Empty;

        public string NextActionText { get; init; } = string.Empty;
    }

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
                    DisplayName = "레시피 상호 변환 포맷",
                    AvailabilityText = "구현된 데이터셋 변환기",
                    TaskContractText = "객체 탐지와 세그멘테이션은 현재 레시피 목적에 맞춰 내보냅니다.",
                    DataContractText = "지원 내보내기: " + interchangeFormats + ".",
                    RuntimeContractText = "데이터 포맷 변환기이며, 실행 가능한 모델 런타임이 아닙니다.",
                    EvidenceContractText = "각 포맷의 전용 검증을 유지합니다. 포맷 변환 성공은 모델 채택 근거가 아닙니다.",
                    NextActionText = "필요한 입력 포맷과 작업을 확인한 뒤에만 모델 어댑터를 선택하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "yolov5-detect",
                    DisplayName = "YOLOv5 객체 탐지",
                    AvailabilityText = "검증된 로컬 학습·추론 어댑터",
                    TaskContractText = "객체 탐지만 지원합니다.",
                    DataContractText = "클래스 순서, 정규화 xywh 박스, 선언된 분할을 갖는 레시피 소유 YOLO 탐지 디렉터리를 사용합니다.",
                    RuntimeContractText = "실행 전 로컬 YOLOv5 프로필의 Python·저장소·클라이언트·가중치 경로가 모두 해석되어야 합니다.",
                    EvidenceContractText = "학습·예측 검토·비교에는 같은 분할/지문과 보존된 런타임·가중치 식별자가 필요합니다.",
                    NextActionText = "학습 또는 비교 전 기존 데이터 준비 상태 검사를 실행하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "yolov8-local",
                    DisplayName = "YOLOv8 로컬 워커",
                    AvailabilityText = "검증된 로컬 어댑터",
                    TaskContractText = "객체 탐지·세그멘테이션·이미지 단위 이상 분류는 각각 별도 작업 계약을 따릅니다.",
                    DataContractText = "레시피 소유 YOLO 내보내기 또는 원본 지문이 일치하는 명시적 활성화 native data.yaml을 사용합니다.",
                    RuntimeContractText = "로컬 Ultralytics 워커, 작업 인지 학습 요청, 정확한 가중치 식별자가 필요합니다.",
                    EvidenceContractText = "후보 런타임 성공, 홀드아웃 평가, 모델 채택은 서로 다른 결정입니다.",
                    NextActionText = "먼저 작업을 선택하고 학습 전에 데이터 준비 상태와 런타임을 확인하세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "onnx-inference",
                    DisplayName = "ONNX 추론",
                    AvailabilityText = "추론 전용(Inference-only) 어댑터",
                    TaskContractText = "현재 이미지 또는 배치 추론만 지원하며, 앱 소유 ONNX 학습 경로는 없습니다.",
                    DataContractText = "외부 모델은 호환되는 입력 크기·출력 구조·클래스 매핑·신뢰도 의미를 제공해야 합니다.",
                    RuntimeContractText = "검증된 ONNX 추론 프로필과 명시적 모델 아티팩트를 연결해야 합니다.",
                    EvidenceContractText = "비교 또는 채택을 결정하기 전에 추론 결과를 레시피 라벨과 검토해야 합니다.",
                    NextActionText = "변환된 데이터셋만으로 ONNX 모델의 학습·비교 가능성을 증명했다고 보지 마세요."
                },
                new ModelAdapterCatalogItem
                {
                    AdapterKey = "yolo11-local",
                    DisplayName = "YOLO11",
                    AvailabilityText = "검증된 로컬 Ultralytics 런타임",
                    TaskContractText = "객체 탐지 데이터셋에서 학습·검증·추론과 YOLOv8 대비 실행을 지원합니다.",
                    DataContractText = "원본 native data.yaml과 YOLO bbox 라벨을 그대로 사용하며, 앱이 원본을 변환하거나 덮어쓰지 않습니다.",
                    RuntimeContractText = "YOLO11을 선택하면 연결된 로컬 Ultralytics 폴더와 기본 yolo11n.pt 시작 가중치를 사용합니다. 실제 실행 전 경로와 가중치를 확인하세요.",
                    EvidenceContractText = "비교 결과는 같은 데이터 분할·가중치·실행 환경의 벤치마크입니다. 자동 채택 근거가 아니며 별도 홀드아웃 검증이 필요합니다.",
                    NextActionText = "YOLO11을 선택한 뒤 로컬 폴더 연결 상태를 확인하고, 같은 데이터셋에서 YOLOv8 대비 실행을 시작하세요."
                }
            };
        }
    }
}
