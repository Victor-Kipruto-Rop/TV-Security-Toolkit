using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Device.Protocol;

public static class MessageEncoder
{
    /// <summary>Frame: "TVSP" | version u8 | length u32 BE | JSON | CRC32 u32 BE.</summary>
    public static byte[] Encode(JsonObject payload)
    {
        var body = Encoding.UTF8.GetBytes(payload.ToJsonString());
        if (body.Length > ProtocolConstants.MaxPayload) throw new ProtocolException("frame too large");
        var frame = new byte[ProtocolConstants.HeaderSize + body.Length + 4];
        ProtocolConstants.Magic.CopyTo(frame, 0);
        frame[4] = ProtocolConstants.Version;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(5, 4), (uint)body.Length);
        body.CopyTo(frame, ProtocolConstants.HeaderSize);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(ProtocolConstants.HeaderSize + body.Length, 4), CryptoUtilities.Crc32(body));
        return frame;
    }
}
