using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Device.Diagnostics;

/// <summary>Read-only health snapshot: firmware info, secure boot, boot chain, entitlement state.</summary>
public sealed class HealthCheckClient
{
    private readonly IDeviceAdapter _device;
    public HealthCheckClient(IDeviceAdapter device) => _device = device;

    public async Task<JsonObject> RunAsync(CancellationToken ct)
    {
        var e = new JsonObject();
        return new JsonObject
        {
            ["info"] = await _device.CallAsync("info", e, ct),
            ["secure_boot"] = await _device.CallAsync("secure_boot_state", new JsonObject(), ct),
            ["boot_chain"] = await _device.CallAsync("verify_boot_chain", new JsonObject(), ct),
            ["state"] = await _device.CallAsync("state", new JsonObject(), ct)
        };
    }
}
