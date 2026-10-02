using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Device.Identity;

public sealed class DeviceIdentityService
{
    private readonly IDeviceAdapter _device;
    public DeviceIdentityService(IDeviceAdapter device) => _device = device;

    public async Task<Core.Models.Device> DescribeAsync(ConnectionType type, CancellationToken ct)
    {
        var id = await _device.GetIdentityAsync(ct);
        var info = await _device.CallAsync("info", new System.Text.Json.Nodes.JsonObject(), ct);
        return new Core.Models.Device
        {
            Name = _device.Name, Connection = type, State = DeviceState.Connected, Identity = id,
            Firmware = new FirmwareInfo { Version = (string?)info["fw_version"] ?? "", RollbackEnforced = (bool?)info["rollback_enforced"] ?? false },
            Hardware = new HardwareInfo { Model = id.Model, Revision = id.Hardware, Region = id.Region }
        };
    }
}
