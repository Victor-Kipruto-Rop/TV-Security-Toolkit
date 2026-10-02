using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine;

/// <summary>Base class for catalog-driven tests: the steps live in test-catalog/*.json, keyed by Id.</summary>
public abstract class CatalogTest : ISecurityTest
{
    public abstract string Id { get; }

    public async Task<TestResult> RunAsync(ITestContext context, IEvidenceCollector evidence, CancellationToken ct)
    {
        var ctx = (TestContext)context;
        var def = ctx.Registry.Get(Id);
        var (status, message) = await ctx.Steps.ExecuteAsync(def, ctx, evidence, ct);
        return new TestResult
        {
            Id = def.Id, Category = def.Category, Title = def.Title, Severity = def.Severity,
            Status = status, Message = message
        };
    }
}
