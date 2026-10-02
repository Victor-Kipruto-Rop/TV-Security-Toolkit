using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Core.Models;

public sealed class SecurityReport
{
    public string ToolkitVersion { get; set; } = "";
    public string TestProfile { get; set; } = "";
    public TestSession Session { get; set; } = new();
    public Device? Device { get; set; }
    public List<Finding> Findings { get; set; } = new();
    public int Passed => Session.Results.Count(r => r.Status == TestStatus.Pass);
    public int Failed => Session.Results.Count(r => r.Status == TestStatus.Fail);
    public int Errors => Session.Results.Count(r => r.Status == TestStatus.Error);
    public int Skipped => Session.Results.Count(r => r.Status == TestStatus.Skipped);
    public int Cancelled => Session.Results.Count(r => r.Status == TestStatus.Cancelled);
    public int Total => Session.Results.Count;
}
