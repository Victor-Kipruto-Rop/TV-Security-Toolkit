"""Reference simulated PAYG TV. Behaves like a *correct* device by default;
'flaws' switch off individual protections so the toolkit's detection can be
demonstrated (e.g. --flaw accept_replay,skip_signature)."""
import json
from .base import DeviceAdapter
from ..security.crypto import verify, sha256_bytes

def _ver(s): return tuple(int(x) for x in s.split("."))
def _vs(t): return ".".join(map(str, t))
def _load(x):
    if isinstance(x, dict): return x
    try: return json.loads(x if isinstance(x, (str, bytes)) else b"")
    except Exception: return None
def _rej(reason, **kw): return dict(accepted=False, reason=reason, **kw)

class SimulatedTvAdapter(DeviceAdapter):
    name = "sim"
    T0 = 1_780_000_000
    DEVICE_ID, MODEL, HW, REGION = "SIM-0001", "SIM-TV-55", "rev-B", "KE"
    REGIONS = {"KE", "UG", "TZ"}
    GRACE, SKEW, CLOCK_TOL = 72 * 3600, 60, 300
    FLAWS = {"skip_signature", "accept_expired", "accept_replay", "no_device_binding",
             "no_clock_check", "reset_clears_nonces", "skip_update_signature", "skip_hash_check",
             "allow_downgrade", "no_model_check", "no_ab_recovery", "boot_corrupt_ok",
             "secure_boot_off", "boot_chain_broken", "fw_modified", "accept_any_cert",
             "allow_tls10", "no_auth", "no_authz", "no_msg_replay", "no_msg_mac",
             "world_readable", "plaintext_storage", "plaintext_creds", "verbose_logs",
             "debug_open", "diag_noauth"}

    def __init__(self, vendor_key: bytes, flaws=()):
        bad = set(flaws) - self.FLAWS
        if bad: raise ValueError(f"unknown flaw(s): {sorted(bad)}")
        self.key, self.flaws = vendor_key, set(flaws)
        self.provision()

    def has(self, f): return f in self.flaws

    def provision(self):
        self.offset, self.hw = 0, self.T0
        self.nonces, self.active_until, self.offline = set(), None, False
        self.fw, self.prev_fw, self.min_fw = (1, 4, 0), None, (1, 3, 0)
        self.bricked, self.last_seq = False, 0

    def identity(self):
        return dict(device_id=self.DEVICE_ID, model=self.MODEL, hardware=self.HW, region=self.REGION)
    def now(self): return self.T0 + self.offset

    # ---- clock / connectivity ----
    def advance(self, seconds):
        self.offset += seconds; self.hw = max(self.hw, self.now()); return dict(now=self.now())
    def set_clock(self, delta):
        self.offset += delta; return dict(now=self.now())
    def go_offline(self): self.offline = True; return dict(offline=True)
    def reconnect(self):
        self.offline = False; return dict(reconnected=True, state=self.state())

    def _clock_ok(self):
        return self.has("no_clock_check") or self.now() >= self.hw - self.CLOCK_TOL

    def state(self):
        n = self.now()
        if not self._clock_ok(): return dict(entitled=False, mode="locked", reason="clock_tamper")
        if self.active_until is None: return dict(entitled=False, mode="locked", reason="no_entitlement")
        if n < self.active_until: return dict(entitled=True, mode="active")
        if self.offline and n < self.active_until + self.GRACE: return dict(entitled=True, mode="grace")
        return dict(entitled=False, mode="locked", reason="expired")

    # ---- PAYG ----
    def apply_entitlement(self, entitlement):
        d = _load(entitlement)
        need = ("device_id", "nonce", "issued_at", "expires_at", "sig")
        if not isinstance(d, dict) or not all(k in d for k in need): return _rej("malformed")
        body = {k: v for k, v in d.items() if k != "sig"}
        if not self.has("skip_signature") and not verify(self.key, body, d["sig"]): return _rej("bad_signature")
        if not isinstance(d["nonce"], str) or len(d["nonce"]) < 8: return _rej("invalid_nonce")
        if d["device_id"] != self.DEVICE_ID and not self.has("no_device_binding"): return _rej("device_mismatch")
        if not self._clock_ok(): return _rej("clock_rollback")
        n = self.now()
        if d["issued_at"] > n + self.SKEW: return _rej("not_yet_valid")
        if d["expires_at"] <= n and not self.has("accept_expired"): return _rej("expired")
        if d["nonce"] in self.nonces and not self.has("accept_replay"): return _rej("replay")
        self.nonces.add(d["nonce"]); self.active_until = d["expires_at"]; self.hw = max(self.hw, n)
        return dict(accepted=True, reason="ok")

    def factory_reset(self):
        self.active_until, self.offline, self.last_seq = None, False, 0
        if self.has("reset_clears_nonces"): self.nonces.clear()
        return dict(ok=True, wiped=True)

    # ---- update ----
    def apply_update(self, package, source="network", interrupt=False):
        if self.bricked: return _rej("device_bricked")
        d = _load(package)
        mk = ("model", "hardware", "version", "body_sha256")
        if not (isinstance(d, dict) and {"manifest", "body", "sig"} <= set(d)
                and isinstance(d["manifest"], dict) and all(k in d["manifest"] for k in mk)):
            return _rej("malformed")
        m = d["manifest"]
        if not self.has("skip_update_signature") and not verify(self.key, m, d["sig"]): return _rej("invalid_signature")
        try: body = bytes.fromhex(d["body"]); v = _ver(m["version"])
        except Exception: return _rej("malformed")
        if not self.has("skip_hash_check") and sha256_bytes(body) != m["body_sha256"]: return _rej("hash_mismatch")
        if (m["model"] != self.MODEL or m["hardware"] != self.HW) and not self.has("no_model_check"): return _rej("wrong_model")
        if v < self.fw and not self.has("allow_downgrade"): return _rej("downgrade_blocked")
        if interrupt:
            if self.has("no_ab_recovery"):
                self.bricked = True
                return _rej("interrupted", bootable=False, running_version=None)
            return _rej("interrupted", bootable=True, running_version=_vs(self.fw))
        self.prev_fw, self.fw = self.fw, v
        return dict(accepted=True, reason="ok", source=source, running_version=_vs(v))

    # ---- firmware ----
    def info(self):
        return dict(**self.identity(), fw_version=_vs(self.fw), rollback_enforced=not self.has("allow_downgrade"))
    def firmware_hash(self):
        return dict(sha256=sha256_bytes(f"fw-{_vs(self.fw)}".encode()), matches_reference=not self.has("fw_modified"))
    def installed_signature(self): return dict(valid=not self.has("fw_modified"))
    def secure_boot_state(self):
        on = not self.has("secure_boot_off"); return dict(enabled=on, locked=on)
    def verify_boot_chain(self):
        return dict(ok=not self.has("boot_chain_broken"), stages=["rom", "bootloader", "kernel", "rootfs"])
    def validate_version(self, version): return dict(supported=_ver(version) >= self.min_fw)
    def check_compatibility(self, model, hardware, region):
        return dict(compatible=model == self.MODEL and hardware == self.HW and region in self.REGIONS)

    # ---- recovery ----
    def boot(self, image="valid"):
        if self.bricked: return dict(booted=False, fallback=None)
        if image == "corrupt" and not self.has("boot_corrupt_ok"): return dict(booted=False, fallback="recovery")
        return dict(booted=True, running_version=_vs(self.fw))
    def rollback(self):
        if not self.prev_fw: return dict(rolled_back=False, reason="no_previous")
        self.fw, self.prev_fw = self.prev_fw, None
        return dict(rolled_back=True, running_version=_vs(self.fw))

    # ---- USB ----
    def usb_discover(self, files):
        return dict(candidates=[f for f in files if f.lower().endswith(".pkg") and not f.startswith(".")])

    # ---- network ----
    def tls_connect(self, scenario="valid"):
        if scenario == "valid" or self.has("accept_any_cert") or (scenario == "tls10" and self.has("allow_tls10")):
            return dict(accepted=True)
        return dict(accepted=False, reason=scenario)
    def api_call(self, credential="valid", endpoint="/diag/read", role="user"):
        if credential in ("none", "expired") and not self.has("no_auth"): return dict(status=401)
        if endpoint.startswith("/admin") and role != "admin" and not self.has("no_authz"): return dict(status=403)
        return dict(status=200)
    def send_message(self, seq, tamper=False):
        if tamper and not self.has("no_msg_mac"): return _rej("bad_mac")
        if seq <= self.last_seq and not self.has("no_msg_replay"): return _rej("replay")
        self.last_seq = max(self.last_seq, seq); return dict(accepted=True, reason="ok")

    # ---- local audits ----
    def storage_audit(self):
        return dict(world_readable_files=3 if self.has("world_readable") else 0,
                    unencrypted_sensitive_files=2 if self.has("plaintext_storage") else 0)
    def credential_storage_audit(self):
        return dict(plaintext_credentials=1 if self.has("plaintext_creds") else 0, hardcoded_credentials=0)
    def log_audit(self): return dict(sensitive_matches=5 if self.has("verbose_logs") else 0)
    def debug_probe(self):
        o = self.has("debug_open"); return dict(uart_shell_open=o, jtag_enabled=o, debug_port_listening=o)
    def diagnostic_access_probe(self): return dict(unauthenticated_access=self.has("diag_noauth"))
