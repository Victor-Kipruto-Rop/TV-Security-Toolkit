using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Engine;
using TVSecurityToolkit.Security.Cryptography;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.IntegrationTests;

public sealed class CancellationTests
{
    [Fact]
    public async Task Cancelling_run_returns_partial_report_and_marks_remaining_tests_cancelled()
    {
        var registry = Harness.LoadRegistry();
        var key = Harness.Key;
        await using var device = Harness.Device(false);
        var context = new TestContext
        {
            Device = device,
            Registry = registry,
            Provider = new LabEntitlementProvider(key),
            Policy = TestPolicyEngine.Load(TestPaths.Config("environments", "development.json")),
            PayloadsDir = Path.Combine(TestPaths.Root, "payloads"),
            Environment = "development",
            TestTimeoutSeconds = 30
        };
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterFirstResult(cancellation);
        var engine = new TestEngine(registry);

        var report = await engine.RunAsync(context, engine.Select(), progress, cancellation.Token);

        Assert.Equal(registry.Tests.Count, report.Total);
        Assert.Contains(report.Session.Results, result => result.Status == TestStatus.Cancelled);
        Assert.Contains(report.Session.Results, result => result.Status != TestStatus.Cancelled);
        Assert.True(report.Session.FinishedUtc.HasValue);
    }

    [Fact]
    public async Task Cancelling_during_a_test_preserves_evidence_already_collected()
    {
        var registry = Harness.LoadRegistry();
        var key = Harness.Key;
        using var cancellation = new CancellationTokenSource();
        var simulator = Harness.Device(false);
        await using var device = new CancelAfterFirstCallAdapter(simulator, cancellation);
        var context = new TestContext
        {
            Device = device,
            Registry = registry,
            Provider = new LabEntitlementProvider(key),
            Policy = TestPolicyEngine.Load(TestPaths.Config("environments", "development.json")),
            PayloadsDir = Path.Combine(TestPaths.Root, "payloads"),
            Environment = "development",
            TestTimeoutSeconds = 30
        };
        var engine = new TestEngine(registry);
        var test = engine.Select(new[] { "payg.entitlement.valid-entitlement" }).ToList();

        var report = await engine.RunAsync(context, test, ct: cancellation.Token);

        var result = Assert.Single(report.Session.Results);
        Assert.Equal(TestStatus.Cancelled, result.Status);
        Assert.NotEmpty(result.Evidence);
    }

    private sealed class CancelAfterFirstResult(CancellationTokenSource cancellation) : IProgress<TVSecurityToolkit.Core.Models.TestResult>
    {
        private bool _cancelled;

        public void Report(TVSecurityToolkit.Core.Models.TestResult value)
        {
            if (_cancelled || value.Status == TestStatus.Running) return;
            _cancelled = true;
            cancellation.Cancel();
        }
    }

    private sealed class CancelAfterFirstCallAdapter(
        IDeviceAdapter inner,
        CancellationTokenSource cancellation) : IDeviceAdapter
    {
        private bool _cancelled;

        public string Name => inner.Name;
        public Task ProvisionAsync(CancellationToken ct) => inner.ProvisionAsync(ct);
        public Task<DeviceIdentity> GetIdentityAsync(CancellationToken ct) => inner.GetIdentityAsync(ct);
        public Task<long> GetNowAsync(CancellationToken ct) => inner.GetNowAsync(ct);
        public Task ResetAsync(CancellationToken ct) => inner.ResetAsync(ct);

        public async Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct)
        {
            var result = await inner.CallAsync(command, args, ct);
            if (!_cancelled)
            {
                _cancelled = true;
                cancellation.Cancel();
            }
            return result;
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
