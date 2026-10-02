using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Core.Models;

public sealed class Finding
{
    public string Id { get; set; } = "";
    public string TestId { get; set; } = "";
    public string Title { get; set; } = "";
    public Severity Severity { get; set; }
    public TestStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string AffectedComponent { get; set; } = "";
    public string ExpectedBehavior { get; set; } = "";
    public string ActualBehavior { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string FirmwareVersion { get; set; } = "";
    public DateTime ObservedUtc { get; set; }
    public List<string> EvidenceCalls { get; set; } = new();
    public string Recommendation { get; set; } = "";
}
