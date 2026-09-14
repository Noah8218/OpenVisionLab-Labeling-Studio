using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using System;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns four-point box input policy while the Shell supplies canvas and presentation callbacks.
    /// </summary>
    internal sealed class FourPointBoxInputAdapter
    {
        private readonly FourPointBoxService service;
        private readonly FourPointBoxInputAdapterContext context;

        internal FourPointBoxInputAdapter(
            FourPointBoxService service,
            FourPointBoxInputAdapterContext context)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal bool TryHandleInput(CanvasImagePointEventArgs e)
        {
            if (context.IsInputActive?.Invoke() != true || e == null)
            {
                return false;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                CancelDraft(updateStatus: true);
                return true;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            Size imageSize = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            WpfFourPointBoxInputResult result = service.TryAddPoint(
                e.ImagePoint,
                imageSize,
                out Rectangle completedBounds,
                out string message);
            context.SetProgress?.Invoke(service.PointCount);
            context.RefreshOverlays?.Invoke();
            context.SetYoloCommandStatus?.Invoke(message, false);
            if (result != WpfFourPointBoxInputResult.Completed)
            {
                return true;
            }

            string className = FirstNonEmpty(context.SelectedClassNameProvider?.Invoke(), "Defect");
            if (context.AddCompletedRectangle?.Invoke(completedBounds, className) != true)
            {
                context.SetYoloCommandStatus?.Invoke("박스 오버레이를 추가할 수 없습니다.", false);
                return true;
            }

            context.SetProgress?.Invoke(0);
            context.RefreshOverlays?.Invoke();
            return true;
        }

        internal bool RemoveLastPoint()
        {
            if (context.IsInputActive?.Invoke() != true || !service.RemoveLastPoint())
            {
                return false;
            }

            context.SetProgress?.Invoke(service.PointCount);
            context.RefreshOverlays?.Invoke();
            context.SetYoloCommandStatus?.Invoke(
                service.PointCount == 0
                    ? "4점 극점 입력을 다시 시작하세요."
                    : service.BuildProgressText(),
                false);
            return true;
        }

        internal bool CancelDraft(bool updateStatus)
        {
            bool canceled = service.Reset();
            context.SetProgress?.Invoke(0);
            if (!canceled)
            {
                return false;
            }

            context.RefreshOverlays?.Invoke();
            if (updateStatus)
            {
                context.SetYoloCommandStatus?.Invoke("4점 극점 초안을 취소했습니다.", false);
            }

            return true;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }

    internal sealed class FourPointBoxInputAdapterContext
    {
        internal Func<bool> IsInputActive { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<string> SelectedClassNameProvider { get; init; }
        internal Func<Rectangle, string, bool> AddCompletedRectangle { get; init; }
        internal Action<int> SetProgress { get; init; }
        internal Action RefreshOverlays { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
    }
}
