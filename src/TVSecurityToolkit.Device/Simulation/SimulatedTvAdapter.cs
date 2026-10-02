using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Device.Communication;
using TVSecurityToolkit.Security.Cryptography;
using TVSecurityToolkit.Security.Firmware;

namespace TVSecurityToolkit.Device.Simulation;

/// <summary>
/// Reference PAYG TV that behaves correctly by default. "Flaws" switch off individual protections so
/// the toolkit's ability to detect them can be demonstrated (e.g. accept_replay, skip_signature).
/// </summary>
public sealed class SimulatedTvAdapter : RpcDeviceAdapter
{
    public const long T0 = 1_780_000_000;
    public const string DeviceId = "SIM-0001", Model = "SIM-TV-55", Hardware = "rev-B", Region = "KE";
    private static readonly string[] Regions = { "KE", "UG", "TZ" };
    private const long Grace = 72 * 3600, Skew = 60, ClockTol = 300;

    public static readonly IReadOnlySet<string> KnownFlaws = new HashSet<string>
    {
        "skip_signature", "accept_expired", "accept_replay", "no_device_binding", "no_clock_check",
        "reset_clears_nonces", "skip_update_signature", "skip_hash_check", "allow_downgrade", "no_model_check",
        "no_ab_recovery", "boot_corrupt_ok", "secure_boot_off", "boot_chain_broken", "fw_modified",
        "accept_any_cert", "allow_tls10", "no_auth", "no_authz", "no_msg_replay", "no_msg_mac",
        "world_readable", "plaintext_storage", "plaintext_creds", "verbose_logs", "debug_open", "diag_noauth"
    };

    private readonly byte[] _key;
    private readonly HashSet<string> _flaws;
    private readonly SignatureVerifier _verifier;

    // device state
    private long _offset, _highWater, _lastSeq;
    private HashSet<string> _nonces = new();
    private long? _activeUntil;
    private bool _offline, _bricked;
    private Version _fw = new(1, 4, 0), _minFw = new(1, 3, 0);
    private Version? _prevFw;

    public SimulatedTvAdapter(byte[] vendorKey, IEnumerable<string>? flaws = null)
    {
        _flaws = (flaws ?? Array.Empty<string>()).Where(f => f.Length > 0).ToHashSet();
        var bad = _flaws.Where(f => !KnownFlaws.Contains(f)).ToList();
        if (bad.Count > 0) throw new ArgumentException("unknown flaw(s): " + string.Join(", ", bad));
        _key = vendorKey;
        _verifier = new SignatureVerifier(vendorKey);
        Reset();
    }

    public override string Name => "simulator";
    private bool Has(string f) => _flaws.Contains(f);
    private long Now => T0 + _offset;

    /// <summary>Restores the full simulator baseline so isolated tests are reproducible.</summary>
    public override Task ResetAsync(CancellationToken ct)
    {
        Reset();
        return Task.CompletedTask;
    }

    private void Reset()
    {
        _offset = 0; _highWater = T0; _nonces = new(); _activeUntil = null; _offline = false;
        _fw = new Version(1, 4, 0); _prevFw = null; _bricked = false; _lastSeq = 0;
    }

