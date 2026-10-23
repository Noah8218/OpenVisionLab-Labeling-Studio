using OpenVisionLab.ImageCanvas.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ImageCanvas.Views
{
	public sealed class AddRoiArrayView : Window
	{
		private AddRoiArrayViewModel subscribedViewModel;

		public AddRoiArrayView()
		{
			Width = 420;
			Height = 300;
			MinWidth = 420;
			MinHeight = 300;
			WindowStyle = WindowStyle.ToolWindow;
			SetResourceReference(BackgroundProperty, "RecipePageBrush");
			SetResourceReference(ForegroundProperty, "RecipeTextBrush");
			Content = CreateContent();
			Loaded += OnLoaded;
		}

		private UIElement CreateContent()
		{
			Grid grid = new Grid { Margin = new Thickness(12) };
			for (int i = 0; i < 5; i++)
			{
				grid.RowDefinitions.Add(new RowDefinition { Height = i == 1 || i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
			}
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.ColumnDefinitions.Add(new ColumnDefinition());

			AddLabel(grid, "Row", 0, 0);
			AddTextBox(grid, nameof(AddRoiArrayViewModel.Rows), 1, 0);
			AddLabel(grid, "Column", 0, 1);
			AddTextBox(grid, nameof(AddRoiArrayViewModel.Columns), 1, 1);
			AddLabel(grid, "Row Gap(mm)", 2, 0);
			AddTextBox(grid, nameof(AddRoiArrayViewModel.RowSpacing), 3, 0);
			AddLabel(grid, "Column Gap(mm)", 2, 1);
			AddTextBox(grid, nameof(AddRoiArrayViewModel.ColumnSpacing), 3, 1);

			Button apply = new Button { Name = "ArrayApplyButton", Content = "Apply", Height = 40, Margin = new Thickness(4) };
			apply.SetResourceReference(StyleProperty, "RecipePrimaryButtonStyle");
			apply.SetBinding(Button.CommandProperty, nameof(AddRoiArrayViewModel.ApplyCommand));
			Grid.SetRow(apply, 4);
			Grid.SetColumn(apply, 0);
			grid.Children.Add(apply);

			Button cancel = new Button { Name = "ArrayCancelButton", Content = "Cancel", IsCancel = true, Height = 40, Margin = new Thickness(4) };
			cancel.SetResourceReference(StyleProperty, "RecipeSecondaryButtonStyle");
			cancel.SetBinding(Button.CommandProperty, nameof(AddRoiArrayViewModel.CancelCommand));
			Grid.SetRow(cancel, 4);
			Grid.SetColumn(cancel, 1);
			grid.Children.Add(cancel);

			return grid;
		}

		private static void AddLabel(Grid grid, string text, int row, int column)
		{
			TextBlock label = new TextBlock { Text = text, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Bottom };
			Grid.SetRow(label, row);
			Grid.SetColumn(label, column);
			grid.Children.Add(label);
		}

		private static void AddTextBox(Grid grid, string bindingPath, int row, int column)
		{
			TextBox textBox = new TextBox { Name = "Array" + bindingPath + "Input", MinHeight = 36, Margin = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center };
			textBox.SetResourceReference(StyleProperty, "RecipeInputStyle");
			textBox.SetBinding(TextBox.TextProperty, bindingPath);
			Grid.SetRow(textBox, row);
			Grid.SetColumn(textBox, column);
			grid.Children.Add(textBox);
		}

		protected override void OnClosed(System.EventArgs e)
		{
			if (subscribedViewModel != null) subscribedViewModel.RequestClose -= OnRequestClose;
			subscribedViewModel = null;
			base.OnClosed(e);
		}

		private void OnRequestClose(bool result) => DialogResult = result;

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			if (subscribedViewModel == null && DataContext is AddRoiArrayViewModel viewModel)
			{
				subscribedViewModel = viewModel;
				subscribedViewModel.RequestClose += OnRequestClose;
			}
		}
	}
}
