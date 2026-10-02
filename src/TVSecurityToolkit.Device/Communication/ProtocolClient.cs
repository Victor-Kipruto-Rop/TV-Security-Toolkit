using System.Diagnostics;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Device.Protocol;

namespace TVSecurityToolkit.Device.Communication;

/// <summary>Sends framed requests over a transport and reads the matching response.</summary>
public sealed class ProtocolClient : IAsyncDisposable
{
    private readonly IDeviceTransport _transport;
    private readonly int _timeoutMs;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<byte> _pending = new();
    private readonly byte[] _chunk = new byte[4096];
    private int _nextId;
    private bool _open;

    public ProtocolClient(IDeviceTransport transport, int timeoutMs = 5000) { _transport = transport; _timeoutMs = timeoutMs; }

    public async Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!_open) { await _transport.OpenAsync(ct); _open = true; }
            _pending.Clear();
            var req = new ProtocolMessage { Id = ++_nextId, Command = command, Args = args };
            await _transport.WriteAsync(MessageEncoder.Encode(req.ToRequestJson()), ct);

            var deadline = Stopwatch.StartNew();
            var header = await ReadExactAsync(ProtocolConstants.HeaderSize, deadline, ct);
            var len = ProtocolValidator.ValidateHeader(header);
            var payload = await ReadExactAsync(len, deadline, ct);
            var crc = await ReadExactAsync(4, deadline, ct);
            var resp = ProtocolMessage.ParseResponse(MessageDecoder.DecodeBody(payload, crc));

            ProtocolValidator.ValidateResponse(resp, req.Id);
            if (!resp.Ok) throw new DeviceException(resp.Error ?? "device returned an error");
            return resp.Result ?? new JsonObject();
        }
        finally { _gate.Release(); }
    }

    private async Task<byte[]> ReadExactAsync(int n, Stopwatch clock, CancellationToken ct)
    {
        while (_pending.Count < n)
        {
            var left = _timeoutMs - (int)clock.ElapsedMilliseconds;
            if (left <= 0) throw new DeviceException($"read timeout ({_pending.Count}/{n} bytes)");
            var got = await _transport.ReadAsync(_chunk, left, ct);
            if (got > 0) _pending.AddRange(_chunk.AsSpan(0, got).ToArray());
        }
        var result = _pending.GetRange(0, n).ToArray();
        _pending.RemoveRange(0, n);
        return result;
    }

    public async ValueTask DisposeAsync() { await _transport.DisposeAsync(); _gate.Dispose(); }
}
