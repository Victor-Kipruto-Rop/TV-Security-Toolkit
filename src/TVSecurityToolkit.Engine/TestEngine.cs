using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine;

/// <summary>Top-level facade: select tests (all, by id, or by category) and run them.</summary>
public sealed class TestEngine
{
    public TestRegistry Registry { get; }
    private readonly TestOrchestrator _orchestrator;

    public TestEngine(TestRegistry registry, ITestRunner? runner = null)
    {
        Registry = registry;
        _orchestrator = new TestOrchestrator(runner ?? new TestRunner());
    }

    public IEnumerable<ISecurityTest> Select(IEnumerable<string>? ids = null, IEnumerable<string>? categories = null)
    {
        var idSet = ids?.ToHashSet();
        var catSet = categories?.ToHashSet();
        return Registry.Tests.Where(t =>
            (idSet is null || idSet.Contains(t.Id)) &&
            (catSet is null || (Registry.TryGet(t.Id, out var d) && catSet.Contains(d.Category))));
    }

    public Task<SecurityReport> RunAsync(TestContext ctx, IEnumerable<ISecurityTest> tests,
        IProgress<TestResult>? progress = null, CancellationToken ct = default) =>
        _orchestrator.RunSessionAsync(ctx, tests, progress, ct);
}
