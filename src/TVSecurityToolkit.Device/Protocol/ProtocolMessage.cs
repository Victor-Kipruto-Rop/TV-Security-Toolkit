using System.Text.Json.Nodes;

namespace TVSecurityToolkit.Device.Protocol;

/// <summary>Request {"id","cmd","args"} / response {"id","ok","result"|"error"}.</summary>
public sealed class ProtocolMessage
{
    public int Id { get; set; }
    public string Command { get; set; } = "";
    public JsonObject Args { get; set; } = new();
    public bool Ok { get; set; }
    public JsonNode? Result { get; set; }
    public string? Error { get; set; }

    public JsonObject ToRequestJson() => new() { ["id"] = Id, ["cmd"] = Command, ["args"] = Args.DeepClone() };

    public JsonObject ToResponseJson()
    {
        var o = new JsonObject { ["id"] = Id, ["ok"] = Ok };
        if (Ok) o["result"] = Result?.DeepClone(); else o["error"] = Error;
        return o;
    }

    public static ProtocolMessage ParseRequest(JsonObject o) => new()
    {
        Id = (int?)o["id"] ?? 0,
        Command = (string?)o["cmd"] ?? "",
        Args = o["args"] is JsonObject a ? (JsonObject)a.DeepClone() : new JsonObject()
    };

    public static ProtocolMessage ParseResponse(JsonObject o) => new()
    {
        Id = (int?)o["id"] ?? -1,
        Ok = (bool?)o["ok"] ?? false,
        Result = o["result"]?.DeepClone(),
        Error = (string?)o["error"]
    };
}
