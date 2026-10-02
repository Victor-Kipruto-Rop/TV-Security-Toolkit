using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Security.Firmware;

public static class FirmwareCompatibilityChecker
{
    public static bool IsCompatible(DeviceIdentity device, string model, string hardware, IReadOnlyCollection<string> regions, string region) =>
        device.Model == model && device.Hardware == hardware && regions.Contains(region);
}
