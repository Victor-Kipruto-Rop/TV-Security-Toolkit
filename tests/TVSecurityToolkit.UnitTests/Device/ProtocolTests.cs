using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Device.Protocol;

namespace TVSecurityToolkit.UnitTests.Device;

public class ProtocolTests
{
    [Fact]
    public void Frame_roundtrip()
    {
        var frame = MessageEncoder.Encode(new JsonObject { ["id"] = 1, ["cmd"] = "info", ["args"] = new JsonObject() });
        var back = MessageDecoder.Decode(frame);
        Assert.Equal("info", (string?)back["cmd"]);
    }

    [Fact]
    public void Corrupted_crc_is_rejected()
    {
        var frame = MessageEncoder.Encode(new JsonObject { ["id"] = 1 });
        frame[^1] ^= 1;
        Assert.Throws<ProtocolException>(() => MessageDecoder.Decode(frame));
    }

    [Fact]
    public void Bad_magic_is_rejected()
    {
        var frame = MessageEncoder.Encode(new JsonObject { ["id"] = 1 });
        frame[0] = (byte)'X';
        Assert.Throws<ProtocolException>(() => MessageDecoder.Decode(frame));
    }

    [Fact]
    public void Truncated_frame_is_rejected()
    {
        var frame = MessageEncoder.Encode(new JsonObject { ["id"] = 1 });
        Assert.Throws<ProtocolException>(() => MessageDecoder.Decode(frame[..^2]));
    }
}
