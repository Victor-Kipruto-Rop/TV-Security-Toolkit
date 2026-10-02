using System.Text.Json;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Security.Policies;

public sealed class SeverityEngine
{
    public Severity FailThreshold { get; }
    public SeverityEngine(Severity threshold) => FailThreshold = threshold;

    public static SeverityEngine Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var s = doc.RootElement.GetProperty("failThreshold").GetString() ?? "Medium";
        return new SeverityEngine(Enum.Parse<Severity>(s, true));
    }

    /// <summary>0 = complete without threshold findings, 1 = threshold findings, 2 = errors or incomplete runs.</summary>
    public int ExitCode(IEnumerable<TestResult> results)
    {
        var list = results.ToList();
        if (list.Any(r => r.Status is TestStatus.Error or TestStatus.Cancelled)) return 2;
        return list.Any(r => r.Status == TestStatus.Fail && r.Severity >= FailThreshold) ? 1 : 0;
    }
}
