using MvcVisionSystem.Yolo;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public static class AnnotationProductivityService
    {
        public const int PreferredDuplicateOffset = 12;

        public const string ShortcutSummaryText =
            "V 선택 · R 박스 · P 폴리곤 · B 브러시 · E 지우개 · H 이동 · 1~9 클래스 · 0 클래스 관리";

        public const string ShortcutHelpText =
            "도구  V 선택  R 박스  P 폴리곤  B 브러시  E 지우개  H 이동\n"
            + "클래스  1~9 빠른 선택  0 클래스 관리\n"
            + "반복/편집  N 마지막 도구+클래스  Ctrl+D 선택 복제  Ctrl+Z/Y 실행 취소/다시 실행\n"
            + "Smart Mask  세그멘테이션에서 자동 윤곽 켜기 → R 박스 → 후보 검토\n"
            + "보정  포함/제외점 1개씩 → 후보 다시 생성 → 이전/현재 후보 비교 → 확정 또는 스킵\n"
            + "저장  후보 생성·복원은 저장하지 않으며, 확정은 표시 중인 후보를 정답 파일에 저장합니다.\n"
            + "F1 도움말 닫기 · 입력 상자 편집 중에는 라벨링 단축키가 실행되지 않습니다.";

        public static AnnotationShortcut ResolveShortcut(Key key, ModifierKeys modifiers)
        {
            if (modifiers == ModifierKeys.Control && key == Key.D)
            {
                return new AnnotationShortcut(WpfAnnotationShortcutKind.DuplicateSelected);
            }

            if (modifiers != ModifierKeys.None)
            {
                return AnnotationShortcut.None;
            }

            if (TryResolveClassIndex(key, out int classIndex))
            {
                return new AnnotationShortcut(WpfAnnotationShortcutKind.SelectClass, classIndex: classIndex);
            }

            return key switch
            {
                Key.V => new AnnotationShortcut(WpfAnnotationShortcutKind.SelectTool, WpfAnnotationTool.Select),
                Key.R => new AnnotationShortcut(WpfAnnotationShortcutKind.SelectTool, WpfAnnotationTool.Rectangle),
                Key.P => new AnnotationShortcut(WpfAnnotationShortcutKind.SelectTool, WpfAnnotationTool.Polygon),
                Key.B => new AnnotationShortcut(WpfAnnotationShortcutKind.SelectTool, WpfAnnotationTool.Brush),
                Key.E => new AnnotationShortcut(WpfAnnotationShortcutKind.SelectTool, WpfAnnotationTool.Eraser),
                Key.H => new AnnotationShortcut(WpfAnnotationShortcutKind.SelectTool, WpfAnnotationTool.PanZoom),
                Key.D0 or Key.NumPad0 => new AnnotationShortcut(WpfAnnotationShortcutKind.OpenClassCatalog),
                Key.N => new AnnotationShortcut(WpfAnnotationShortcutKind.RepeatLast),
                Key.F1 => new AnnotationShortcut(WpfAnnotationShortcutKind.ToggleShortcutHelp),
                _ => AnnotationShortcut.None
            };
        }

        public static string GetToolShortcutText(WpfAnnotationTool tool)
        {
            return tool switch
            {
                WpfAnnotationTool.Select => "V",
                WpfAnnotationTool.Rectangle => "R",
                WpfAnnotationTool.Polygon => "P",
                WpfAnnotationTool.Brush => "B",
                WpfAnnotationTool.Eraser => "E",
                WpfAnnotationTool.PanZoom => "H",
                WpfAnnotationTool.Undo => "Ctrl+Z",
                WpfAnnotationTool.Redo => "Ctrl+Y",
                WpfAnnotationTool.Delete => "Delete",
                _ => string.Empty
            };
        }

        public static bool IsRepeatableDrawingTool(WpfAnnotationTool tool)
            => tool == WpfAnnotationTool.Rectangle
                || tool == WpfAnnotationTool.Polygon
                || tool == WpfAnnotationTool.Brush;

        public static Rectangle CreateOffsetRectangle(Rectangle source, Size imageSize)
        {
            Point offset = ResolveDuplicateOffset(source, imageSize);
            return new Rectangle(
                source.X + offset.X,
                source.Y + offset.Y,
                source.Width,
                source.Height);
        }

        public static LabelingSegmentationObject CreateOffsetSegment(
            LabelingSegmentationObject source,
            Size imageSize,
                MaskAnnotationService maskService)
        {
                LabelingSegmentationObject duplicate = AnnotationHistoryService.CloneSegment(source);
            if (duplicate == null)
            {
                return null;
            }

            duplicate.ObjectId = string.Empty;
            duplicate.ComponentIndex = -1;
            duplicate.LastStructuralOperation = string.Empty;
            duplicate.Selected = true;
            Point offset = ResolveDuplicateOffset(source.Bounds, imageSize);
            if (duplicate.IsRasterMask)
            {
                (maskService ?? new MaskAnnotationService()).TryMoveRasterMask(
                    duplicate,
                    offset.X,
                    offset.Y,
                    imageSize,
                    out _);
                return duplicate;
            }

            duplicate.Points = duplicate.Points
                .Select(point => new Point(point.X + offset.X, point.Y + offset.Y))
                .ToList();
            duplicate.CutoutPolygons = duplicate.CutoutPolygons
                .Select(cutout => cutout
                    .Select(point => new Point(point.X + offset.X, point.Y + offset.Y))
                    .ToList())
                .ToList();
            return duplicate;
        }

        private static bool TryResolveClassIndex(Key key, out int classIndex)
        {
            if (key >= Key.D1 && key <= Key.D9)
            {
                classIndex = (int)key - (int)Key.D1;
                return true;
            }

            if (key >= Key.NumPad1 && key <= Key.NumPad9)
            {
                classIndex = (int)key - (int)Key.NumPad1;
                return true;
            }

            classIndex = -1;
            return false;
        }

        private static Point ResolveDuplicateOffset(Rectangle bounds, Size imageSize)
        {
            if (bounds.IsEmpty || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return Point.Empty;
            }

            int deltaX = Math.Min(PreferredDuplicateOffset, Math.Max(0, imageSize.Width - bounds.Right));
            int deltaY = Math.Min(PreferredDuplicateOffset, Math.Max(0, imageSize.Height - bounds.Bottom));
            if (deltaX == 0)
            {
                deltaX = -Math.Min(PreferredDuplicateOffset, Math.Max(0, bounds.Left));
            }

            if (deltaY == 0)
            {
                deltaY = -Math.Min(PreferredDuplicateOffset, Math.Max(0, bounds.Top));
            }

            return new Point(deltaX, deltaY);
        }
    }

    [Obsolete("Use AnnotationProductivityService.", false)]
    public static class WpfAnnotationProductivityService
    {
        public const int PreferredDuplicateOffset = AnnotationProductivityService.PreferredDuplicateOffset;

        public const string ShortcutSummaryText = AnnotationProductivityService.ShortcutSummaryText;

        public const string ShortcutHelpText = AnnotationProductivityService.ShortcutHelpText;

        public static WpfAnnotationShortcut ResolveShortcut(Key key, ModifierKeys modifiers)
            => WpfAnnotationShortcut.From(AnnotationProductivityService.ResolveShortcut(key, modifiers));

        public static string GetToolShortcutText(WpfAnnotationTool tool)
            => AnnotationProductivityService.GetToolShortcutText(tool);

        public static bool IsRepeatableDrawingTool(WpfAnnotationTool tool)
            => AnnotationProductivityService.IsRepeatableDrawingTool(tool);

        public static Rectangle CreateOffsetRectangle(Rectangle source, Size imageSize)
            => AnnotationProductivityService.CreateOffsetRectangle(source, imageSize);

        public static LabelingSegmentationObject CreateOffsetSegment(
            LabelingSegmentationObject source,
            Size imageSize,
                MaskAnnotationService maskService)
            => AnnotationProductivityService.CreateOffsetSegment(source, imageSize, maskService);
    }
}
