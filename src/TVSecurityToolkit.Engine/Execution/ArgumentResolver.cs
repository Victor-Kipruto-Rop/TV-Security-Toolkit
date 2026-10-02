using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Exceptions;

namespace TVSecurityToolkit.Engine.Execution;

/// <summary>
/// Expands catalog argument markers:
///   "@dir/file"            -> bytes of payloads/dir/file
///   {"$entitlement": {..}} -> signed lab entitlement (times relative to device clock)
///   {"$package": {..}}     -> signed lab update package (bytes)
///   {"$bytes": "text"}     -> UTF-8 bytes of text
/// Bytes are carried as {"$b64": "..."}.
/// </summary>
public sealed class ArgumentResolver
{
    public static JsonObject B64(byte[] data) => new() { ["$b64"] = Convert.ToBase64String(data) };

    public async Task<JsonNode?> ResolveAsync(JsonNode? node, TestContext ctx, CancellationToken ct)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue v when v.TryGetValue<string>(out var s) && s.StartsWith('@'):
                var path = Path.GetFullPath(Path.Combine(ctx.PayloadsDir, s[1..]));
                if (!path.StartsWith(Path.GetFullPath(ctx.PayloadsDir), StringComparison.Ordinal))
                    throw new ValidationException("payload path escapes payloads directory: " + s);
                return B64(await File.ReadAllBytesAsync(path, ct));
            case JsonObject o when o["$entitlement"] is JsonObject spec:
            {
                var now = await ctx.Device.GetNowAsync(ct);
                var device = (string?)spec["device"] ?? "self";
                if (device == "self") device = (await ctx.Device.GetIdentityAsync(ct)).DeviceId;
                var omit = spec["omit"] is JsonArray a ? a.Select(x => (string)x!).ToList() : new List<string>();
                return ctx.Provider.BuildEntitlement(device, now + ((long?)spec["issued"] ?? -3600), now + ((long?)spec["expires"] ?? 2592000),
                    (string?)spec["nonce"] ?? "nonce-default", (bool?)spec["tamper"] ?? false, omit);
            }
            case JsonObject o when o["$package"] is JsonObject spec:
            {
                var id = await ctx.Device.GetIdentityAsync(ct);
                var model = (string?)spec["model"] ?? "self"; if (model == "self") model = id.Model;
                var hw = (string?)spec["hardware"] ?? "self"; if (hw == "self") hw = id.Hardware;
                return B64(ctx.Provider.BuildPackage(model, hw, (string?)spec["version"] ?? "1.5.0", (string?)spec["tamper"]));
            }
            case JsonObject o when o["$bytes"] is JsonValue b:
                return B64(Encoding.UTF8.GetBytes(b.GetValue<string>()));
            case JsonObject o:
                var copy = new JsonObject();
                foreach (var kv in o) copy[kv.Key] = await ResolveAsync(kv.Value, ctx, ct);
                return copy;
            case JsonArray arr:
                var list = new JsonArray();
                foreach (var x in arr) list.Add(await ResolveAsync(x, ctx, ct));
                return list;
            default:
                return node.DeepClone();
        }
    }
}
