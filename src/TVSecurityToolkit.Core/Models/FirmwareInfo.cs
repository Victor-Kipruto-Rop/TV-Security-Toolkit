namespace TVSecurityToolkit.Core.Models;

public sealed class FirmwareInfo
{
    public string Version { get; set; } = "";
    public bool RollbackEnforced { get; set; }
}
