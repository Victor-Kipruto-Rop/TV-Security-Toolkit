using System.Text.Json.Nodes;

namespace TVSecurityToolkit.Engine.Assertions;

public static class DeviceAssertions
{
    public static List<string> Locked(JsonNode? state) =>
        AssertionEngine.Match(state, new JsonObject { ["entitled"] = false, ["mode"] = "locked" });
    public static List<string> Entitled(JsonNode? state) =>
        AssertionEngine.Match(state, new JsonObject { ["entitled"] = true });
}
