using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine;

/// <summary>Runs a selection of tests as one session and builds the report.</summary>
public sealed class TestOrchestrator
{
    private readonly ITestRunner _runner;
    public TestOrchestrator(ITestRunner runner) => _runner = runner;

    public async Task<SecurityReport> RunSessionAsync(TestContext ctx, IEnumerable<ISecurityTest> tests,
        IProgress<TestResult>? progress, CancellationToken ct)
    {
        var session = new TestSession { Environment = ctx.Environment, DeviceName = ctx.Device.Name };
        try
        {
            session.Results = await _runner.RunAsync(tests, ctx, progress, ct);
        }
        finally
        {
            session.FinishedUtc = DateTime.UtcNow;
        }
        var report = BuildReport(session, ctx.Registry);
        report.TestProfile = ctx.Environment;
        return report;
    }

    public static SecurityReport BuildReport(TestSession session, TestRegistry? registry = null) => new()
    {
        ToolkitVersion = ApplicationConstants.Version,
        Session = session,
        Findings = session.Results.Where(r => r.Status is TestStatus.Fail or TestStatus.Error)
            .Select(r =>
            {
                var expected = registry is not null && registry.TryGet(r.Id, out var definition)
                    ? string.Join("; ", definition.Steps.Where(s => s.Expect is not null)
                        .Select(s => $"{s.Call}: {s.Expect!.ToJsonString()}"))
                    : "See the test catalog for the expected behavior.";
                return new Finding
                {
                    Id = $"F-{session.Id}-{r.Id}",
                    TestId = r.Id,
                    Title = r.Title,
                    Severity = r.Severity,
                    Status = r.Status,
                    Message = r.Message,
                    AffectedComponent = r.Category,
                    ExpectedBehavior = expected,
                    ActualBehavior = r.Message,
                    DeviceName = session.DeviceName,
                    ObservedUtc = session.FinishedUtc ?? DateTime.UtcNow,
                    EvidenceCalls = r.Evidence.Select(e => e.Call).Distinct(StringComparer.Ordinal).ToList(),
                    Recommendation = "Review the associated test evidence and remediate the reported security control."
                };
            })
            .OrderByDescending(f => f.Severity).ToList()
    };
}
