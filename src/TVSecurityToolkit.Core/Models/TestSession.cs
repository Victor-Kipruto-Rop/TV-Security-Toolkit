namespace TVSecurityToolkit.Core.Models;

public sealed class TestSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; set; }
    public string Environment { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public List<TestResult> Results { get; set; } = new();
}
