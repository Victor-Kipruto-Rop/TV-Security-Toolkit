using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.UnitTests.Security;

public class PolicyTests
{
    private static TestDefinition Test(string call) => new()
    {
        Id = "x.y.z", Category = "payg", Title = "t", Severity = Severity.High,
        Steps = { new TestStep { Call = call, Args = new JsonObject() } }
    };

    [Fact]
    public void Readonly_environment_blocks_state_changing_tests()
    {
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "payg" }, AllowStateChanging = false });
        Assert.False(p.Decide(Test("apply_entitlement")).Allowed);
        Assert.True(p.Decide(Test("state")).Allowed);
    }

    [Fact]
    public void Missing_state_change_setting_defaults_to_readonly()
    {
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "payg" } });
        var (allowed, reason) = p.Decide(Test("apply_entitlement"));
        Assert.False(allowed);
        Assert.StartsWith("state-changing test blocked in this environment", reason);
    }

    [Fact]
    public void Readonly_environment_fails_closed_on_unknown_command()
    {
        // A command that is neither on the read-only allowlist nor on the legacy blocklist must be
        // refused, so a newly introduced command cannot silently reach a production device.
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "payg" }, AllowStateChanging = false });
        var (allowed, reason) = p.Decide(Test("brand_new_mutation"));

        Assert.False(allowed);
        Assert.Contains("brand_new_mutation", reason);
    }

    [Fact]
    public void Readonly_environment_blocks_every_command_absent_from_the_allowlist()
    {
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "payg" }, AllowStateChanging = false });
        foreach (var call in SecurityConstants.MutatingCommands)
            Assert.False(p.Decide(Test(call)).Allowed);

        foreach (var call in SecurityConstants.ReadOnlyCommands)
            Assert.True(p.Decide(Test(call)).Allowed);
    }

    [Fact]
    public void Readonly_allowlist_check_fails_closed_case_sensitively()
    {
        // Command matching is ordinal: 'State' is not the read-only 'state' command.
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "payg" }, AllowStateChanging = false });
        Assert.False(p.Decide(Test("STATE")).Allowed);
    }

    [Fact]
    public void State_changing_environment_is_unaffected_by_the_allowlist()
    {
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "payg" }, AllowStateChanging = true });
        Assert.True(p.Decide(Test("brand_new_mutation")).Allowed);
    }

    [Fact]
    public void Legacy_blocklist_still_applies_when_allowlist_is_disabled()
    {
        var p = new TestPolicyEngine(new EnvironmentPolicy
        {
            AllowedCategories = { "payg" }, AllowStateChanging = false, EnforceReadOnlyCommandAllowlist = false
        });
        Assert.False(p.Decide(Test("factory_reset")).Allowed);
        // Defence in depth is a blocklist, so an unknown command is still permitted in this mode.
        Assert.True(p.Decide(Test("brand_new_mutation")).Allowed);
    }

    [Fact]
    public void Allowlist_enforcement_is_enabled_by_default()
    {
        Assert.True(new EnvironmentPolicy().EnforceReadOnlyCommandAllowlist);
    }

    [Fact]
    public void Shipped_production_readonly_environment_enforces_the_allowlist()
    {
        var policy = TestPolicyEngine.Load(TestPaths.Config("environments", "production-readonly.json"));

        Assert.False(policy.Policy.AllowStateChanging);
        Assert.True(policy.Policy.EnforceReadOnlyCommandAllowlist);
    }

    [Fact]
    public void Shipped_environments_declare_the_allowlist_setting()
    {
        // staging is a state-changing environment by design; production-readonly is not. Both must
        // carry an explicit allowlist setting so the fail-closed default is never implicit.
        foreach (var name in new[] { "development", "staging", "production-readonly" })
        {
            var raw = File.ReadAllText(TestPaths.Config("environments", name + ".json"));
            Assert.Contains("\"enforceReadOnlyCommandAllowlist\"", raw);
        }
    }

    [Fact]
    public void Catalog_definitions_are_unique_across_files()
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(TestPaths.Root, "test-catalog"), "*.json", SearchOption.AllDirectories))
        {
            var cf = System.Text.Json.JsonSerializer.Deserialize<CatalogFile>(
                File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            if (cf is null) continue;
            foreach (var t in cf.Tests)
            {
                if (ids.TryGetValue(t.Id, out var first)) duplicates.Add($"{t.Id} ({first} and {file})");
                else ids[t.Id] = file;
            }
        }
        Assert.True(duplicates.Count == 0, "duplicate catalog ids: " + string.Join("; ", duplicates));
    }

    [Fact]
    public void Every_catalog_test_has_an_implemented_test_class()
    {
        // A catalog entry with no matching ISecurityTest class would silently never run.
        var registry = new TVSecurityToolkit.Engine.TestRegistry();
        registry.LoadCatalog(Path.Combine(TestPaths.Root, "test-catalog"));
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.PayG.Replay.ReplayProtectionTest).Assembly);
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Firmware.Integrity.FirmwareHashTest).Assembly);
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Update.Valid.ValidUpdateTest).Assembly);
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Local.Storage.SecureStorageTest).Assembly);

        var missing = registry.Definitions
            .Select(d => d.Id)
            .Where(id => !registry.Tests.Any(t => t.Id == id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.All(id => id.StartsWith("network.", StringComparison.Ordinal)),
            $"catalog definition(s) with no test class: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Recovery_and_usb_catalog_tests_are_registered()
    {
        // The .NET catalog previously had no recovery/usb entries while the Python implementation
        // did. These assertions pin the parity fix so the definitions cannot silently disappear.
        // The new tests live in the Update assembly, which does not depend on the network adapters.
        var registry = new TVSecurityToolkit.Engine.TestRegistry();
        registry.LoadCatalog(Path.Combine(TestPaths.Root, "test-catalog"));
        registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Update.Valid.ValidUpdateTest).Assembly);

        foreach (var id in new[]
        {
            "recovery.boot.boot-validation",
            "recovery.factory.factory-reset",
            "recovery.rollback.rollback",
            "usb.discovery.package-discovery",
            "usb.integrity.usb-integrity",
            "usb.malformed.malformed-package",
            "usb.signature.usb-signature"
        })
        {
            Assert.True(registry.TryGet(id, out _), $"catalog definition missing: {id}");
            Assert.True(registry.Tests.Any(t => t.Id == id), $"test class missing for: {id}");
        }
    }

    [Fact]
    public void Every_shipped_environment_allows_every_catalog_category()
    {
        // A new category is blocked until it is added to the environment allow lists. This makes
        // that dependency explicit instead of silently skipping the category at run time.
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(TestPaths.Root, "test-catalog"), "*.json", SearchOption.AllDirectories))
        {
            var cf = System.Text.Json.JsonSerializer.Deserialize<CatalogFile>(
                File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            if (cf is null) continue;
            foreach (var c in cf.Tests.Select(t => t.Category))
                if (!string.IsNullOrWhiteSpace(c)) categories.Add(c);
        }

        foreach (var env in new[] { "development", "staging", "production-readonly" })
        {
            var policy = TestPolicyEngine.Load(TestPaths.Config("environments", env + ".json"));
            var missing = categories.Where(c => !policy.Policy.AllowedCategories.Contains(c)).ToList();
            Assert.True(missing.Count == 0,
                $"environment '{env}' does not allow catalog categories: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Production_readonly_environment_blocks_all_state_changing_catalog_tests()
    {
        // The safety property the allowlist exists to guarantee, asserted against the real catalog.
        var registry = new TVSecurityToolkit.Engine.TestRegistry();
        registry.LoadCatalog(Path.Combine(TestPaths.Root, "test-catalog"));
        var policy = TestPolicyEngine.Load(TestPaths.Config("environments", "production-readonly.json"));

        var leaked = registry.Definitions
            .Where(d => d.Steps.Any(s => SecurityConstants.IsStateChanging(s.Call)))
            .Where(d => policy.Decide(d).Allowed)
            .Select(d => d.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.True(leaked.Count == 0,
            $"production-readonly would run state-changing tests: {string.Join(", ", leaked)}");
    }

    [Fact]
    public void Every_catalog_step_uses_a_protocol_command()
    {
        var bad = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(TestPaths.Root, "test-catalog"), "*.json", SearchOption.AllDirectories))
        {
            var cf = System.Text.Json.JsonSerializer.Deserialize<CatalogFile>(
                File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            if (cf is null) continue;
            foreach (var t in cf.Tests.SelectMany(t => t.Steps).Where(s => !ProtocolConstants.Commands.Contains(s.Call)))
                bad.Add($"{Path.GetFileName(file)}:{t.Call}");
        }
        Assert.True(bad.Count == 0, "catalog uses commands outside the protocol: " + string.Join(", ", bad));
    }

    [Fact]
    public void Every_catalog_command_in_a_readonly_environment_is_classified()
    {
        // Guards against catalog drift: every protocol command the shipped catalog uses must be
        // explicitly classified, so an unclassified command is refused in read-only environments
        // rather than silently permitted. Mutating commands are classified as state-changing.
        var catalogDir = Path.Combine(TestPaths.Root, "test-catalog");
        var unclassified = new SortedSet<string>(StringComparer.Ordinal);
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(catalogDir, "*.json", SearchOption.AllDirectories))
        {
            var cf = System.Text.Json.JsonSerializer.Deserialize<CatalogFile>(
                File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            if (cf is null) continue;
            foreach (var call in cf.Tests.SelectMany(t => t.Steps).Select(s => s.Call))
            {
                if (!ProtocolConstants.Commands.Contains(call)) continue;
                used.Add(call);
                // Classified read-only, or explicitly known to be state-changing.
                if (SecurityConstants.IsStateChanging(call) && !SecurityConstants.MutatingCommands.Contains(call))
                    unclassified.Add(call);
            }
        }

        Assert.True(used.Count > 0, "no catalog commands found");
        Assert.True(unclassified.Count == 0,
            "catalog uses commands that are neither read-only nor known mutating: " + string.Join(", ", unclassified));
    }

    [Fact]
    public void Category_not_in_allow_list_is_blocked()
    {
        var p = new TestPolicyEngine(new EnvironmentPolicy { AllowedCategories = { "firmware" } });
        Assert.False(p.Decide(Test("state")).Allowed);
    }

    [Fact]
    public void Severity_engine_exit_codes()
    {
        var e = new SeverityEngine(Severity.Medium);
        TestResult R(TestStatus s, Severity sev) => new() { Status = s, Severity = sev };
        Assert.Equal(0, e.ExitCode(new[] { R(TestStatus.Pass, Severity.Critical), R(TestStatus.Fail, Severity.Low) }));
        Assert.Equal(1, e.ExitCode(new[] { R(TestStatus.Fail, Severity.High) }));
        Assert.Equal(2, e.ExitCode(new[] { R(TestStatus.Error, Severity.Low) }));
    }
}
