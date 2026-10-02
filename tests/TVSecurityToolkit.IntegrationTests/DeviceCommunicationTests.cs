using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.IntegrationTests;

/// <summary>Runs the same suite through the full framed protocol (encoder, CRC, chunked reassembly, decoder).</summary>
public class DeviceCommunicationTests
{
    [Fact]
    public async Task Whole_suite_passes_over_the_protocol_loopback()
    {
        var report = await Harness.RunAsync("development", viaProtocol: true);
        Assert.All(report.Session.Results, r => Assert.True(r.Status == TestStatus.Pass, $"{r.Id}: {r.Message}"));
    }

    [Fact]
    public async Task Flaws_are_detected_identically_over_the_protocol()
    {
        var direct = await Harness.RunAsync("development", false, "accept_replay", "no_msg_mac");
        var proto = await Harness.RunAsync("development", true, "accept_replay", "no_msg_mac");
        Assert.Equal(direct.Findings.Select(f => f.TestId).OrderBy(x => x), proto.Findings.Select(f => f.TestId).OrderBy(x => x));
    }

    [Fact]
    public async Task Identity_is_available_over_the_protocol()
    {
        await using var dev = Harness.Device(true);
        var id = await dev.GetIdentityAsync(CancellationToken.None);
        Assert.Equal("SIM-0001", id.DeviceId);
    }
}
