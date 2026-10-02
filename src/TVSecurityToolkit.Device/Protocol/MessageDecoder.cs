using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Device.Protocol;

public static class MessageDecoder
{
    public static JsonObject DecodeBody(byte[] payload, byte[] crcBytes)
    {
        if (BinaryPrimitives.ReadUInt32BigEndian(crcBytes) != CryptoUtilities.Crc32(payload)) throw new ProtocolException("crc mismatch");
        try { return JsonNode.Parse(Encoding.UTF8.GetString(payload)) as JsonObject ?? throw new ProtocolException("payload is not an object"); }
        catch (System.Text.Json.JsonException e) { throw new ProtocolException("invalid JSON payload", e); }
    }

    /// <summary>Decodes a complete frame held in memory.</summary>
    public static JsonObject Decode(byte[] frame)
    {
        var n = ProtocolValidator.ValidateHeader(frame);
        if (frame.Length != ProtocolConstants.HeaderSize + n + 4) throw new ProtocolException("length mismatch");
        return DecodeBody(frame[ProtocolConstants.HeaderSize..(ProtocolConstants.HeaderSize + n)], frame[^4..]);
    }
}
