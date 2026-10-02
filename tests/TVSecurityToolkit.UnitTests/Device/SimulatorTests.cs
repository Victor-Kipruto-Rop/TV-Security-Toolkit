using System.Text.Json.Nodes;
using TVSecurityToolkit.Device.Simulation;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.UnitTests.Device;

public class SimulatorTests
{
    private static readonly byte[] Key = { 9, 9, 9, 9 };
    private static readonly LabEntitlementProvider Lab = new(Key);

    private static async Task<JsonNode> Call(SimulatedTvAdapter d, string cmd, JsonObject? a = null) =>
        await d.CallAsync(cmd, a ?? new JsonObject(), CancellationToken.None);

    private static JsonObject Ent(string nonce, long issued = SimulatedTvAdapter.T0 - 3600, long expires = SimulatedTvAdapter.T0 + 86400) =>
        new() { ["entitlement"] = Lab.BuildEntitlement(SimulatedTvAdapter.DeviceId, issued, expires, nonce, false, Array.Empty<string>()) };

    [Fact]
    public async Task Valid_entitlement_then_replay_is_rejected()
    {
        var d = new SimulatedTvAdapter(Key);
        Assert.True((bool)(await Call(d, "apply_entitlement", Ent("nonce-0001")))["accepted"]!);
        var second = await Call(d, "apply_entitlement", Ent("nonce-0001"));
        Assert.Equal("replay", (string?)second["reason"]);
    }

    [Fact]
    public async Task Replay_flaw_is_honoured()
    {
        var d = new SimulatedTvAdapter(Key, new[] { "accept_replay" });
        await Call(d, "apply_entitlement", Ent("nonce-0001"));
        Assert.True((bool)(await Call(d, "apply_entitlement", Ent("nonce-0001")))["accepted"]!);
    }

    [Fact]
    public async Task Expired_entitlement_is_rejected() =>
        Assert.Equal("expired", (string?)(await Call(new SimulatedTvAdapter(Key), "apply_entitlement",
            Ent("nonce-0002", expires: SimulatedTvAdapter.T0 - 1)))["reason"]);

    [Fact]
    public async Task Clock_rollback_locks_the_device()
    {
        var d = new SimulatedTvAdapter(Key);
        await Call(d, "apply_entitlement", Ent("nonce-0003"));
        await Call(d, "advance", new JsonObject { ["seconds"] = 43200 });
        await Call(d, "set_clock", new JsonObject { ["delta"] = -43200 });
        Assert.False((bool)(await Call(d, "state"))["entitled"]!);
    }

    [Fact]
    public void Unknown_flaw_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new SimulatedTvAdapter(Key, new[] { "not_a_flaw" }));

    [Fact]
    public async Task Downgrade_package_is_blocked()
    {
        var d = new SimulatedTvAdapter(Key);
        var pkg = Lab.BuildPackage(SimulatedTvAdapter.Model, SimulatedTvAdapter.Hardware, "1.2.0", null);
        var r = await Call(d, "apply_update", new JsonObject { ["package"] = new JsonObject { ["$b64"] = Convert.ToBase64String(pkg) } });
        Assert.Equal("downgrade_blocked", (string?)r["reason"]);
    }
}
