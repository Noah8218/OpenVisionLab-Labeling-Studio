using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public class AnnotationHistoryStack
    {
        private const int DefaultHistoryLimit = 50;
        private readonly int historyLimit;
        private readonly List<WpfAnnotationHistorySnapshot> undoSnapshots = new List<WpfAnnotationHistorySnapshot>();
        private readonly List<WpfAnnotationHistorySnapshot> redoSnapshots = new List<WpfAnnotationHistorySnapshot>();

        public AnnotationHistoryStack(int historyLimit = DefaultHistoryLimit)
        {
            if (historyLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(historyLimit));
            }

            this.historyLimit = historyLimit;
        }

        public int UndoCount => undoSnapshots.Count;

        public int RedoCount => redoSnapshots.Count;

        public WpfAnnotationHistorySnapshot PeekUndo()
            => undoSnapshots.Count == 0 ? null : undoSnapshots[undoSnapshots.Count - 1];

        public WpfAnnotationHistorySnapshot PeekRedo()
            => redoSnapshots.Count == 0 ? null : redoSnapshots[redoSnapshots.Count - 1];

        public void Push(WpfAnnotationHistorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            undoSnapshots.Add(snapshot);
            TrimToLimit(undoSnapshots);
            redoSnapshots.Clear();
        }

        public bool TryMoveUndoToRedo(
            WpfAnnotationHistorySnapshot oppositeSnapshot,
            out WpfAnnotationHistorySnapshot target)
        {
            target = PeekUndo();
            if (target == null || oppositeSnapshot == null)
            {
                target = null;
                return false;
            }

            undoSnapshots.RemoveAt(undoSnapshots.Count - 1);
            redoSnapshots.Add(oppositeSnapshot);
            return true;
        }

        public bool TryMoveRedoToUndo(
            WpfAnnotationHistorySnapshot oppositeSnapshot,
            out WpfAnnotationHistorySnapshot target)
        {
            target = PeekRedo();
            if (target == null || oppositeSnapshot == null)
            {
                target = null;
                return false;
            }

            redoSnapshots.RemoveAt(redoSnapshots.Count - 1);
            undoSnapshots.Add(oppositeSnapshot);
            TrimToLimit(undoSnapshots);
            return true;
        }

        public void Clear()
        {
            undoSnapshots.Clear();
            redoSnapshots.Clear();
        }

        private void TrimToLimit(List<WpfAnnotationHistorySnapshot> snapshots)
        {
            while (snapshots.Count > historyLimit)
            {
                snapshots.RemoveAt(0);
            }
        }
    }

    [Obsolete("Use AnnotationHistoryStack.", false)]
    public sealed class WpfAnnotationHistoryStack : AnnotationHistoryStack
    {
        public WpfAnnotationHistoryStack(int historyLimit = 50)
            : base(historyLimit)
        {
        }
    }
}
