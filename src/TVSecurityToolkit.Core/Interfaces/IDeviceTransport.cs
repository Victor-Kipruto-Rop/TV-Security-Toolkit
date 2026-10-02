namespace TVSecurityToolkit.Core.Interfaces;

/// <summary>Raw byte pipe to a device (USB bulk, serial, TCP, HTTP).</summary>
public interface IDeviceTransport : IAsyncDisposable
{
    string Name { get; }
    Task OpenAsync(CancellationToken ct);
    Task CloseAsync();
    Task WriteAsync(byte[] data, CancellationToken ct);
    /// <summary>Reads up to buffer.Length bytes. Returns 0 when the timeout elapses with no data.</summary>
    Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct);
}
