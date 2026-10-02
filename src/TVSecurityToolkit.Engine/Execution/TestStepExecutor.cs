using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Engine.Assertions;

namespace TVSecurityToolkit.Engine.Execution;

public sealed class TestStepExecutor
{
    private readonly ArgumentResolver _resolver = new();

    /// <summary>Runs steps in order; stops at the first step whose result does not match its expectation.</summary>
    public async Task<(TestStatus Status, string Message)> ExecuteAsync(TestDefinition def, TestContext ctx,
        IEvidenceCollector evidence, CancellationToken ct)
    {
        for (var i = 0; i < def.Steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = def.Steps[i];
            var args = (JsonObject)(await _resolver.ResolveAsync(step.Args ?? new JsonObject(), ctx, ct))!;
            var result = await ctx.Device.CallAsync(step.Call, args, ct);
            var errors = step.Expect is null ? new List<string>() : AssertionEngine.Match(result, step.Expect);
            evidence.Record(step.Call, args, result, errors);
            if (errors.Count > 0)
                return (TestStatus.Fail, $"step {i + 1} ({step.Call}): " + string.Join("; ", errors));
        }
        return (TestStatus.Pass, "");
    }
}
