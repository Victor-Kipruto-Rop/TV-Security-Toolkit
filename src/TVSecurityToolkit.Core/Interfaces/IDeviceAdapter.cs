using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Core.Interfaces;

public interface IDeviceAdapter : IAsyncDisposable
{
    string Name { get; }
    Task ProvisionAsync(CancellationToken ct);
    Task<DeviceIdentity> GetIdentityAsync(CancellationToken ct);
    Task<long> GetNowAsync(CancellationToken ct);
    /// <summary>Invoke a protocol command (see ProtocolConstants.Commands); returns the result object.</summary>
    Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct);

    /// <summary>
    /// Return the device to a known baseline so an isolated test cannot influence later results.
    /// Adapters that cannot guarantee a clean baseline must throw, which surfaces as a test error
    /// rather than silently sharing state between tests.
    /// </summary>
    Task ResetAsync(CancellationToken ct);
}
