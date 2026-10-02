using TVSecurityToolkit.App.Services;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ApplicationService _app;
    public SettingsViewModel(ApplicationService app) { _app = app; }

    public IReadOnlyList<string> Environments => _app.Environments;

    public string SelectedEnvironment
    {
        get => _app.EnvironmentName;
        set { _app.EnvironmentName = value; Raise(); _app.Notifications.Notify("Environment: " + value); }
    }

    public string OutputDir { get => _app.OutputDir; set { _app.OutputDir = value; Raise(); } }
    public string Note => "production-readonly blocks every state-changing test before it reaches the device.";
}
