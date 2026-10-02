using System.Text.Json.Nodes;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Engine.Assertions;

/// <summary>
/// Subset matching: every key in 'expected' must be present and match in 'actual'.
/// Operator objects: {"in":[..]}, {"ge":n}, {"le":n}, {"not":x}.
/// </summary>
public static class AssertionEngine
{
    private static readonly HashSet<string> Ops = new() { "in", "ge", "le", "not" };

    public static List<string> Match(JsonNode? actual, JsonNode? expected)
    {
        var errors = new List<string>();
        Match(actual, expected, "", errors);
        return errors;
    }

    private static string Show(JsonNode? n) => n?.ToJsonString() ?? "null";
    private static bool Same(JsonNode? a, JsonNode? b) => CryptoUtilities.Canonical(a) == CryptoUtilities.Canonical(b);

    private static void Match(JsonNode? actual, JsonNode? expected, string path, List<string> errors)
    {
        var p = path.Length == 0 ? "" : path + ": ";
        if (expected is JsonObject eo)
        {
            if (eo.Count > 0 && eo.All(k => Ops.Contains(k.Key)))
            {
                if (eo["in"] is JsonArray set && !set.Any(x => Same(x, actual)))
                    errors.Add($"{p}expected one of {Show(set)}, got {Show(actual)}");
                if (eo["ge"] is JsonNode ge && !(Num(actual) >= Num(ge))) errors.Add($"{p}expected >= {Show(ge)}, got {Show(actual)}");
                if (eo["le"] is JsonNode le && !(Num(actual) <= Num(le))) errors.Add($"{p}expected <= {Show(le)}, got {Show(actual)}");
                if (eo.ContainsKey("not") && Same(actual, eo["not"])) errors.Add($"{p}must not equal {Show(eo["not"])}");
                return;
            }
            if (actual is not JsonObject ao) { errors.Add($"{p}expected object, got {Show(actual)}"); return; }
            foreach (var kv in eo)
            {
                var sub = path.Length == 0 ? kv.Key : path + "." + kv.Key;
                if (!ao.ContainsKey(kv.Key)) { errors.Add($"{sub}: missing"); continue; }
                Match(ao[kv.Key], kv.Value, sub, errors);
            }
            return;
        }
        if (!Same(actual, expected)) errors.Add($"{p}expected {Show(expected)}, got {Show(actual)}");
    }

    private static double Num(JsonNode? n)
    {
        try { return n is null ? double.NaN : (double)n; } catch { return double.NaN; }
    }
}
