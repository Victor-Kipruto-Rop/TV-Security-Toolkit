using System.Text.Json.Nodes;
using TVSecurityToolkit.Engine.Assertions;

namespace TVSecurityToolkit.UnitTests.Engine;

public class AssertionEngineTests
{
    private static JsonNode J(string s) => JsonNode.Parse(s)!;

    [Fact] public void Subset_match_ignores_extra_keys() =>
        Assert.Empty(AssertionEngine.Match(J("{\"a\":1,\"b\":2}"), J("{\"a\":1}")));

    [Fact] public void Missing_key_is_reported() =>
        Assert.Contains(AssertionEngine.Match(J("{\"a\":1}"), J("{\"b\":1}")), e => e.Contains("missing"));

    [Fact] public void Value_mismatch_is_reported() =>
        Assert.Single(AssertionEngine.Match(J("{\"ok\":false}"), J("{\"ok\":true}")));

    [Fact] public void In_operator() =>
        Assert.Empty(AssertionEngine.Match(J("{\"r\":\"b\"}"), J("{\"r\":{\"in\":[\"a\",\"b\"]}}")));

    [Fact] public void Ge_operator() =>
        Assert.Single(AssertionEngine.Match(J("{\"n\":1}"), J("{\"n\":{\"ge\":2}}")));

    [Fact] public void Arrays_compare_by_value() =>
        Assert.Empty(AssertionEngine.Match(J("{\"c\":[\"a.pkg\"]}"), J("{\"c\":[\"a.pkg\"]}")));

    [Fact] public void Nested_objects_match() =>
        Assert.Empty(AssertionEngine.Match(J("{\"s\":{\"entitled\":false,\"mode\":\"locked\",\"reason\":\"x\"}}"), J("{\"s\":{\"entitled\":false,\"mode\":\"locked\"}}")));
}
