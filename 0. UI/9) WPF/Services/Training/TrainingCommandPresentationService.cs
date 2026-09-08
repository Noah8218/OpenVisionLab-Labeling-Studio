using System;

namespace MvcVisionSystem
{
    public class TrainingRecoveryStatus
    {
        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;
    }

    public static class TrainingCommandPresentationService
    {
        public static string BuildAlreadyRunningStatus()
        {
            return "이미 학습이 진행 중입니다. 다시 시작하려면 먼저 중지하세요.";
        }

        public static string BuildPreparingDatasetStatus()
        {
            return "학습 데이터셋 준비 중...";
        }

        public static TrainingRecoveryStatus BuildWorkerConnectionFailureRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = "추론 실행기 연결 실패",
                Detail = NormalizeDetail(detail),
                Action = "다음: 모델 실행기의 테스트 또는 재시작으로 연결을 확인한 뒤 학습을 다시 시작하세요."
            };
        }

        public static string BuildStartCommandResultStatus(bool started)
        {
            return started
                ? "학습 시작 요청 전달됨. 아직 성공 완료가 아니며, 워커의 시작 응답과 첫 에폭 로그를 확인하는 중입니다. 중지 버튼으로 취소할 수 있습니다."
                : "학습 시작 명령을 보내지 못했습니다. 데이터셋 준비 상태와 추론 연결을 확인하세요.";
        }

        public static string BuildStartCommandResultStatus(bool started, string failureDetail)
        {
            if (started || string.IsNullOrWhiteSpace(failureDetail))
            {
                return BuildStartCommandResultStatus(started);
            }

            return $"\uD559\uC2B5 \uC2DC\uC791 \uC2E4\uD328: {TrainingReadinessPresentationService.BuildFriendlyIssueSummary(failureDetail)}";
        }

        public static TrainingRecoveryStatus BuildStartFailureRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = "학습 시작 실패",
                Detail = NormalizeDetail(detail),
                Action = "다음: 데이터셋 점검 결과와 모델 실행기 연결 상태를 확인한 뒤 학습 시작을 다시 누르세요."
            };
        }

        public static string BuildTrainingAcceptedProgressText()
        {
            return "학습 시작 요청 전달됨 / 워커 시작 확인 대기";
        }

        public static string BuildStartExceptionStatus(string message)
        {
            return $"학습 시작 실패: {NormalizeDetail(message)}";
        }

        public static TrainingRecoveryStatus BuildStartExceptionRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = "학습 시작 오류",
                Detail = NormalizeDetail(detail),
                Action = "다음: 상세 로그에서 마지막 오류를 확인하고 설정을 수정한 뒤 학습을 다시 시작하세요."
            };
        }

        public static string BuildStoppingStatus()
        {
            return "학습 중지 요청 중...";
        }

        public static string BuildStopCommandResultStatus(bool stopped)
        {
            return stopped
                ? "학습 중지 확인 완료."
                : "학습 중지 명령을 보내지 못했습니다. 추론 연결을 확인하세요.";
        }

        public static TrainingRecoveryStatus BuildStopFailureRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = "학습 중지 실패",
                Detail = NormalizeDetail(detail),
                Action = "다음: 모델 실행기 재시작 또는 중지 후 상태를 다시 확인하세요."
            };
        }

        public static string BuildStopExceptionStatus(string message)
        {
            return $"학습 중지 실패: {NormalizeDetail(message)}";
        }

        public static TrainingRecoveryStatus BuildStopExceptionRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = "학습 중지 오류",
                Detail = NormalizeDetail(detail),
                Action = "다음: 상세 로그에서 오류를 확인하고 모델 실행기를 재시작하세요."
            };
        }

        private static string NormalizeDetail(string detail)
        {
            return string.IsNullOrWhiteSpace(detail)
                ? "상세 원인을 확인할 수 없습니다."
                : detail.Trim();
        }
    }

    [Obsolete("Use TrainingRecoveryStatus.", false)]
    public sealed class WpfTrainingRecoveryStatus : TrainingRecoveryStatus
    {
    }

    [Obsolete("Use TrainingCommandPresentationService.", false)]
    public static class WpfTrainingCommandPresentationService
    {
        public static string BuildAlreadyRunningStatus()
            => TrainingCommandPresentationService.BuildAlreadyRunningStatus();

        public static string BuildPreparingDatasetStatus()
            => TrainingCommandPresentationService.BuildPreparingDatasetStatus();

        public static WpfTrainingRecoveryStatus BuildWorkerConnectionFailureRecovery(string detail)
            => ToLegacy(TrainingCommandPresentationService.BuildWorkerConnectionFailureRecovery(detail));

        public static string BuildStartCommandResultStatus(bool started)
            => TrainingCommandPresentationService.BuildStartCommandResultStatus(started);

        public static string BuildStartCommandResultStatus(bool started, string failureDetail)
            => TrainingCommandPresentationService.BuildStartCommandResultStatus(started, failureDetail);

        public static WpfTrainingRecoveryStatus BuildStartFailureRecovery(string detail)
            => ToLegacy(TrainingCommandPresentationService.BuildStartFailureRecovery(detail));

        public static string BuildTrainingAcceptedProgressText()
            => TrainingCommandPresentationService.BuildTrainingAcceptedProgressText();

        public static string BuildStartExceptionStatus(string message)
            => TrainingCommandPresentationService.BuildStartExceptionStatus(message);

        public static WpfTrainingRecoveryStatus BuildStartExceptionRecovery(string detail)
            => ToLegacy(TrainingCommandPresentationService.BuildStartExceptionRecovery(detail));

        public static string BuildStoppingStatus()
            => TrainingCommandPresentationService.BuildStoppingStatus();

        public static string BuildStopCommandResultStatus(bool stopped)
            => TrainingCommandPresentationService.BuildStopCommandResultStatus(stopped);

        public static WpfTrainingRecoveryStatus BuildStopFailureRecovery(string detail)
            => ToLegacy(TrainingCommandPresentationService.BuildStopFailureRecovery(detail));

        public static string BuildStopExceptionStatus(string message)
            => TrainingCommandPresentationService.BuildStopExceptionStatus(message);

        public static WpfTrainingRecoveryStatus BuildStopExceptionRecovery(string detail)
            => ToLegacy(TrainingCommandPresentationService.BuildStopExceptionRecovery(detail));

        private static WpfTrainingRecoveryStatus ToLegacy(TrainingRecoveryStatus status)
        {
            if (status == null)
            {
                return null;
            }

            return new WpfTrainingRecoveryStatus
            {
                Title = status.Title,
                Detail = status.Detail,
                Action = status.Action
            };
        }
    }
}
