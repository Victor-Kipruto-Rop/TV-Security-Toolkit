using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Device.Diagnostics;

/// <summary>Read-only probes of exposed debug/diagnostic surfaces.</summary>
public sealed class DiagnosticClient
{
    private readonly IDeviceAdapter _device;
    public DiagnosticClient(IDeviceAdapter device) => _device = device;

    public async Task<JsonObject> RunAsync(CancellationToken ct) => new()
    {
        ["debug"] = await _device.CallAsync("debug_probe", new JsonObject(), ct),
        ["diagnostic_access"] = await _device.CallAsync("diagnostic_access_probe", new JsonObject(), ct)
    };
}
