using System.Collections.ObjectModel;
using TVSecurityToolkit.App.Services;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class NavItem
{
    public NavItem(string title, ViewModelBase vm) { Title = title; ViewModel = vm; }
    public string Title { get; }
    public ViewModelBase ViewModel { get; }
}

public sealed class MainViewModel : ViewModelBase
{
    private NavItem? _selected;
    private ViewModelBase? _current;
    private string _status;

    public MainViewModel(ApplicationService app)
    {
        var selection = new TestSelectionViewModel(app);
        Items = new ObservableCollection<NavItem>
        {
            new("Dashboard", new DashboardViewModel(app)),
            new("Device", new DeviceViewModel(app)),
            new("Test selection", selection),
            new("Test execution", new TestExecutionViewModel(app, selection)),
            new("Results", new ResultsViewModel(app)),
            new("Findings", new FindingsViewModel(app)),
            new("Reports", new ReportsViewModel(app)),
            new("Diagnostics", new DiagnosticsViewModel(app)),
            new("Settings", new SettingsViewModel(app)),
            new("About", new AboutViewModel())
        };
        _status = app.Notifications.Last;
        app.Notifications.Notified += m => Status = m;
        Selected = Items[0];
    }

    public ObservableCollection<NavItem> Items { get; }

    public NavItem? Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) CurrentViewModel = value?.ViewModel; }
    }

    public ViewModelBase? CurrentViewModel { get => _current; private set => Set(ref _current, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
}
