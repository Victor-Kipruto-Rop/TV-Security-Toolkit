using System.Text.Json.Nodes;

namespace TVSecurityToolkit.Engine.Assertions;

public static class FirmwareAssertions
{
    public static List<string> Version(JsonNode? info, string version) =>
        AssertionEngine.Match(info, new JsonObject { ["fw_version"] = version });
}
