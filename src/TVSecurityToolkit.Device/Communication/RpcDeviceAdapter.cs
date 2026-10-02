using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Device.Communication;

/// <summary>Adapter base: identity, clock and provisioning are expressed as protocol commands.</summary>
public abstract class RpcDeviceAdapter : IDeviceAdapter
{
    private DeviceIdentity? _identity;
    public abstract string Name { get; }
    public abstract Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct);

    public virtual async Task ProvisionAsync(CancellationToken ct)
    {
        try { await CallAsync("provision", new JsonObject(), ct); }
        catch (DeviceException) { /* provision is an optional test-reset command */ }
    }

    public async Task<DeviceIdentity> GetIdentityAsync(CancellationToken ct)
    {
        if (_identity is not null) return _identity;
        var r = await CallAsync("identity", new JsonObject(), ct);
        return _identity = new DeviceIdentity
        {
            DeviceId = (string?)r["device_id"] ?? "", Model = (string?)r["model"] ?? "",
            Hardware = (string?)r["hardware"] ?? "", Region = (string?)r["region"] ?? ""
        };
    }

    public async Task<long> GetNowAsync(CancellationToken ct)
    {
        var r = await CallAsync("now", new JsonObject(), ct);
        return (long?)r["now"] ?? throw new DeviceException("device returned no clock value");
    }

    /// <summary>
    /// Restores a baseline before and after an isolated test. Adapters that cannot guarantee a clean
    /// baseline must throw so the failure is visible instead of silently sharing device state.
    /// </summary>
    public virtual Task ResetAsync(CancellationToken ct) =>
        throw new DeviceException($"{Name} does not support reset; isolated tests cannot run against this transport");

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
