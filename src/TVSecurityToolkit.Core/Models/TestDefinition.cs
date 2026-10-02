using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Core.Models;

public sealed class TestStep
{
    public string Call { get; set; } = "";
    public JsonObject? Args { get; set; }
    public JsonNode? Expect { get; set; }
}

public sealed class TestDefinition
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Group { get; set; } = "";
    public string Title { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Medium;
    public string Description { get; set; } = "";

    /// <summary>
    /// Ids of tests that must complete successfully before this one runs. Used by the scheduler to
    /// order tests so state-dependent checks follow their prerequisites.
    /// </summary>
    public List<string> DependsOn { get; set; } = new();

    /// <summary>
    /// When true the device is returned to a known baseline before and after the test runs, so a
    /// state-changing test cannot influence later results.
    /// </summary>
    public bool Isolated { get; set; }

    public List<TestStep> Steps { get; set; } = new();
}

public sealed class CatalogFile
{
    public string Category { get; set; } = "";
    public string Group { get; set; } = "";
    public List<TestDefinition> Tests { get; set; } = new();
}
