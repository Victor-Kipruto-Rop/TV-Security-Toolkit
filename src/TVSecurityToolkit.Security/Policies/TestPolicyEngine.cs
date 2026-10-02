using System.Text.Json;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Security.Policies;

public sealed class EnvironmentPolicy
{
    public string Name { get; set; } = "development";
    public List<string> AllowedCategories { get; set; } = new();
    public List<string> DenyTests { get; set; } = new();
    public bool AllowStateChanging { get; set; }

    /// <summary>
    /// When true, a read-only environment permits only commands on
    /// <see cref="SecurityConstants.ReadOnlyCommands"/> and refuses everything else. This is the
    /// default so an unrecognised or newly introduced command fails closed.
    /// </summary>
    public bool EnforceReadOnlyCommandAllowlist { get; set; } = true;
}

/// <summary>Decides whether a test may run in the active environment.</summary>
public sealed class TestPolicyEngine
{
    public EnvironmentPolicy Policy { get; }
    public TestPolicyEngine(EnvironmentPolicy policy) => Policy = policy;

    public static TestPolicyEngine Load(string path) =>
        new(JsonSerializer.Deserialize<EnvironmentPolicy>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("empty environment file: " + path));

    public (bool Allowed, string Reason) Decide(TestDefinition t)
    {
        if (Policy.AllowedCategories.Count > 0 && !Policy.AllowedCategories.Contains(t.Category))
            return (false, "category not allowed in this environment");
        if (Policy.DenyTests.Contains(t.Id)) return (false, "test denied in this environment");
        if (!Policy.AllowStateChanging)
        {
            // Fail closed: anything not explicitly classified as read-only is refused, so a new or
            // unknown command cannot reach a production device through an incomplete blocklist.
            if (Policy.EnforceReadOnlyCommandAllowlist)
            {
                var unknown = t.Steps.Select(s => s.Call)
                    .Where(SecurityConstants.IsStateChanging)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (unknown.Count > 0)
                    return (false, $"state-changing test blocked in this environment (not on the read-only allowlist: {string.Join(", ", unknown)})");
                return (true, "");
            }

            // Defence in depth for environments that disable the allowlist.
            if (t.Steps.Any(s => SecurityConstants.MutatingCommands.Contains(s.Call)))
                return (false, "state-changing test blocked in this environment");
        }
        return (true, "");
    }
}
