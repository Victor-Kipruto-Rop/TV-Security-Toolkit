using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Engine;

/// <summary>
/// Orders tests for execution: declared dependencies first (a test always follows its prerequisites),
/// then higher severity, then by id for a stable order. Cycles and unknown dependencies are reported
/// as validation errors rather than silently reordering.
/// </summary>
public sealed class TestScheduler
{
    public IEnumerable<ISecurityTest> Order(IEnumerable<ISecurityTest> tests, TestRegistry registry) =>
        OrderWithProblems(tests, registry).Order;

    /// <summary>
    /// Returns the execution order together with any dependency problems found, so a caller can
    /// surface configuration errors without discarding a usable order.
    /// </summary>
    public (List<ISecurityTest> Order, List<string> Problems) OrderWithProblems(
        IEnumerable<ISecurityTest> tests, TestRegistry registry)
    {
        var selected = tests.ToList();
        var byId = selected.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var test in selected)
        {
            if (!registry.TryGet(test.Id, out var definition)) continue;
            foreach (var dependency in definition.DependsOn)
            {
                if (byId.ContainsKey(dependency)) continue;
                if (registry.Definitions.Any(d => string.Equals(d.Id, dependency, StringComparison.Ordinal)))
                {
                    problems.Add($"{test.Id} depends on '{dependency}', which was not selected for this run");
                }
                else
                {
                    problems.Add($"{test.Id} depends on unknown test '{dependency}'");
                }
            }
        }

        // Seed with tests that declare no dependencies among the selection, ordered by severity then id.
        var rank = selected
            .Select(t => new
            {
                Test = t,
                Severity = registry.TryGet(t.Id, out var d) ? (int)d.Severity : -1
            })
            .ToDictionary(x => x.Test.Id, x => x.Severity, StringComparer.Ordinal);

        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<ISecurityTest>(selected.Count);

        // Kahn's algorithm, always taking the next eligible test in severity/id order for determinism.
        while (order.Count < selected.Count)
        {
            var next = selected
                .Where(t => !emitted.Contains(t.Id))
                .Where(t =>
                {
                    if (!registry.TryGet(t.Id, out var definition)) return true;
                    return definition.DependsOn
                        .Where(byId.ContainsKey)
                        .All(d => emitted.Contains(d));
                })
                .OrderByDescending(t => rank[t.Id])
                .ThenBy(t => t.Id, StringComparer.Ordinal)
                .FirstOrDefault();

            // Nothing is eligible, so the remaining tests form a dependency cycle.
            if (next is null)
            {
                var remaining = selected.Where(t => !emitted.Contains(t.Id)).Select(t => t.Id);
                problems.Add("dependency cycle between tests: " + string.Join(", ", remaining));
                order.AddRange(selected.Where(t => !emitted.Contains(t.Id)));
                break;
            }

            order.Add(next);
            emitted.Add(next.Id);
        }

        return (order, problems);
    }
}
