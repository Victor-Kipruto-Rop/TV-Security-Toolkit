namespace TVSecurityToolkit.Core.Models;

public sealed class Evidence
{
    public string Call { get; set; } = "";
    public string Args { get; set; } = "";
    public string Result { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}
