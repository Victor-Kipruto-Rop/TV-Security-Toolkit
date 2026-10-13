using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Device.Communication;

namespace TVSecurityToolkit.UnitTests.Device;

public sealed class ReadOnlyTransportBoundaryTests
{
    [Fact]
    public async Task Protocol_client_rejects_unallowlisted_command_before_opening_transport()
    {
        var transport = new RecordingTransport();
        await using var client = new ProtocolClient(
            transport, enforceReadOnlyCommandAllowlist: true);

        var error = await Assert.ThrowsAsync<DeviceException>(
            () => client.CallAsync("apply_entitlement", new JsonObject(), CancellationToken.None));

        Assert.Contains("read-only policy", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(transport.Opened);
        Assert.False(transport.Wrote);
    }

    [Fact]
    public async Task Hardware_adapter_rejects_unallowlisted_command_even_if_client_policy_is_disabled()
    {
        var transport = new RecordingTransport();
        var client = new ProtocolClient(transport, enforceReadOnlyCommandAllowlist: false);
        await using var adapter = new ProtocolDeviceAdapter(
            "test transport", client, enforceReadOnlyCommandAllowlist: true);

        var error = await Assert.ThrowsAsync<DeviceException>(
            () => adapter.CallAsync("factory_reset", new JsonObject(), CancellationToken.None));

        Assert.Contains("read-only policy", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(transport.Opened);
        Assert.False(transport.Wrote);
    }

    [Fact]
    public async Task Hardware_adapter_still_rejects_unknown_protocol_commands()
    {
        var transport = new RecordingTransport();
        var client = new ProtocolClient(transport);
        await using var adapter = new ProtocolDeviceAdapter(
            "test transport", client, enforceReadOnlyCommandAllowlist: true);

        await Assert.ThrowsAsync<ProtocolException>(
            () => adapter.CallAsync("not-a-protocol-command", new JsonObject(), CancellationToken.None));

        Assert.False(transport.Opened);
        Assert.False(transport.Wrote);
    }

    private sealed class RecordingTransport : IDeviceTransport
    {
        public string Name => "recording";
        public bool Opened { get; private set; }
        public bool Wrote { get; private set; }

        public Task OpenAsync(CancellationToken ct)
        {
            Opened = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync() => Task.CompletedTask;

        public Task WriteAsync(byte[] data, CancellationToken ct)
        {
            Wrote = true;
            return Task.CompletedTask;
        }

        public Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct) =>
            Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
