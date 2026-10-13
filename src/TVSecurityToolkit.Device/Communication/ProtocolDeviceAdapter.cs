using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Exceptions;

namespace TVSecurityToolkit.Device.Communication;

/// <summary>
/// Real-device adapter: each command becomes one framed request over the transport. When
/// read-only enforcement is enabled, both this adapter and its ProtocolClient reject unclassified
/// commands; the duplicate check is deliberate defence in depth.
/// </summary>
public sealed class ProtocolDeviceAdapter : RpcDeviceAdapter
{
    private readonly ProtocolClient _client;
    private readonly bool _enforceReadOnlyCommandAllowlist;

    public ProtocolDeviceAdapter(
        string name,
        ProtocolClient client,
        bool enforceReadOnlyCommandAllowlist = false)
    {
        Name = name;
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _enforceReadOnlyCommandAllowlist = enforceReadOnlyCommandAllowlist;
    }

    public override string Name { get; }

    public override Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct)
    {
        if (!ProtocolConstants.Commands.Contains(command))
            throw new ProtocolException("unknown command " + command);

        if (_enforceReadOnlyCommandAllowlist && SecurityConstants.IsStateChanging(command))
            throw new DeviceException(
                $"command '{command}' refused by the adapter read-only policy; only explicitly allowlisted commands are permitted");

        return _client.CallAsync(command, args, ct);
    }

    public override ValueTask DisposeAsync() => _client.DisposeAsync();
}
