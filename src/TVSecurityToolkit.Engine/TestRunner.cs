using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Engine.Execution;

namespace TVSecurityToolkit.Engine;

public sealed class TestRunner : ITestRunner
{
    private readonly TestExecutor _executor = new();
    private readonly TestScheduler _scheduler = new();

    public async Task<List<TestResult>> RunAsync(IEnumerable<ISecurityTest> tests, ITestContext context,
        IProgress<TestResult>? progress, CancellationToken ct)
    {
        var ctx = (TestContext)context;
        var (ordered, dependencyProblems) = _scheduler.OrderWithProblems(tests, ctx.Registry);
        var results = new List<TestResult>(ordered.Count);

        // Set when stopOnCriticalFailure trips; remaining tests are recorded as cancelled so the
        // report still accounts for every test that was selected.
        var halted = false;
        var haltReason = "";

        // Dependency problems are surfaced once, as a cancel reason on the first test, so they are visible in
        // the report. Only a cycle stops the run outright; unknown or unselected dependencies are
        // informational and the run continues.
        var cycles = dependencyProblems.Where(p => p.StartsWith("dependency cycle", StringComparison.Ordinal)).ToList();
        if (cycles.Count > 0)
        {
            halted = true;
            haltReason = "not executed: " + string.Join("; ", cycles);
        }
        else if (dependencyProblems.Count > 0)
        {
            ctx.DependenciesSkipped.AddRange(dependencyProblems);
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var test = ordered[i];
            ctx.Registry.TryGet(test.Id, out var activeDefinition);
            if (!ct.IsCancellationRequested && !halted)
            {
                progress?.Report(new TestResult
                {
                    Id = test.Id,
                    Category = activeDefinition?.Category ?? "",
                    Title = activeDefinition?.Title ?? test.Id,
                    Severity = activeDefinition?.Severity ?? TVSecurityToolkit.Core.Enums.Severity.Medium,
                    Status = TVSecurityToolkit.Core.Enums.TestStatus.Running
                });
            }
            TestResult r;
            if (ct.IsCancellationRequested || halted)
            {
                r = new TestResult
                {
                    Id = test.Id,
                    Category = activeDefinition?.Category ?? "",
                    Title = activeDefinition?.Title ?? test.Id,
                    Severity = activeDefinition?.Severity ?? TVSecurityToolkit.Core.Enums.Severity.Medium,
                    Status = TVSecurityToolkit.Core.Enums.TestStatus.Cancelled,
                    Message = halted ? haltReason : "cancelled before execution"
                };
            }
            else
            {
                r = await _executor.RunAsync(test, ctx, ct);

                if (ctx.StopOnCriticalFailure
                    && r.Severity == TVSecurityToolkit.Core.Enums.Severity.Critical
                    && r.Status is TVSecurityToolkit.Core.Enums.TestStatus.Fail or TVSecurityToolkit.Core.Enums.TestStatus.Error)
                {
                    halted = true;
                    haltReason = $"not executed: a critical test failed ({r.Id}) and stopOnCriticalFailure is enabled";
                }
            }
            results.Add(r);
            progress?.Report(r);
        }
        return results;
    }
}
