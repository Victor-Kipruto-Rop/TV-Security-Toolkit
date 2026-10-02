using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Core.Interfaces;

public interface ISecurityTest
{
    string Id { get; }
    Task<TestResult> RunAsync(ITestContext context, IEvidenceCollector evidence, CancellationToken ct);
}

/// <summary>Marker for the context object defined in the Engine project.</summary>
public interface ITestContext { }
