using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine.Validation;

public static class TestValidator
{
    public static List<string> Validate(TestDefinition d)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Id)) e.Add("test has no id");
        if (string.IsNullOrWhiteSpace(d.Title)) e.Add("test has no title");
        if (d.Steps.Count == 0) e.Add("test has no steps");
        if (d.Steps.Any(s => string.IsNullOrWhiteSpace(s.Call))) e.Add("a step has no call");
        if (string.Equals(d.Id, string.Join(",", d.DependsOn), StringComparison.Ordinal))
            e.Add("test depends on itself");
        if (d.DependsOn.Count != d.DependsOn.Distinct(StringComparer.Ordinal).Count())
            e.Add("test lists a dependency more than once");
        return e;
    }
}
