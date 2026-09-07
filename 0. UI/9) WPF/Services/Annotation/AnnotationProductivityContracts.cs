using System;

namespace MvcVisionSystem
{
    public enum WpfAnnotationShortcutKind
    {
        None,
        SelectTool,
        SelectClass,
        OpenClassCatalog,
        RepeatLast,
        DuplicateSelected,
        ToggleShortcutHelp
    }

    public class AnnotationShortcut
    {
        public static AnnotationShortcut None { get; } = new AnnotationShortcut(
            WpfAnnotationShortcutKind.None);

        public AnnotationShortcut(
            WpfAnnotationShortcutKind kind,
            WpfAnnotationTool tool = WpfAnnotationTool.Select,
            int classIndex = -1)
        {
            Kind = kind;
            Tool = tool;
            ClassIndex = classIndex;
        }

        public WpfAnnotationShortcutKind Kind { get; }

        public WpfAnnotationTool Tool { get; }

        public int ClassIndex { get; }
    }

    [Obsolete("Use AnnotationShortcut.", false)]
    public sealed class WpfAnnotationShortcut : AnnotationShortcut
    {
        public static new WpfAnnotationShortcut None { get; } = new WpfAnnotationShortcut(
            WpfAnnotationShortcutKind.None);

        public WpfAnnotationShortcut(
            WpfAnnotationShortcutKind kind,
            WpfAnnotationTool tool = WpfAnnotationTool.Select,
            int classIndex = -1)
            : base(kind, tool, classIndex)
        {
        }

        public static WpfAnnotationShortcut From(AnnotationShortcut shortcut)
        {
            if (shortcut is WpfAnnotationShortcut compatibilityShortcut)
            {
                return compatibilityShortcut;
            }

            return new WpfAnnotationShortcut(shortcut?.Kind ?? WpfAnnotationShortcutKind.None, shortcut?.Tool ?? WpfAnnotationTool.Select, shortcut?.ClassIndex ?? -1);
        }
    }
}
