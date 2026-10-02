using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Core.Models;

public sealed class TestResult
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public Severity Severity { get; set; }
    public TestStatus Status { get; set; }
    public string Message { get; set; } = "";
    public long DurationMs { get; set; }
    public List<Evidence> Evidence { get; set; } = new();
}
