using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.IntegrationTests;

public class PayGTests
{
    [Fact]
    public async Task Correct_device_passes_every_catalog_test()
    {
        var report = await Harness.RunAsync();
        Assert.Equal(48, report.Total);
        Assert.All(report.Session.Results, r => Assert.True(r.Status == TestStatus.Pass, $"{r.Id}: {r.Message}"));
    }

    [Fact]
    public async Task Replay_and_signature_flaws_are_detected()
    {
        var report = await Harness.RunAsync("development", false, "accept_replay", "skip_signature");
        var failed = report.Findings.Select(f => f.TestId).ToHashSet();
        Assert.Contains("payg.replay.replay-protection", failed);
        Assert.Contains("payg.entitlement.invalid-entitlement", failed);
    }

    [Fact]
    public async Task Clock_rollback_flaw_is_detected()
    {
        var report = await Harness.RunAsync("development", false, "no_clock_check");
        Assert.Contains(report.Findings, f => f.TestId == "payg.clock.time-integrity");
    }

    [Fact]
    public async Task Readonly_environment_skips_state_changing_tests_and_never_fails_a_correct_device()
    {
        var report = await Harness.RunAsync("production-readonly");
        Assert.True(report.Skipped > 0);
        Assert.Equal(0, report.Failed + report.Errors);
    }

    [Fact]
    public void Every_catalog_definition_has_a_test_class()
    {
        var r = Harness.LoadRegistry();
        Assert.Equal(r.Definitions.Count, r.Tests.Count);
    }
}
