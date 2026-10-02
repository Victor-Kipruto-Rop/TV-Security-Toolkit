using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Device.Communication;

/// <summary>Request/response over HTTP POST: each write posts a frame, the reply body becomes readable data.</summary>
public sealed class HttpTransport : IDeviceTransport
{
    private readonly HttpClient _http = new();
    private readonly string _url;
    private byte[] _reply = Array.Empty<byte>();
    private int _pos;

    public HttpTransport(string url) => _url = url;
    public string Name => "http " + _url;
    public Task OpenAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task WriteAsync(byte[] data, CancellationToken ct)
    {
        using var content = new ByteArrayContent(data);
        using var resp = await _http.PostAsync(_url, content, ct);
        if (!resp.IsSuccessStatusCode) throw new DeviceException("HTTP " + (int)resp.StatusCode);
        _reply = await resp.Content.ReadAsByteArrayAsync(ct);
        _pos = 0;
    }

    public Task<int> ReadAsync(byte[] buffer, int timeoutMs, CancellationToken ct)
    {
        var n = Math.Min(buffer.Length, _reply.Length - _pos);
        Array.Copy(_reply, _pos, buffer, 0, n);
        _pos += n;
        return Task.FromResult(n);
    }

    public Task CloseAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() { _http.Dispose(); return ValueTask.CompletedTask; }
}
