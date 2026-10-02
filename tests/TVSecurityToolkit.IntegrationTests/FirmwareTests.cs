namespace TVSecurityToolkit.IntegrationTests;

public class FirmwareTests
{
    [Fact]
    public async Task Downgrade_and_signature_flaws_are_detected()
    {
        var report = await Harness.RunAsync("development", false, "allow_downgrade", "skip_update_signature");
        var failed = report.Findings.Select(f => f.TestId).ToHashSet();
        Assert.Contains("firmware.rollback.rollback-protection", failed);
        Assert.Contains("update.downgrade.downgrade-protection", failed);
        Assert.Contains("update.signature.invalid-signature", failed);
    }

    [Fact]
    public async Task Secure_boot_flaw_is_detected()
    {
        var report = await Harness.RunAsync("development", false, "secure_boot_off");
        Assert.Contains(report.Findings, f => f.TestId == "firmware.secure-boot.secure-boot-state");
    }
}
