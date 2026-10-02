using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Engine.Execution;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.Engine;

public sealed class TestContext : ITestContext
{
    public required IDeviceAdapter Device { get; init; }
    public required TestRegistry Registry { get; init; }
    public required TestPolicyEngine Policy { get; init; }
    public required IEntitlementProvider Provider { get; init; }
    public required string PayloadsDir { get; init; }
    public string Environment { get; init; } = "development";
    public int TestTimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Stop the run after the first Critical-severity failure. Remaining tests are recorded as
    /// cancelled rather than silently omitted, so the report still accounts for every selected test.
    /// </summary>
    public bool StopOnCriticalFailure { get; init; }

    /// <summary>
    /// Non-fatal dependency problems observed during this run (for example a dependency that was not
    /// part of the selection). Reported for visibility; they do not stop the run.
    /// </summary>
    public List<string> DependenciesSkipped { get; } = new();

    public TestStepExecutor Steps { get; init; } = new();
}
