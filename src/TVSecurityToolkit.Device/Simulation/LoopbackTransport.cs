using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Device.Simulation;

/// <summary>In-process "USB" link to a ProtocolServer. Replies in small chunks to exercise frame reassembly.</summary>
public sealed class LoopbackTransport : IDeviceTransport
{
    private readonly ProtocolServer _server;
    private readonly int _chunk;
    private byte[] _out = Array.Empty<byte>();
    private int _pos;

    public LoopbackTransport(ProtocolServer server, int chunk = 16) { _server = server; _chunk = chunk; }
    public string Name => "loopback";
    public Task OpenAsync(CancellationToken ct) => Task.CompletedTask;
    public Task CloseAsync() => Task.CompletedTask;

    public async Task WriteAsync(byte[] data, CancellationToken ct) { _out = await _server.HandleAsync(data, ct); _pos = 0; }

    public Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct)
    {
        var n = Math.Min(Math.Min(buffer.Length, _chunk), _out.Length - _pos);
        Array.Copy(_out, _pos, buffer, 0, n);
        _pos += n;
        return Task.FromResult(n);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
