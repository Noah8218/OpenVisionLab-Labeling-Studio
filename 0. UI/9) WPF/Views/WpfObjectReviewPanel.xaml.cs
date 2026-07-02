using System.Windows.Controls;
using Border = System.Windows.Controls.Border;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfObjectReviewPanel : UserControl
    {
        public WpfObjectReviewPanel()
        {
            InitializeComponent();
        }

        public WpfObjectReviewPanelViewModel ViewModel => DataContext as WpfObjectReviewPanelViewModel;

        public TextBlock SummaryTextBlock => ObjectReviewSummaryText;
        public Border LabelSaveBadge => ObjectReviewLabelSaveBadge;
        public TextBlock LabelSaveBadgeTextBlock => ObjectReviewLabelSaveBadgeText;
        public TextBlock LabelSaveDetailTextBlock => ObjectReviewLabelSaveDetailText;
        public WpfUiButton DeleteButton => DeleteObjectButton;
        public ComboBox ClassBox => ObjectClassBox;
        public WpfUiButton ApplyClassButton => ApplyObjectClassButton;
        public ListBox ObjectList => ObjectListBox;
    }
}
