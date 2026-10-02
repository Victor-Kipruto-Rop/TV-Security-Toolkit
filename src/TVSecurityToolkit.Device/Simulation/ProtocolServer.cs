using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Device.Protocol;

namespace TVSecurityToolkit.Device.Simulation;

/// <summary>Device-side protocol endpoint wrapping any adapter (reference implementation for device firmware).</summary>
public sealed class ProtocolServer
{
    private readonly IDeviceAdapter _device;
    public ProtocolServer(IDeviceAdapter device) => _device = device;

    public async Task<byte[]> HandleAsync(byte[] frame, CancellationToken ct)
    {
        var id = 0;
        try
        {
            var req = ProtocolMessage.ParseRequest(MessageDecoder.Decode(frame));
            id = req.Id;
            if (!ProtocolConstants.Commands.Contains(req.Command)) throw new InvalidOperationException("unknown command " + req.Command);
            var result = await _device.CallAsync(req.Command, req.Args, ct);
            return MessageEncoder.Encode(new ProtocolMessage { Id = id, Ok = true, Result = result }.ToResponseJson());
        }
        catch (Exception e)
        {
            return MessageEncoder.Encode(new ProtocolMessage { Id = id, Ok = false, Error = $"{e.GetType().Name}: {e.Message}" }.ToResponseJson());
        }
    }
}
