using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Device.Communication;
using TVSecurityToolkit.Device.Simulation;
using TVSecurityToolkit.Engine;
using TVSecurityToolkit.Security.Cryptography;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.IntegrationTests;

/// <summary>Builds a registry from the real catalog + test assemblies and runs it against the simulator.</summary>
public static class Harness
{
    public static TestRegistry LoadRegistry()
    {
        var r = new TestRegistry();
        r.LoadCatalog(Path.Combine(TestPaths.Root, "test-catalog"));
        r.RegisterAssembly(typeof(TVSecurityToolkit.Tests.PayG.Replay.ReplayProtectionTest).Assembly);
        r.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Firmware.Integrity.FirmwareHashTest).Assembly);
        r.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Update.Valid.ValidUpdateTest).Assembly);
        r.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Network.TLS.TlsConfigurationTest).Assembly);
        r.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Local.Storage.SecureStorageTest).Assembly);
        return r;
    }

    public static byte[] Key => new SecurityPolicyEngine(TestPaths.Config("security-policy.json")).LabKey;

    public static IDeviceAdapter Device(bool viaProtocol, params string[] flaws)
    {
        var sim = new SimulatedTvAdapter(Key, flaws);
        return viaProtocol
            ? new ProtocolDeviceAdapter("loopback", new ProtocolClient(new LoopbackTransport(new ProtocolServer(sim)), 5000))
            : sim;
    }

    public static async Task<SecurityReport> RunAsync(string environment = "development", bool viaProtocol = false, params string[] flaws)
    {
        var registry = LoadRegistry();
        var ctx = new TestContext
        {
            Device = Device(viaProtocol, flaws), Registry = registry, Provider = new LabEntitlementProvider(Key),
            Policy = TestPolicyEngine.Load(TestPaths.Config("environments", environment + ".json")),
            PayloadsDir = Path.Combine(TestPaths.Root, "payloads"), Environment = environment, TestTimeoutSeconds = 30
        };
        var engine = new TestEngine(registry);
        return await engine.RunAsync(ctx, engine.Select());
    }
}
