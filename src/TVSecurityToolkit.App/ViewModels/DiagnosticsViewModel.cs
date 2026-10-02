using System.Text.Json;
using TVSecurityToolkit.App.Commands;
using TVSecurityToolkit.App.Services;
using TVSecurityToolkit.Device.Diagnostics;
using TVSecurityToolkit.Security.Network;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class DiagnosticsViewModel : ViewModelBase
{
    private string _output = "";
    private string _tlsHost = "";
    private string _tlsPort = "443";

    public DiagnosticsViewModel(ApplicationService app)
    {
        HealthCommand = new AsyncRelayCommand(async _ => Output = (await new HealthCheckClient(app.Device!).RunAsync(CancellationToken.None)).ToJsonString(new() { WriteIndented = true }),
            _ => app.Device is not null, e => Output = "Error: " + e.Message);
        ProbeCommand = new AsyncRelayCommand(async _ => Output = (await new DiagnosticClient(app.Device!).RunAsync(CancellationToken.None)).ToJsonString(new() { WriteIndented = true }),
            _ => app.Device is not null, e => Output = "Error: " + e.Message);
        TlsCommand = new AsyncRelayCommand(async _ =>
        {
            if (string.IsNullOrWhiteSpace(TlsHost)) throw new InvalidOperationException("Enter an authorized TLS host.");
            if (!int.TryParse(TlsPort, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Enter a valid TCP port (1-65535).");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var issues = await TransportSecurityChecker.CheckAsync(TlsHost, port, app.SecurityPolicy.MinTlsVersion, timeout.Token);
            Output = JsonSerializer.Serialize(new
            {
                host = TlsHost,
                port,
                minimumTlsVersion = app.SecurityPolicy.MinTlsVersion,
                passed = issues.Count == 0,
                issues
            }, new JsonSerializerOptions { WriteIndented = true });
        }, null, e => Output = "Error: " + e.Message);
    }

    public AsyncRelayCommand HealthCommand { get; }
    public AsyncRelayCommand ProbeCommand { get; }
    public AsyncRelayCommand TlsCommand { get; }
    public string TlsHost { get => _tlsHost; set => Set(ref _tlsHost, value); }
    public string TlsPort { get => _tlsPort; set => Set(ref _tlsPort, value); }
    public string Output { get => _output; private set => Set(ref _output, value); }
}
