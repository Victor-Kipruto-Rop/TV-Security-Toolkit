using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Device.Diagnostics;

public sealed class DeviceInformationClient
{
    private readonly IDeviceAdapter _device;
    public DeviceInformationClient(IDeviceAdapter device) => _device = device;
    public Task<JsonNode> GetAsync(CancellationToken ct) => _device.CallAsync("info", new JsonObject(), ct);
}
