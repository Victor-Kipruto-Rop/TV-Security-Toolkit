using System.Diagnostics;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Engine.Validation;

namespace TVSecurityToolkit.Engine.Execution;

/// <summary>validate -> compatibility -> policy -> provision device -> run.</summary>
public sealed class TestPipeline
{
    public async Task<TestResult> RunAsync(ISecurityTest test, TestContext ctx, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var evidence = new EvidenceCollector();
        if (!ctx.Registry.TryGet(test.Id, out var def))
            return new TestResult { Id = test.Id, Title = test.Id, Status = TestStatus.Error, Message = "no catalog definition" };

        TestResult Make(TestStatus s, string m) => new()
        {
            Id = def.Id, Category = def.Category, Title = def.Title, Severity = def.Severity, Status = s, Message = m,
            DurationMs = sw.ElapsedMilliseconds
        };
        TestResult WithEvidence(TestResult result)
        {
            result.Evidence = evidence.Drain();
            result.DurationMs = sw.ElapsedMilliseconds;
            return result;
        }

        var problems = TestValidator.Validate(def).Concat(CompatibilityValidator.Validate(def)).ToList();
        if (problems.Count > 0) return Make(TestStatus.Error, string.Join("; ", problems));

        var (allowed, reason) = PolicyValidator.Check(def, ctx.Policy);
        if (!allowed) return Make(TestStatus.Skipped, reason);

        try
        {
            // Isolated tests run against a known baseline: restore before, and again afterwards so a
            // state-changing test cannot influence later results.
            if (def.Isolated) await ctx.Device.ResetAsync(ct);

            await ctx.Device.ProvisionAsync(ct);
            var result = await test.RunAsync(ctx, evidence, ct);
            result.Evidence = evidence.Drain();
            result.DurationMs = sw.ElapsedMilliseconds;

            if (def.Isolated) await ctx.Device.ResetAsync(CancellationToken.None);
            return result;
        }
        catch (OperationCanceledException)
        {
            return WithEvidence(Make(TestStatus.Cancelled, "cancelled"));
        }
        catch (Exception e)
        {
            return WithEvidence(Make(TestStatus.Error, $"{e.GetType().Name}: {e.Message}"));
        }
    }
}
