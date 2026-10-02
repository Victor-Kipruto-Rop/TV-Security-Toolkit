using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Exceptions;

namespace TVSecurityToolkit.Device.Communication;

/// <summary>Real-device adapter: each command becomes one framed request over the transport.</summary>
public sealed class ProtocolDeviceAdapter : RpcDeviceAdapter
{
    private readonly ProtocolClient _client;
    public ProtocolDeviceAdapter(string name, ProtocolClient client) { Name = name; _client = client; }
    public override string Name { get; }

    public override Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct)
    {
        if (!ProtocolConstants.Commands.Contains(command)) throw new ProtocolException("unknown command " + command);
        return _client.CallAsync(command, args, ct);
    }

    public override ValueTask DisposeAsync() => _client.DisposeAsync();
}
