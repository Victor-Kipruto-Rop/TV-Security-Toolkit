namespace TVSecurityToolkit.Core.Models;

public sealed class Entitlement
{
    public string DeviceId { get; set; } = "";
    public string Nonce { get; set; } = "";
    public long IssuedAt { get; set; }
    public long ExpiresAt { get; set; }
    public string Signature { get; set; } = "";
}
