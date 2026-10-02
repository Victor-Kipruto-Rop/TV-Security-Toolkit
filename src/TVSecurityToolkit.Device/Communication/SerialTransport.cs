using System.IO.Ports;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Device.Communication;

public sealed class SerialTransport : IDeviceTransport
{
    private readonly ConnectionSettings _s;
    private SerialPort? _port;
    public SerialTransport(ConnectionSettings s) => _s = s;
    public string Name => "serial " + _s.SerialPort;

    public Task OpenAsync(CancellationToken ct)
    {
        _port = new SerialPort(_s.SerialPort, _s.BaudRate) { WriteTimeout = _s.TimeoutMs };
        _port.Open();
        return Task.CompletedTask;
    }

    public Task WriteAsync(byte[] data, CancellationToken ct) => Task.Run(() => _port!.Write(data, 0, data.Length), ct);

    public Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct) => Task.Run(() =>
    {
        _port!.ReadTimeout = timeoutMs;
        try { return _port.Read(buffer, 0, buffer.Length); }
        catch (TimeoutException) { return 0; }
    }, ct);

    public Task CloseAsync() { _port?.Close(); _port = null; return Task.CompletedTask; }
    public async ValueTask DisposeAsync() => await CloseAsync();
}
