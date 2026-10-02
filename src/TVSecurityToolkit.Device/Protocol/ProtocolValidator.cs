using System.Buffers.Binary;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Exceptions;

namespace TVSecurityToolkit.Device.Protocol;

public static class ProtocolValidator
{
    /// <summary>Checks the 9-byte header and returns the declared payload length.</summary>
    public static int ValidateHeader(ReadOnlySpan<byte> h)
    {
        if (h.Length < ProtocolConstants.HeaderSize) throw new ProtocolException("short header");
        if (!h[..4].SequenceEqual(ProtocolConstants.Magic)) throw new ProtocolException("bad magic");
        if (h[4] != ProtocolConstants.Version) throw new ProtocolException($"unsupported protocol version {h[4]}");
        var n = BinaryPrimitives.ReadUInt32BigEndian(h.Slice(5, 4));
        if (n > ProtocolConstants.MaxPayload) throw new ProtocolException("declared length too large");
        return (int)n;
    }

    public static void ValidateResponse(ProtocolMessage response, int expectedId)
    {
        if (response.Id != expectedId) throw new ProtocolException($"response id {response.Id} != request id {expectedId}");
    }
}
