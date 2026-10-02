using System.Net.Sockets;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Device.Communication;

public sealed class TcpTransport : IDeviceTransport
{
    private readonly ConnectionSettings _s;
    private TcpClient? _client;
    private NetworkStream? _stream;
    public TcpTransport(ConnectionSettings s) => _s = s;
    public string Name => $"tcp {_s.Host}:{_s.Port}";

    public async Task OpenAsync(CancellationToken ct)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_s.Host, _s.Port, ct);
        _stream = _client.GetStream();
    }

    public Task WriteAsync(byte[] data, CancellationToken ct) => _stream!.WriteAsync(data, 0, data.Length, ct);

    public async Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            var bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            if (bytesRead == 0) throw new DeviceException($"TCP connection closed by remote host ({Name})");
            return bytesRead;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return 0; }
    }

    public Task CloseAsync() { _client?.Close(); _client = null; return Task.CompletedTask; }
    public async ValueTask DisposeAsync() => await CloseAsync();
}
