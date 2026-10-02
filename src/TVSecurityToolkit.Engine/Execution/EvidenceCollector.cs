using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Engine.Execution;

/// <summary>Records each step; secrets are redacted and binary blobs summarised.</summary>
public sealed class EvidenceCollector : IEvidenceCollector
{
    private readonly List<Evidence> _items = new();

    public void Record(string call, JsonNode? args, JsonNode? result, IReadOnlyList<string> errors) =>
        _items.Add(new Evidence
        {
            Call = call,
            Args = Summarize(args)?.ToJsonString() ?? "null",
            Result = Summarize(result)?.ToJsonString() ?? "null",
            Errors = errors.ToList()
        });

    public List<Evidence> Drain() { var r = _items.ToList(); _items.Clear(); return r; }

    public static JsonNode? Summarize(JsonNode? n)
    {
        switch (n)
        {
            case null: return null;
            case JsonObject o when o.Count == 1 && o["$b64"] is JsonValue b && b.TryGetValue<string>(out var s):
                try
                {
                    var bytes = Convert.FromBase64String(s);
                    return JsonValue.Create($"<{bytes.Length} bytes sha256={HashService.Sha256Hex(bytes)[..12]}>");
                }
                catch (FormatException) { return JsonValue.Create("<invalid base64>"); }
            case JsonObject o:
                var copy = new JsonObject();
                foreach (var kv in o)
                    copy[kv.Key] = SecurityConstants.RedactedKeys.Contains(kv.Key) ? JsonValue.Create("[redacted]") : Summarize(kv.Value);
                return copy;
            case JsonArray a:
                var arr = new JsonArray();
                foreach (var x in a) arr.Add(Summarize(x));
                return arr;
            default:
                return n.DeepClone();
        }
    }
}
