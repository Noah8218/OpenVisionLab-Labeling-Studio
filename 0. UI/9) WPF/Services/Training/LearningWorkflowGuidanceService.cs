using OpenVisionLab;
using System;
using System.Globalization;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the pure, user-facing guidance text for the learning workflow.
    /// The ViewModel owns selections and mutable presentation state; this owner
    /// keeps wording and mode/step/tool mapping out of that state container.
    /// </summary>
    public static class LearningWorkflowGuidanceService
    {
        public static string BuildObjectDetectionMvpNextActionText(string actionText)
        {
            if (string.IsNullOrWhiteSpace(actionText))
            {
                return Translate("WpfLearningWorkflow.ObjectDetectionMvpNextAction.Empty");
            }

            string normalized = actionText.Trim();
            if (normalized.StartsWith("\uC644\uB8CC:", StringComparison.Ordinal))
            {
                return Translate("WpfLearningWorkflow.ObjectDetectionMvpNextAction.Complete");
            }

            const string nextPrefix = "\uB2E4\uC74C:";
            if (normalized.StartsWith(nextPrefix, StringComparison.Ordinal))
            {
                normalized = normalized.Substring(nextPrefix.Length).Trim();
            }

            return Format(
                "WpfLearningWorkflow.ObjectDetectionMvpNextAction.Dynamic",
                LocalizationTextRuntimeService.Translate(normalized));
        }

        public static string BuildCurrentYoloTrainingActionText(WpfYoloTrainingWorkflowStepItem step)
        {
            return step?.Order switch
            {
                1 => "데이터셋 만들기",
                2 => "이미지 불러오기",
                3 => "클래스 등록",
                4 => "라벨링 시작",
                5 => "데이터셋 점검",
                6 => "학습 설정 확인",
                7 => "AI 후보 검토",
                _ => "이 단계로 이동"
            };
        }

        public static string FormatHistoryActionSuffix(string actionName)
            => string.IsNullOrWhiteSpace(actionName) ? string.Empty : $": {actionName}";

        public static string FormatLiveLabelingTaskToolText(string toolText)
        {
            string value = toolText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return "\uB3C4\uAD6C: \uC120\uD0DD";
            }

            if (value.IndexOf("\uAC80\uC0AC", StringComparison.Ordinal) >= 0
                || value.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("\uD050", StringComparison.Ordinal) >= 0)
            {
                return $"\uBAA8\uB4DC: {value}";
            }

            return $"\uB3C4\uAD6C: {value}";
        }

        public static string BuildDatasetPurposeSummaryText(WpfLearningMode mode)
        {
            return mode switch
            {
                WpfLearningMode.ObjectDetection => Translate("WpfLearningWorkflow.DatasetPurpose.ObjectDetection"),
                WpfLearningMode.Segmentation => Translate("WpfLearningWorkflow.DatasetPurpose.Segmentation"),
                WpfLearningMode.AnomalyDetection => Translate("WpfLearningWorkflow.DatasetPurpose.AnomalyDetection"),
                WpfLearningMode.Train => Translate("WpfLearningWorkflow.DatasetPurpose.Train"),
                WpfLearningMode.Infer => Translate("WpfLearningWorkflow.DatasetPurpose.Infer"),
                WpfLearningMode.Review => Translate("WpfLearningWorkflow.DatasetPurpose.Review"),
                _ => Translate("WpfLearningWorkflow.DatasetPurpose.Default")
            };
        }

        public static string BuildDatasetPurposeToolSummaryText(WpfLearningMode? mode)
        {
            return mode switch
            {
                WpfLearningMode.ObjectDetection => Translate("WpfLearningWorkflow.ToolSummary.ObjectDetection"),
                WpfLearningMode.Segmentation => Translate("WpfLearningWorkflow.ToolSummary.Segmentation"),
                WpfLearningMode.AnomalyDetection => Translate("WpfLearningWorkflow.ToolSummary.AnomalyDetection"),
                _ => Translate("WpfLearningWorkflow.ToolSummary.Default")
            };
        }

        public static string BuildDatasetSetupActionText(WpfLearningMode? mode)
        {
            return mode switch
            {
                WpfLearningMode.ObjectDetection => Translate("WpfLearningWorkflow.DatasetSetupAction"),
                WpfLearningMode.Segmentation => Translate("WpfLearningWorkflow.DatasetSetupAction"),
                WpfLearningMode.AnomalyDetection => Translate("WpfLearningWorkflow.DatasetSetupAction"),
                _ => Translate("WpfLearningWorkflow.DatasetSetupStatus.Before")
            };
        }

        public static string BuildDatasetSetupFirstActionText(WpfLearningMode? mode)
        {
            return mode switch
            {
                WpfLearningMode.ObjectDetection => Translate("WpfLearningWorkflow.FirstAction.ObjectDetection"),
                WpfLearningMode.Segmentation => Translate("WpfLearningWorkflow.FirstAction.Segmentation"),
                WpfLearningMode.AnomalyDetection => Translate("WpfLearningWorkflow.FirstAction.AnomalyDetection"),
                _ => Translate("WpfLearningWorkflow.FirstAction.Default")
            };
        }

        public static string BuildCurrentWorkflowActionText(WpfLearningStep? step, WpfLearningMode? purposeMode)
        {
            return step switch
            {
                WpfLearningStep.Sample => "\uB2E4\uC74C: \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC5F4\uACE0 \uCCAB \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Label => purposeMode switch
                {
                    WpfLearningMode.Segmentation => "\uB2E4\uC74C: \uD3F4\uB9AC\uACE4/\uBE0C\uB7EC\uC2DC\uB85C \uB9C8\uC2A4\uD06C\uB97C \uB9CC\uB4E4\uACE0 \uC800\uC7A5\uD569\uB2C8\uB2E4.",
                    WpfLearningMode.AnomalyDetection => "다음: 이미지 전체를 정상(OK) 또는 이상(NG)으로 판정하고 다음 이미지로 이동합니다.",
                    _ => "\uB2E4\uC74C: \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uD074\uB798\uC2A4\uAC00 \uB9DE\uB294\uC9C0 \uD655\uC778\uD569\uB2C8\uB2E4."
                },
                WpfLearningStep.Infer => "\uB2E4\uC74C: \uCD94\uB860\uC744 \uC2E4\uD589\uD558\uACE0 AI \uD6C4\uBCF4\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Review => "\uB2E4\uC74C: AI \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uC2A4\uD0B5\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Save => "\uB2E4\uC74C: \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uACE0 \uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uC2E4\uD589\uD569\uB2C8\uB2E4.",
                _ => string.Empty
            };
        }

        public static string BuildModeDetailText(WpfLearningMode mode)
        {
            return mode switch
            {
                WpfLearningMode.ObjectDetection => "\uAC1D\uCCB4 \uD0D0\uC9C0: \uC774\uBBF8\uC9C0 \uC548\uC758 \uAC1D\uCCB4 \uC704\uCE58\uB97C \uBC15\uC2A4\uB85C \uCC3E\uACE0, YOLO \uB4F1 \uAC1D\uCCB4 \uD0D0\uC9C0 \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uC815\uB2F5 \uB77C\uBCA8\uB85C \uD655\uC815\uD569\uB2C8\uB2E4.",
                WpfLearningMode.Segmentation => "\uC138\uADF8\uBA58\uD14C\uC774\uC158: \uD53D\uC140 \uB2E8\uC704 \uB9C8\uC2A4\uD06C\uB97C \uB9CC\uB4E4\uACE0, \uBAA8\uB378 \uD559\uC2B5/\uAC80\uC0AC\uB294 \uC5F0\uACB0\uB41C \uC138\uADF8\uBA58\uD14C\uC774\uC158 \uC2E4\uD589\uAE30\uC5D0\uC11C \uC9C4\uD589\uD569\uB2C8\uB2E4.",
                WpfLearningMode.AnomalyDetection => "이상 탐지: 이미지 전체의 정상/이상을 판정하고, 연결된 이미지 분류 실행기에서 학습과 검사를 진행합니다. 결함 위치를 그리는 작업은 객체탐지 또는 세그멘테이션을 사용하세요.",
                WpfLearningMode.Train => "\uD559\uC2B5: \uB77C\uBCA8\uACFC \uD074\uB798\uC2A4\uAC00 \uC900\uBE44\uB41C \uB4A4 \uB370\uC774\uD130\uC14B\uACFC \uD30C\uB77C\uBBF8\uD130\uB97C \uD3C9\uAC00\uD569\uB2C8\uB2E4.",
                WpfLearningMode.Infer => "\uCD94\uB860: \uD604\uC7AC \uC774\uBBF8\uC9C0 \uB610\uB294 \uC120\uD0DD \uC774\uBBF8\uC9C0\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C \uAC80\uC0AC\uD569\uB2C8\uB2E4.",
                WpfLearningMode.Review => "\uAC80\uD1A0: AI \uD6C4\uBCF4\uB97C \uBCF4\uACE0 \uD655\uC815/\uC2A4\uD0B5\uD558\uBA70 \uC815\uB2F5 \uB77C\uBCA8\uB85C \uBC14\uAFC9\uB2C8\uB2E4.",
                _ => "\uB77C\uBCA8\uB9C1 \uD750\uB984\uC740 \uC815\uB2F5 \uC601\uC5ED\uC744 \uB9CC\uB4E4\uACE0 AI\uAC00 \uBC30\uC6B8 \uAE30\uC900\uC744 \uC900\uBE44\uD569\uB2C8\uB2E4."
            };
        }

        public static string BuildStepDetailText(WpfLearningStep? step, WpfLearningMode purposeMode)
        {
            return step switch
            {
                WpfLearningStep.Sample => "\uC0D8\uD50C \uC774\uBBF8\uC9C0\uB97C \uBD88\uB7EC\uC640 \uAE30\uC900 \uD654\uBA74\uC744 \uB9CC\uB4ED\uB2C8\uB2E4.",
                WpfLearningStep.Label when purposeMode == WpfLearningMode.AnomalyDetection => "이미지 전체를 정상(OK) 또는 이상(NG)으로 판정합니다. 박스나 마스크는 그리지 않습니다.",
                WpfLearningStep.Label => "\uC815\uB2F5 \uB77C\uBCA8\uC744 \uC9C1\uC811 \uB9CC\uB4E4\uACE0 \uD074\uB798\uC2A4\uC640 \uC704\uCE58\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Infer => "AI \uD6C4\uBCF4\uB97C \uB9CC\uB4E0 \uB4A4 \uB77C\uBCA8\uACFC \uBE44\uAD50\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Review => "\uD6C4\uBCF4\uB97C \uD558\uB098\uC529 \uBCF4\uBA70 \uD655\uC815, \uC804\uCCB4 \uD655\uC815, \uC2A4\uD0B5\uC744 \uC120\uD0DD\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Save when purposeMode == WpfLearningMode.AnomalyDetection => "현재 이미지의 OK/NG 판정을 이상탐지 검토 상태로 저장합니다.",
                WpfLearningStep.Save => "\uD604\uC7AC \uB77C\uBCA8\uC744 \uB370\uC774\uD130\uC14B \uC800\uC7A5 \uD3F4\uB354\uC758 \uD559\uC2B5 \uB77C\uBCA8 \uD30C\uC77C\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4.",
                _ => string.Empty
            };
        }

        public static string BuildToolDetailText(WpfAnnotationTool? tool)
        {
            return tool switch
            {
                WpfAnnotationTool.Rectangle => "\uBC15\uC2A4: \uAC1D\uCCB4 \uD0D0\uC9C0 \uD559\uC2B5\uC5D0\uC11C \uAC00\uC7A5 \uAE30\uBCF8\uC774 \uB418\uB294 \uC601\uC5ED\uC785\uB2C8\uB2E4.",
                WpfAnnotationTool.Ellipse => "\uC6D0/\uD0C0\uC6D0: \uC6D0\uD615 \uD639\uC740 \uD0C0\uC6D0 \uC601\uC5ED\uC744 \uBE60\uB974\uAC8C \uC124\uBA85\uD558\uB294 \uBCF4\uC870 \uB3C4\uAD6C\uC785\uB2C8\uB2E4.",
                WpfAnnotationTool.Polygon => "\uD3F4\uB9AC\uACE4: \uC138\uADF8\uBA58\uD14C\uC774\uC158 \uACBD\uACC4\uB97C \uAF2D\uC9D3\uC810\uC73C\uB85C \uB9CC\uB4ED\uB2C8\uB2E4.",
                WpfAnnotationTool.Brush => "\uBE0C\uB7EC\uC2DC: \uB9C8\uC2A4\uD06C\uB97C \uCE60\uD574 \uD53D\uC140 \uB2E8\uC704 \uC815\uB2F5\uC744 \uB9CC\uB4ED\uB2C8\uB2E4.",
                WpfAnnotationTool.Eraser => "\uC9C0\uC6B0\uAC1C: \uB9C8\uC2A4\uD06C\uB098 \uC601\uC5ED \uC77C\uBD80\uB97C \uC81C\uAC70\uD569\uB2C8\uB2E4.",
                WpfAnnotationTool.PanZoom => "\uC774\uB3D9: \uB77C\uBCA8\uC744 \uB9CC\uB4E4\uAE30 \uC804\uC5D0 \uD654\uBA74 \uC704\uCE58\uB97C \uBE60\uB974\uAC8C \uC870\uC815\uD569\uB2C8\uB2E4.",
                WpfAnnotationTool.Delete => "\uC0AD\uC81C: \uC120\uD0DD\uD55C \uB77C\uBCA8\uC744 \uC81C\uAC70\uD569\uB2C8\uB2E4.",
                WpfAnnotationTool.Undo => "\uB418\uB3CC\uB9AC\uAE30: \uC9C1\uC804 \uD3B8\uC9D1\uC744 \uB418\uB3CC\uB9AC\uB294 \uBC84\uD2BC\uC785\uB2C8\uB2E4.",
                WpfAnnotationTool.Redo => "\uB2E4\uC2DC \uC801\uC6A9: \uB418\uB3CC\uB9B0 \uD3B8\uC9D1\uC744 \uB2E4\uC2DC \uC801\uC6A9\uD558\uB294 \uBC84\uD2BC\uC785\uB2C8\uB2E4.",
                _ => "\uC120\uD0DD: \uB9CC\uB4E0 \uB77C\uBCA8\uC744 \uACE0\uB974\uACE0 \uAC80\uC0AC\uD569\uB2C8\uB2E4."
            };
        }

        private static string Translate(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                Translate(key),
                arguments ?? Array.Empty<object>());
        }
    }

    [Obsolete("Use LearningWorkflowGuidanceService.", false)]
    public static class WpfLearningWorkflowGuidanceService
    {
        public static string BuildObjectDetectionMvpNextActionText(string actionText)
            => LearningWorkflowGuidanceService.BuildObjectDetectionMvpNextActionText(actionText);

        public static string BuildCurrentYoloTrainingActionText(WpfYoloTrainingWorkflowStepItem step)
            => LearningWorkflowGuidanceService.BuildCurrentYoloTrainingActionText(step);

        public static string FormatHistoryActionSuffix(string actionName)
            => LearningWorkflowGuidanceService.FormatHistoryActionSuffix(actionName);

        public static string FormatLiveLabelingTaskToolText(string toolText)
            => LearningWorkflowGuidanceService.FormatLiveLabelingTaskToolText(toolText);

        public static string BuildDatasetPurposeSummaryText(WpfLearningMode mode)
            => LearningWorkflowGuidanceService.BuildDatasetPurposeSummaryText(mode);

        public static string BuildDatasetPurposeToolSummaryText(WpfLearningMode? mode)
            => LearningWorkflowGuidanceService.BuildDatasetPurposeToolSummaryText(mode);

        public static string BuildDatasetSetupActionText(WpfLearningMode? mode)
            => LearningWorkflowGuidanceService.BuildDatasetSetupActionText(mode);

        public static string BuildDatasetSetupFirstActionText(WpfLearningMode? mode)
            => LearningWorkflowGuidanceService.BuildDatasetSetupFirstActionText(mode);

        public static string BuildCurrentWorkflowActionText(WpfLearningStep? step, WpfLearningMode? purposeMode)
            => LearningWorkflowGuidanceService.BuildCurrentWorkflowActionText(step, purposeMode);

        public static string BuildModeDetailText(WpfLearningMode mode)
            => LearningWorkflowGuidanceService.BuildModeDetailText(mode);

        public static string BuildStepDetailText(WpfLearningStep? step, WpfLearningMode purposeMode)
            => LearningWorkflowGuidanceService.BuildStepDetailText(step, purposeMode);

        public static string BuildToolDetailText(WpfAnnotationTool? tool)
            => LearningWorkflowGuidanceService.BuildToolDetailText(tool);
    }
}
