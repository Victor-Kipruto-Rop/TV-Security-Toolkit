using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Device.Identity;

/// <summary>Confirms the device answers an authenticated API call (credential handling is device-specific).</summary>
public sealed class DeviceAuthenticator
{
    private readonly IDeviceAdapter _device;
    public DeviceAuthenticator(IDeviceAdapter device) => _device = device;

    public async Task<bool> AuthenticateAsync(CancellationToken ct)
    {
        var r = await _device.CallAsync("api_call", new JsonObject { ["credential"] = "valid", ["endpoint"] = "/diag/read", ["role"] = "user" }, ct);
        return (int?)r["status"] == 200;
    }
}
