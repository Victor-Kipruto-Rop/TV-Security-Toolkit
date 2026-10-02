using System.Text.Json.Nodes;

namespace TVSecurityToolkit.Engine.Assertions;

public static class SecurityAssertions
{
    public static List<string> Rejected(JsonNode? result, string reason) =>
        AssertionEngine.Match(result, new JsonObject { ["accepted"] = false, ["reason"] = reason });
    public static List<string> Accepted(JsonNode? result) =>
        AssertionEngine.Match(result, new JsonObject { ["accepted"] = true });
}
