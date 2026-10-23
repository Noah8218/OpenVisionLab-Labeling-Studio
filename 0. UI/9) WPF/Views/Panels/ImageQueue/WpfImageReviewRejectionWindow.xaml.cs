using System;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfImageReviewRejectionWindow : Window
    {
        private readonly ImageReviewRejectionViewModel viewModel;

        public WpfImageReviewRejectionWindow(ImageReviewRejectionViewModel viewModel, Window owner)
        {
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            ArgumentNullException.ThrowIfNull(owner);
            Resources = owner.Resources;
            InitializeComponent();
            Owner = owner;
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            RejectButton.Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.CloseRequested -= OnCloseRequested;
            base.OnClosed(e);
        }

        private void OnCloseRequested(bool accepted) => DialogResult = accepted;
    }
}
