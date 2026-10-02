using TVSecurityToolkit.App.Services;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly ApplicationService _app;
    public DashboardViewModel(ApplicationService app)
    {
        _app = app;
        app.ReportChanged += Refresh; app.DeviceChanged += Refresh;
    }

    private void Refresh()
    {
        Raise(nameof(DeviceText)); Raise(nameof(EnvironmentText)); Raise(nameof(Passed)); Raise(nameof(Failed));
        Raise(nameof(Errors)); Raise(nameof(Skipped)); Raise(nameof(Cancelled)); Raise(nameof(Total));
        Raise(nameof(Findings)); Raise(nameof(CriticalFindings)); Raise(nameof(HighFindings));
    }

    public string DeviceText => _app.DeviceInfo is null ? "No device connected"
        : $"{_app.DeviceInfo.Identity?.Model} ({_app.DeviceInfo.Identity?.DeviceId}) - firmware {_app.DeviceInfo.Firmware?.Version}";
    public string EnvironmentText => _app.EnvironmentName;
    public int Total => _app.LastReport?.Total ?? 0;
    public int Passed => _app.LastReport?.Passed ?? 0;
    public int Failed => _app.LastReport?.Failed ?? 0;
    public int Errors => _app.LastReport?.Errors ?? 0;
    public int Skipped => _app.LastReport?.Skipped ?? 0;
    public int Cancelled => _app.LastReport?.Cancelled ?? 0;
    public int Findings => _app.LastReport?.Findings.Count ?? 0;
    public int CriticalFindings => _app.LastReport?.Findings.Count(f => f.Severity == TVSecurityToolkit.Core.Enums.Severity.Critical) ?? 0;
    public int HighFindings => _app.LastReport?.Findings.Count(f => f.Severity == TVSecurityToolkit.Core.Enums.Severity.High) ?? 0;
}