    private static JsonObject Rej(string reason) => new() { ["accepted"] = false, ["reason"] = reason };
    private static JsonObject Ok() => new() { ["accepted"] = true, ["reason"] = "ok" };
    private static string S(JsonObject a, string k) => (string?)a[k] ?? "";
    private static long Number(JsonObject args, string name)
    {
        if (args[name] is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue)) return longValue;
            if (value.TryGetValue<int>(out var intValue)) return intValue;
        }
        throw new ArgumentException($"'{name}' must be an integer", nameof(args));
    }

    public override Task<JsonNode> CallAsync(string command, JsonObject args, CancellationToken ct)
    {
        JsonNode r = command switch
        {
            "provision" => DoProvision(),
            "identity" => new JsonObject { ["device_id"] = DeviceId, ["model"] = Model, ["hardware"] = Hardware, ["region"] = Region },
            "now" => new JsonObject { ["now"] = Now },
            "advance" => Advance(Number(args, "seconds")),
            "set_clock" => SetClock(Number(args, "delta")),
            "go_offline" => GoOffline(),
            "reconnect" => Reconnect(),
            "state" => State(),
            "apply_entitlement" => ApplyEntitlement(args["entitlement"]),
            "factory_reset" => FactoryReset(),
            "apply_update" => ApplyUpdate(args["package"], (bool?)args["interrupt"] ?? false, (string?)args["source"] ?? "network"),
            "info" => new JsonObject
            {
                ["device_id"] = DeviceId, ["model"] = Model, ["hardware"] = Hardware, ["region"] = Region,
                ["fw_version"] = _fw.ToString(), ["rollback_enforced"] = !Has("allow_downgrade")
            },
            "firmware_hash" => new JsonObject
            {
                ["sha256"] = HashService.Sha256Hex(Encoding.UTF8.GetBytes("fw-" + _fw)), ["matches_reference"] = !Has("fw_modified")
            },
            "installed_signature" => new JsonObject { ["valid"] = !Has("fw_modified") },
            "secure_boot_state" => new JsonObject { ["enabled"] = !Has("secure_boot_off"), ["locked"] = !Has("secure_boot_off") },
            "verify_boot_chain" => new JsonObject { ["ok"] = !Has("boot_chain_broken"), ["stages"] = new JsonArray { "rom", "bootloader", "kernel", "rootfs" } },
            "validate_version" => new JsonObject { ["supported"] = Version.Parse(S(args, "version")) >= _minFw },
            "check_compatibility" => new JsonObject
            {
                ["compatible"] = S(args, "model") == Model && S(args, "hardware") == Hardware && Regions.Contains(S(args, "region"))
            },
            "boot" => Boot(S(args, "image") is { Length: > 0 } img ? img : "valid"),
            "rollback" => Rollback(),
            "usb_discover" => UsbDiscover(args["files"] as JsonArray),
            "tls_connect" => TlsConnect(S(args, "scenario") is { Length: > 0 } sc ? sc : "valid"),
            "api_call" => ApiCall(args),
            "send_message" => SendMessage((long)args["seq"]!, (bool?)args["tamper"] ?? false),
            "storage_audit" => new JsonObject
            {
                ["world_readable_files"] = Has("world_readable") ? 3 : 0, ["unencrypted_sensitive_files"] = Has("plaintext_storage") ? 2 : 0
            },
            "credential_storage_audit" => new JsonObject { ["plaintext_credentials"] = Has("plaintext_creds") ? 1 : 0, ["hardcoded_credentials"] = 0 },
            "log_audit" => new JsonObject { ["sensitive_matches"] = Has("verbose_logs") ? 5 : 0 },
            "debug_probe" => new JsonObject
            {
                ["uart_shell_open"] = Has("debug_open"), ["jtag_enabled"] = Has("debug_open"), ["debug_port_listening"] = Has("debug_open")
            },
            "diagnostic_access_probe" => new JsonObject { ["unauthenticated_access"] = Has("diag_noauth") },
            _ => throw new DeviceException("unknown command " + command)
        };
        return Task.FromResult(r);
    }

    private JsonObject DoProvision() { Reset(); return new JsonObject { ["ok"] = true }; }
    private JsonObject Advance(long s) { _offset += s; _highWater = Math.Max(_highWater, Now); return new JsonObject { ["now"] = Now }; }
    private JsonObject SetClock(long d) { _offset += d; return new JsonObject { ["now"] = Now }; }
    private JsonObject GoOffline() { _offline = true; return new JsonObject { ["offline"] = true }; }
    private JsonObject Reconnect() { _offline = false; return new JsonObject { ["reconnected"] = true, ["state"] = State() }; }

    private bool ClockOk() => Has("no_clock_check") || Now >= _highWater - ClockTol;

    private JsonObject State()
    {
        var n = Now;
        JsonObject Locked(string why) => new() { ["entitled"] = false, ["mode"] = "locked", ["reason"] = why };
        if (!ClockOk()) return Locked("clock_tamper");
        if (_activeUntil is null) return Locked("no_entitlement");
        if (n < _activeUntil) return new JsonObject { ["entitled"] = true, ["mode"] = "active" };
        if (_offline && n < _activeUntil + Grace) return new JsonObject { ["entitled"] = true, ["mode"] = "grace" };
        return Locked("expired");
    }

    /// <summary>Accepts a dictionary, a {"$b64": ...} blob of JSON, or a JSON string; null when unparseable.</summary>
    private static JsonNode? Load(JsonNode? n)
    {
        try
        {
            if (n is JsonObject o)
            {
                if (o.Count == 1 && o["$b64"] is JsonValue b)
                    return JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String((string)b!)));
                return o;
            }
            if (n is JsonValue v && v.TryGetValue<string>(out var s)) return JsonNode.Parse(s);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or FormatException) { }
        return null;
    }

    private JsonObject ApplyEntitlement(JsonNode? arg)
    {
        if (Load(arg) is not JsonObject doc) return Rej("malformed");
        foreach (var k in new[] { "device_id", "nonce", "issued_at", "expires_at", "sig" })
            if (doc[k] is null) return Rej("malformed");
        var body = new JsonObject();
        foreach (var kv in doc) if (kv.Key != "sig") body[kv.Key] = kv.Value?.DeepClone();

        string sig, nonce, dev; long issued, expires;
        try
        {
            sig = (string)doc["sig"]!; dev = (string)doc["device_id"]!;
            issued = (long)doc["issued_at"]!; expires = (long)doc["expires_at"]!;
            nonce = (string)doc["nonce"]!;
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException) { return Rej("malformed"); }

        if (!Has("skip_signature") && !_verifier.Verify(body, sig)) return Rej("bad_signature");
        if (nonce.Length < SecurityConstants.MinNonceLength) return Rej("invalid_nonce");
        if (dev != DeviceId && !Has("no_device_binding")) return Rej("device_mismatch");
        if (!ClockOk()) return Rej("clock_rollback");
        var n = Now;
        if (issued > n + Skew) return Rej("not_yet_valid");
        if (expires <= n && !Has("accept_expired")) return Rej("expired");
        if (_nonces.Contains(nonce) && !Has("accept_replay")) return Rej("replay");
        _nonces.Add(nonce); _activeUntil = expires; _highWater = Math.Max(_highWater, n);
        return Ok();
    }

    private JsonObject FactoryReset()
    {
        _activeUntil = null; _offline = false; _lastSeq = 0;
        if (Has("reset_clears_nonces")) _nonces.Clear();
        return new JsonObject { ["ok"] = true, ["wiped"] = true };
    }

    private JsonObject ApplyUpdate(JsonNode? package, bool interrupt, string source)
    {
        if (_bricked) return Rej("device_bricked");
        var res = FirmwareValidator.Validate(Load(package), new PackageCheckOptions
        {
            Key = _key, Model = Model, Hardware = Hardware, CurrentVersion = _fw,
            SkipSignature = Has("skip_update_signature"), SkipHash = Has("skip_hash_check"),
            SkipModel = Has("no_model_check"), AllowDowngrade = Has("allow_downgrade")
        });
        if (!res.Accepted) return Rej(res.Reason);
        if (interrupt)
        {
            if (Has("no_ab_recovery")) { _bricked = true; return new JsonObject { ["accepted"] = false, ["reason"] = "interrupted", ["bootable"] = false, ["running_version"] = null }; }
            return new JsonObject { ["accepted"] = false, ["reason"] = "interrupted", ["bootable"] = true, ["running_version"] = _fw.ToString() };
        }
        _prevFw = _fw; _fw = res.Version!;
        return new JsonObject { ["accepted"] = true, ["reason"] = "ok", ["source"] = source, ["running_version"] = _fw.ToString() };
    }

    private JsonObject Boot(string image)
    {
        if (_bricked) return new JsonObject { ["booted"] = false, ["fallback"] = null };
        if (image == "corrupt" && !Has("boot_corrupt_ok")) return new JsonObject { ["booted"] = false, ["fallback"] = "recovery" };
        return new JsonObject { ["booted"] = true, ["running_version"] = _fw.ToString() };
    }

    private JsonObject Rollback()
    {
        if (_prevFw is null) return new JsonObject { ["rolled_back"] = false, ["reason"] = "no_previous" };
        _fw = _prevFw; _prevFw = null;
        return new JsonObject { ["rolled_back"] = true, ["running_version"] = _fw.ToString() };
    }

    private static JsonObject UsbDiscover(JsonArray? files)
    {
        var list = new JsonArray();
        foreach (var f in files?.Select(x => (string?)x ?? "") ?? Enumerable.Empty<string>())
            if (f.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase) && !f.StartsWith('.')) list.Add(f);
        return new JsonObject { ["candidates"] = list };
    }

    private JsonObject TlsConnect(string scenario)
    {
        if (scenario == "valid" || Has("accept_any_cert") || (scenario == "tls10" && Has("allow_tls10"))) return new JsonObject { ["accepted"] = true };
        return new JsonObject { ["accepted"] = false, ["reason"] = scenario };
    }

    private JsonObject ApiCall(JsonObject a)
    {
        var cred = S(a, "credential") is { Length: > 0 } c ? c : "valid";
        var endpoint = S(a, "endpoint") is { Length: > 0 } e ? e : "/diag/read";
        var role = S(a, "role") is { Length: > 0 } r ? r : "user";
        if ((cred == "none" || cred == "expired") && !Has("no_auth")) return new JsonObject { ["status"] = 401 };
        if (endpoint.StartsWith("/admin") && role != "admin" && !Has("no_authz")) return new JsonObject { ["status"] = 403 };
        return new JsonObject { ["status"] = 200 };
    }

    private JsonObject SendMessage(long seq, bool tamper)
    {
        if (tamper && !Has("no_msg_mac")) return Rej("bad_mac");
        if (seq <= _lastSeq && !Has("no_msg_replay")) return Rej("replay");
        _lastSeq = Math.Max(_lastSeq, seq);
        return Ok();
    }
}
