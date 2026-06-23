using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    // Compatibility shim: existing WPF view-models keep one base while shared MVVM logic lives in OpenVisionLab.Mvvm.
    public abstract class WpfObservableViewModel : ObservableObject
    {
    }
}
