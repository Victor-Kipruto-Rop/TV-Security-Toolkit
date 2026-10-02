using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Core.Interfaces;

public interface ITestRunner
{
    Task<List<TestResult>> RunAsync(IEnumerable<ISecurityTest> tests, ITestContext context,
        IProgress<TestResult>? progress, CancellationToken ct);
}
