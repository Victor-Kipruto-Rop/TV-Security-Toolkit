using TVSecurityToolkit.App.ViewModels;

namespace TVSecurityToolkit.App.Services;

public sealed class NavigationService
{
    public ViewModelBase? Current { get; private set; }
    public event Action<ViewModelBase>? Navigated;

    public void NavigateTo(ViewModelBase vm) { Current = vm; Navigated?.Invoke(vm); }
}
