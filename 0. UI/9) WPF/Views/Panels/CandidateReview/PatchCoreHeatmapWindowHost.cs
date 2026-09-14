using System;
using System.Windows;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Candidate Review PatchCore evidence window lifetime.
    /// Candidate loading and review state remain in the existing service and ViewModel.
    /// </summary>
    internal sealed class PatchCoreHeatmapWindowHost : IDisposable
    {
        private readonly Window owner;
        private readonly Action closeViewModel;
        private WpfPatchCoreHeatmapWindow window;
        private YoloWorkerSmokeCandidate candidate;
        private bool isDisposed;

        internal PatchCoreHeatmapWindowHost(Window owner, Action closeViewModel)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.closeViewModel = closeViewModel ?? throw new ArgumentNullException(nameof(closeViewModel));
        }

        internal bool IsOpen => window != null;

        internal bool IsOpenFor(YoloWorkerSmokeCandidate selectedCandidate)
            => window != null && ReferenceEquals(candidate, selectedCandidate);

        internal void Show(
            WpfCandidateReviewPanelViewModel viewModel,
            YoloWorkerSmokeCandidate selectedCandidate,
            bool topmost)
        {
            if (isDisposed || viewModel == null || selectedCandidate == null)
            {
                return;
            }

            if (window != null)
            {
                window.Activate();
                return;
            }

            window = new WpfPatchCoreHeatmapWindow(viewModel)
            {
                Owner = owner,
                Topmost = topmost
            };
            candidate = selectedCandidate;
            window.ApplyThemeFrom(owner);
            window.Closed += Window_Closed;
            window.Show();
            window.Activate();
        }

        internal void RefreshTheme()
        {
            if (!isDisposed)
            {
                window?.ApplyThemeFrom(owner);
            }
        }

        internal void Close()
        {
            if (window == null)
            {
                candidate = null;
                closeViewModel();
                return;
            }

            WpfPatchCoreHeatmapWindow closingWindow = window;
            DetachWindow();
            closingWindow.Close();
            closeViewModel();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (sender is WpfPatchCoreHeatmapWindow closedWindow
                && ReferenceEquals(window, closedWindow))
            {
                DetachWindow();
            }

            closeViewModel();
        }

        private void DetachWindow()
        {
            if (window == null)
            {
                candidate = null;
                return;
            }

            window.Closed -= Window_Closed;
            window = null;
            candidate = null;
        }
    }
}
