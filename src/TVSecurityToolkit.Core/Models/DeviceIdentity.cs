namespace TVSecurityToolkit.Core.Models;

public sealed class DeviceIdentity
{
    public string DeviceId { get; set; } = "";
    public string Model { get; set; } = "";
    public string Hardware { get; set; } = "";
    public string Region { get; set; } = "";
}
