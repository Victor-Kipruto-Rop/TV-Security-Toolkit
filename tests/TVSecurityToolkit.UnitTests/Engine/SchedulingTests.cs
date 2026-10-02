using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Engine;
using TVSecurityToolkit.Engine.Validation;

namespace TVSecurityToolkit.UnitTests.Engine;

public class SchedulingTests
{
    private static (TestRegistry registry, FakeTest a, FakeTest b, FakeTest c) Build(
        string aId, string[] aDeps, string bId, string[] bDeps, string cId, string[] cDeps)
    {
        var registry = new TestRegistry();
        registry.LoadDefinitions(new[]
        {
            new TestDefinition { Id = aId, Title = aId, Category = "payg", Severity = Core.Enums.Severity.Low, DependsOn = aDeps.ToList() },
            new TestDefinition { Id = bId, Title = bId, Category = "payg", Severity = Core.Enums.Severity.High, DependsOn = bDeps.ToList() },
            new TestDefinition { Id = cId, Title = cId, Category = "payg", Severity = Core.Enums.Severity.Critical, DependsOn = cDeps.ToList() }
        });
        return (registry, new FakeTest(aId), new FakeTest(bId), new FakeTest(cId));
    }

    [Fact]
    public void Dependency_runs_after_its_prerequisite_even_when_lower_severity()
    {
        var (registry, a, b, _c) = Build(
            "a.first", Array.Empty<string>(),
            "b.dependent", new[] { "a.first" },
            "c.other", Array.Empty<string>());

        var order = new TestScheduler().Order(new ISecurityTest[] { b, a }, registry).Select(t => t.Id).ToList();

        Assert.True(order.IndexOf("a.first") < order.IndexOf("b.dependent"),
            "dependent test must run after its prerequisite: " + string.Join(" -> ", order));
    }

    [Fact]
    public void Higher_severity_still_wins_when_no_dependencies_exist()
    {
        var (registry, a, b, c) = Build(
            "a.low", Array.Empty<string>(),
            "b.high", Array.Empty<string>(),
            "c.critical", Array.Empty<string>());

        var order = new TestScheduler().Order(new ISecurityTest[] { a, b, c }, registry).Select(t => t.Id).ToList();

        Assert.Equal(new[] { "c.critical", "b.high", "a.low" }, order);
    }

    [Fact]
    public void Dependency_cycle_is_reported_rather_than_looping_forever()
    {
        var (registry, a, b, _c) = Build(
            "a.one", new[] { "b.two" },
            "b.two", new[] { "a.one" },
            "c.other", Array.Empty<string>());

        var (order, problems) = new TestScheduler().OrderWithProblems(new ISecurityTest[] { a, b }, registry);

        Assert.Contains(problems, p => p.Contains("dependency cycle", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, order.Count);
    }

    [Fact]
    public void Unknown_dependency_is_reported()
    {
        var (registry, a, _b, _c) = Build(
            "a.one", new[] { "does.not.exist" },
            "b.two", Array.Empty<string>(),
            "c.three", Array.Empty<string>());

        var (_order, problems) = new TestScheduler().OrderWithProblems(new ISecurityTest[] { a }, registry);

        Assert.Contains(problems, p => p.Contains("does.not.exist", StringComparison.Ordinal));
    }

    [Fact]
    public void Dependency_not_in_the_selection_is_reported()
    {
        var (registry, a, _b, _c) = Build(
            "a.one", new[] { "b.two" },
            "b.two", Array.Empty<string>(),
            "c.three", Array.Empty<string>());

        var (_order, problems) = new TestScheduler().OrderWithProblems(new ISecurityTest[] { a }, registry);

        Assert.Contains(problems, p => p.Contains("was not selected", StringComparison.OrdinalIgnoreCase));
    }
[Fact]
    public void Self_dependency_is_rejected_by_validation()
    {
        var definition = new TestDefinition
        {
            Id = "a.one", Title = "t", Category = "payg",
            Steps = { new TestStep { Call = "state" } }
        };
        definition.DependsOn.Add("a.one");

        Assert.Contains(TestValidator.Validate(definition), v => v.Contains("itself", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Duplicate_dependencies_are_rejected_by_validation()
    {
        var definition = new TestDefinition
        {
            Id = "a.one", Title = "t", Category = "payg",
            Steps = { new TestStep { Call = "state" } },
            DependsOn = { "b.two", "b.two" }
        };

        Assert.Contains(TestValidator.Validate(definition), v => v.Contains("more than once", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Valid_definition_produces_no_validation_problems()
    {
        var definition = new TestDefinition
        {
            Id = "a.one", Title = "t", Category = "payg",
            Steps = { new TestStep { Call = "state" } },
            DependsOn = { "b.two" }
        };

        Assert.Empty(TestValidator.Validate(definition));
    }

    [Fact]
    public void Shipped_catalog_declares_no_dependency_problems()
    {
        var registry = new TestRegistry();
        registry.LoadCatalog(Path.Combine(TestPaths.Root, "test-catalog"));
        var ids = registry.Definitions.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var definition in registry.Definitions)
        {
            foreach (var dependency in definition.DependsOn)
            {
                if (!ids.Contains(dependency))
                    problems.Add($"{definition.Id} -> unknown dependency '{dependency}'");
                if (dependency == definition.Id)
                    problems.Add($"{definition.Id} -> depends on itself");
            }
        }

        Assert.True(problems.Count == 0, "catalog dependency problems: " + string.Join("; ", problems));
    }

    [Fact]
    public void Full_catalog_schedules_without_cycles()
    {
        var registry = new TestRegistry();
        registry.LoadCatalog(Path.Combine(TestPaths.Root, "test-catalog"));
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.PayG.Replay.ReplayProtectionTest).Assembly);
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Update.Valid.ValidUpdateTest).Assembly);

        var (order, problems) = new TestScheduler().OrderWithProblems(registry.Tests, registry);

        Assert.DoesNotContain(problems, p => p.Contains("cycle", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(registry.Tests.Count, order.Count);
        Assert.Equal(order.Count, order.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count());
    }

    private sealed class FakeTest(string id) : ISecurityTest
    {
        public string Id => id;
        public Task<TestResult> RunAsync(ITestContext context, IEvidenceCollector evidence, CancellationToken ct) =>
            Task.FromResult(new TestResult { Id = id });
    }
}