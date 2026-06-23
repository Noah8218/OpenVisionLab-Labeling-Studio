using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenVisionLab.ImageCanvas.Infrastructure
{
	public interface IObservableObject : OpenVisionLab.Mvvm.IObservableObject
	{
	}

	// Keep the historical ImageCanvas namespace as a wrapper while new MVVM code uses OpenVisionLab.Mvvm directly.
	public abstract class ObservableObject : OpenVisionLab.Mvvm.ObservableObject, IObservableObject
	{
	}

	public interface IDisposableExtension : IDisposable
	{
		bool IsDisposed { get; }
	}
}

namespace OpenVisionLab.ImageCanvas.Commands
{
	public sealed class RelayCommand : OpenVisionLab.Mvvm.RelayCommand
	{
		public RelayCommand(Action execute, Func<bool> canExecute = null)
			: base(execute, canExecute)
		{
		}
	}

	public sealed class RelayCommand<T> : OpenVisionLab.Mvvm.RelayCommand<T>
	{
		public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
			: base(execute, canExecute)
		{
		}
	}
}

namespace OpenVisionLab.ImageCanvas.Events
{

}

namespace OpenVisionLab.ImageCanvas.SharedViewModels
{
	public sealed class MenuItemViewModel : OpenVisionLab.ImageCanvas.Infrastructure.ObservableObject
	{
		private Geometry _iconData;
		private bool _isVisible = true;
		private int _order;
		private string _background;
		private string _foreground;

		public ICommand Command { get; set; }
		public string Header { get; set; }
		public ObservableCollection<MenuItemViewModel> Children { get; } = new ObservableCollection<MenuItemViewModel>();

		public Geometry IconData
		{
			get => _iconData;
			set => SetProperty(ref _iconData, value);
		}

		public bool IsVisible
		{
			get => _isVisible;
			set => SetProperty(ref _isVisible, value);
		}

		public int Order
		{
			get => _order;
			set => SetProperty(ref _order, value);
		}

		public string Background
		{
			get => _background;
			set => SetProperty(ref _background, value);
		}

		public string Foreground
		{
			get => _foreground;
			set => SetProperty(ref _foreground, value);
		}
	}

	public static class MaterialIconData
	{
		public static readonly Geometry Image = Geometry.Parse("M8.5,13.5L11,16.5L14.5,12L19,18H5M21,19V5C21,3.89 20.1,3 19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19Z");
		public static readonly Geometry ContentSave = Geometry.Parse("M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z");
		public static readonly Geometry CheckCircle = Geometry.Parse("M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M10,17L18,9L16.59,7.59L10,14.17L6.41,10.59L5,12L10,17Z");
		public static readonly Geometry Delete = Geometry.Parse("M9,3V4H4V6H5V19A2,2 0 0,0 7,21H17A2,2 0 0,0 19,19V6H20V4H15V3H9M7,6H17V19H7V6Z");
	}

	public static class MenuItemUtil
	{
		public static string GetDescription(Enum value)
		{
			MemberInfo member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
			return member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
		}
	}
}
