using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine.Execution;

/// <summary>Runs one test through the pipeline with a timeout; converts exceptions into Error results.</summary>
public sealed class TestExecutor
{
    private readonly TestPipeline _pipeline = new();

    public async Task<TestResult> RunAsync(ISecurityTest test, TestContext ctx, CancellationToken ct)
    {
        using var cts = TestCancellation.WithTimeout(ct, TimeSpan.FromSeconds(ctx.TestTimeoutSeconds));
        try
        {
            var result = await _pipeline.RunAsync(test, ctx, cts.Token);
            if (result.Status == TestStatus.Cancelled && !ct.IsCancellationRequested)
            {
                result.Status = TestStatus.Error;
                result.Message = $"timed out after {ctx.TestTimeoutSeconds}s";
            }
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error(test, ctx, $"timed out after {ctx.TestTimeoutSeconds}s");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            return Error(test, ctx, $"{e.GetType().Name}: {e.Message}");
        }
    }

    private static TestResult Error(ISecurityTest test, TestContext ctx, string message)
    {
        ctx.Registry.TryGet(test.Id, out var d);
        return new TestResult
        {
            Id = test.Id, Category = d?.Category ?? "", Title = d?.Title ?? test.Id,
            Severity = d?.Severity ?? Severity.Medium, Status = TestStatus.Error, Message = message
        };
    }
}
