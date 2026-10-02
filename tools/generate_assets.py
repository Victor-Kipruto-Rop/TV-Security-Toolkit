#!/usr/bin/env python3
"""Regenerates config/, profiles/, schemas/ and tests/ JSON. Run from the toolkit root."""
import json, secrets
from pathlib import Path
R = Path(__file__).resolve().parent.parent

def w(rel, obj):
    p = R / rel; p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(obj, indent=2) + "\n")

# ---------------- config ----------------
key_file = R / "config/security.json"
key = json.loads(key_file.read_text())["lab_signing_key_hex"] if key_file.exists() else secrets.token_hex(32)
w("config/toolkit.json", {"name": "TV-Security-Toolkit", "default_profile": "development", "default_device": "sim"})
w("config/environment.json", {"payloads_dir": "payloads", "reports_dir": "reports", "evidence_dir": "evidence"})
w("config/logging.json", {"level": "INFO", "file": "logs/tests/toolkit.log", "format": "%(asctime)s %(levelname)s %(message)s"})
w("config/security.json", {"lab_signing_key_hex": key,
   "note": "LAB key for the simulated device only. Never place production signing keys here."})
w("config/device-policy.json", {"allow_state_changing": True})
w("config/test-policy.json", {"required_fields": ["title", "severity", "steps"], "max_steps_per_test": 20,
   "stop_on_critical_failure": False})
w("config/severity-rules.json", {"levels": ["info", "low", "medium", "high", "critical"], "fail_threshold": "medium"})
w("config/report-policy.json", {"formats": ["html", "json"], "redact_keys": ["sig", "key", "secret", "password", "mac"]})
ALL = ["firmware", "payg", "update", "network", "local", "usb", "recovery"]
profiles = {
    "development": (ALL, True), "staging": (ALL, True), "factory": (["firmware", "payg", "update", "usb", "recovery"], True),
    "production-readonly": (ALL, False)}
for name, (cats, mut) in profiles.items():
    w(f"profiles/{name}/profile.json", {"name": name, "description": f"{name} test profile"})
    w(f"profiles/{name}/allowed-tests.json", {"categories": cats, "deny": []})
    w(f"profiles/{name}/device-policy.json", {"allow_state_changing": mut})
w("app/application.config.json", {"entry": "launcher.py"})
w("app/manifests/capabilities.json", {"capabilities": ["run-tests", "generate-reports", "validate-packages"]})
w("firmware/manifests/firmware-schema.json", {"type": "object", "required": ["model", "hardware", "version", "body_sha256"]})
w("firmware/manifests/update-schema.json", {"type": "object", "required": ["manifest", "body", "sig"]})
w("drivers/usb/driver-manifest.json", {"drivers": []}); w("drivers/serial/driver-manifest.json", {"drivers": []})
for n, req in {"test": ["title", "severity", "steps"], "result": ["id", "status"], "finding": ["id", "severity", "message"],
               "device": ["device_id", "model"], "entitlement": ["device_id", "nonce", "issued_at", "expires_at", "sig"],
               "firmware": ["model", "hardware", "version"], "report": ["meta", "summary", "results"]}.items():
    w(f"schemas/{n}.schema.json", {"$schema": "http://json-schema.org/draft-07/schema#", "type": "object", "required": req})

# ---------------- tests ----------------
E = lambda **k: {"$entitlement": k}
P = lambda **k: {"$package": k}
def S(call, args=None, expect=None):
    s = {"call": call, "args": args or {}}
    if expect is not None: s["expect"] = expect
    return s
def rej(reason): return {"accepted": False, "reason": reason}
OK = {"accepted": True}
LOCKED = {"entitled": False, "mode": "locked"}

TESTS = {
"firmware/integrity/firmware-hash": ("Installed firmware hash matches approved reference", "critical", [S("firmware_hash", expect={"matches_reference": True})]),
"firmware/integrity/signature-validation": ("Installed firmware signature is valid", "critical", [S("installed_signature", expect={"valid": True})]),
"firmware/secure-boot/secure-boot-state": ("Secure boot enabled and locked", "critical", [S("secure_boot_state", expect={"enabled": True, "locked": True})]),
"firmware/secure-boot/boot-chain": ("Boot chain verifies end to end", "critical", [S("verify_boot_chain", expect={"ok": True})]),
"firmware/version/version-validation": ("Supported firmware version accepted", "medium", [S("validate_version", {"version": "1.4.0"}, {"supported": True})]),
"firmware/version/unsupported-version": ("Unsupported firmware version rejected", "medium", [S("validate_version", {"version": "0.9.0"}, {"supported": False})]),
"firmware/rollback/rollback-protection": ("Rollback protection is enforced", "high", [S("info", expect={"rollback_enforced": True}), S("apply_update", {"package": P(version="1.2.0")}, rej("downgrade_blocked"))]),
"firmware/compatibility/model-validation": ("Model compatibility check", "medium", [S("check_compatibility", {"model": "SIM-TV-55", "hardware": "rev-B", "region": "KE"}, {"compatible": True}), S("check_compatibility", {"model": "OTHER-TV", "hardware": "rev-B", "region": "KE"}, {"compatible": False})]),
"firmware/compatibility/hardware-validation": ("Hardware revision compatibility check", "medium", [S("check_compatibility", {"model": "SIM-TV-55", "hardware": "rev-Z", "region": "KE"}, {"compatible": False})]),
"firmware/compatibility/region-validation": ("Region compatibility check", "medium", [S("check_compatibility", {"model": "SIM-TV-55", "hardware": "rev-B", "region": "XX"}, {"compatible": False})]),

"payg/entitlement/valid-entitlement": ("Valid signed entitlement accepted", "high", [S("apply_entitlement", {"entitlement": "@valid/valid-entitlement.json"}, OK), S("state", expect={"entitled": True, "mode": "active"})]),
"payg/entitlement/invalid-entitlement": ("Entitlement with bad signature rejected", "critical", [S("apply_entitlement", {"entitlement": "@invalid/invalid-entitlement.json"}, rej("bad_signature")), S("state", expect=LOCKED)]),
"payg/entitlement/malformed-entitlement": ("Malformed entitlement rejected", "high", [S("apply_entitlement", {"entitlement": {"$bytes": "{not json"}}, rej("malformed")), S("apply_entitlement", {"entitlement": E(omit=["expires_at"])}, rej("malformed"))]),
"payg/expiration/expired-entitlement": ("Expired entitlement rejected", "critical", [S("apply_entitlement", {"entitlement": "@expired/expired-entitlement.json"}, rej("expired")), S("state", expect=LOCKED)]),
"payg/expiration/future-entitlement": ("Entitlement issued in the future rejected", "high", [S("apply_entitlement", {"entitlement": E(issued=3600, expires=90000, nonce="nonce-future-1")}, rej("not_yet_valid"))]),
"payg/expiration/expiration-boundary": ("Entitlement lapses exactly at expiry", "high", [S("apply_entitlement", {"entitlement": E(expires=60, nonce="nonce-bound-1")}, OK), S("advance", {"seconds": 59}), S("state", expect={"entitled": True}), S("advance", {"seconds": 1}), S("state", expect={"entitled": False})]),
"payg/replay/replay-protection": ("Replayed entitlement rejected", "critical", [S("apply_entitlement", {"entitlement": E(nonce="nonce-replay-1")}, OK), S("apply_entitlement", {"entitlement": E(nonce="nonce-replay-1")}, rej("replay"))]),
"payg/replay/nonce-validation": ("Empty/short nonce rejected", "high", [S("apply_entitlement", {"entitlement": E(nonce="")}, rej("invalid_nonce"))]),
"payg/device-binding/device-mismatch": ("Entitlement for another device rejected", "critical", [S("apply_entitlement", {"entitlement": E(device="OTHER-9999", nonce="nonce-other-1")}, rej("device_mismatch"))]),
"payg/device-binding/device-binding": ("Entitlement bound to this device accepted", "high", [S("apply_entitlement", {"entitlement": E(nonce="nonce-bind-01")}, OK)]),
"payg/clock/time-validation": ("Device locks after entitlement time passes", "high", [S("apply_entitlement", {"entitlement": E(expires=3600, nonce="nonce-time-01")}, OK), S("advance", {"seconds": 7200}), S("state", expect=LOCKED)]),
"payg/clock/time-integrity": ("Clock rollback does not re-extend entitlement", "critical", [S("apply_entitlement", {"entitlement": E(expires=86400, nonce="nonce-clock-01")}, OK), S("advance", {"seconds": 43200}), S("set_clock", {"delta": -43200}), S("state", expect={"entitled": False, "reason": "clock_tamper"})]),
"payg/offline/offline-grace": ("Offline grace period is bounded", "high", [S("apply_entitlement", {"entitlement": E(expires=60, nonce="nonce-offl-01")}, OK), S("go_offline"), S("advance", {"seconds": 120}), S("state", expect={"entitled": True, "mode": "grace"}), S("advance", {"seconds": 259200}), S("state", expect=LOCKED)]),
"payg/offline/reconnect": ("Reconnect after lapse needs fresh entitlement", "medium", [S("apply_entitlement", {"entitlement": E(expires=60, nonce="nonce-reco-01")}, OK), S("go_offline"), S("advance", {"seconds": 400000}), S("reconnect", expect={"state": LOCKED}), S("apply_entitlement", {"entitlement": E(nonce="nonce-reco-02")}, OK), S("state", expect={"entitled": True})]),
"payg/factory-reset/reset-security": ("Factory reset keeps replay protection", "critical", [S("apply_entitlement", {"entitlement": E(nonce="nonce-reset-1")}, OK), S("factory_reset", expect={"ok": True}), S("state", expect=LOCKED), S("apply_entitlement", {"entitlement": E(nonce="nonce-reset-1")}, rej("replay"))]),
"payg/factory-reset/state-recovery": ("Device accepts fresh entitlement after reset", "medium", [S("factory_reset", expect={"ok": True}), S("apply_entitlement", {"entitlement": E(nonce="nonce-recov-1")}, OK), S("state", expect={"entitled": True})]),

"update/valid/valid-update": ("Valid signed update installs", "high", [S("apply_update", {"package": "@valid/valid-update.pkg"}, {"accepted": True, "running_version": "1.5.0"})]),
"update/integrity/corrupted-package": ("Truncated package rejected", "high", [S("apply_update", {"package": "@corrupted/corrupted-manifest.json"}, rej("malformed")), S("info", expect={"fw_version": "1.4.0"})]),
"update/integrity/hash-mismatch": ("Package with altered body rejected", "critical", [S("apply_update", {"package": "@corrupted/corrupted-firmware.pkg"}, rej("hash_mismatch"))]),
"update/signature/invalid-signature": ("Package with bad signature rejected", "critical", [S("apply_update", {"package": "@invalid/invalid-signature.pkg"}, rej("invalid_signature")), S("info", expect={"fw_version": "1.4.0"})]),
"update/downgrade/downgrade-protection": ("Signed downgrade rejected", "high", [S("apply_update", {"package": P(version="1.3.0")}, rej("downgrade_blocked"))]),
"update/compatibility/wrong-model-update": ("Update for other model/hardware rejected", "high", [S("apply_update", {"package": "@incompatible/wrong-model.pkg"}, rej("wrong_model")), S("apply_update", {"package": "@incompatible/wrong-hardware.pkg"}, rej("wrong_model"))]),
"update/recovery/interrupted-update": ("Interrupted update leaves device bootable", "critical", [S("apply_update", {"package": P(), "interrupt": True}, {"accepted": False, "reason": "interrupted", "bootable": True, "running_version": "1.4.0"})]),
"update/recovery/failed-update-recovery": ("Device recovers after failed update", "high", [S("apply_update", {"package": "@corrupted/corrupted-firmware.pkg"}, rej("hash_mismatch")), S("boot", expect={"booted": True, "running_version": "1.4.0"}), S("apply_update", {"package": "@valid/valid-update.pkg"}, OK)]),

"network/tls/tls-validation": ("TLS rejects wrong host and legacy protocol", "high", [S("tls_connect", {"scenario": "valid"}, {"accepted": True}), S("tls_connect", {"scenario": "wrong_host"}, {"accepted": False}), S("tls_connect", {"scenario": "tls10"}, {"accepted": False})]),
"network/tls/certificate-validation": ("TLS rejects expired/self-signed/revoked certs", "critical", [S("tls_connect", {"scenario": s}, {"accepted": False}) for s in ("expired_cert", "self_signed", "revoked")]),
"network/authentication/authentication": ("Unauthenticated API call rejected", "critical", [S("api_call", {"credential": "valid"}, {"status": 200}), S("api_call", {"credential": "none"}, {"status": 401})]),
"network/authentication/expired-credential": ("Expired credential rejected", "high", [S("api_call", {"credential": "expired"}, {"status": 401})]),
"network/authorization/authorization": ("Admin endpoint denied to normal user", "critical", [S("api_call", {"endpoint": "/admin/config", "role": "user"}, {"status": 403}), S("api_call", {"endpoint": "/admin/config", "role": "admin"}, {"status": 200})]),
"network/authorization/privilege-boundary": ("Technician cannot factory-reset via API", "high", [S("api_call", {"endpoint": "/admin/factory-reset", "role": "technician"}, {"status": 403})]),
"network/replay/message-replay": ("Replayed protocol message rejected", "high", [S("send_message", {"seq": 1}, OK), S("send_message", {"seq": 1}, rej("replay"))]),
"network/integrity/message-integrity": ("Tampered protocol message rejected", "critical", [S("send_message", {"seq": 5, "tamper": True}, rej("bad_mac"))]),

"local/storage/sensitive-storage": ("Sensitive data stored encrypted", "high", [S("storage_audit", expect={"unencrypted_sensitive_files": 0})]),
"local/storage/permission": ("No world-readable sensitive files", "medium", [S("storage_audit", expect={"world_readable_files": 0})]),
"local/credentials/credential-storage": ("No plaintext or hardcoded credentials", "high", [S("credential_storage_audit", expect={"plaintext_credentials": 0, "hardcoded_credentials": 0})]),
"local/logs/sensitive-log-data": ("Logs contain no sensitive data", "medium", [S("log_audit", expect={"sensitive_matches": 0})]),
"local/debug/debug-interface": ("Debug interfaces disabled in production", "high", [S("debug_probe", expect={"uart_shell_open": False, "jtag_enabled": False, "debug_port_listening": False})]),
"local/debug/diagnostic-access": ("Diagnostics require authentication", "high", [S("diagnostic_access_probe", expect={"unauthenticated_access": False})]),

"usb/discovery/package-discovery": ("USB scan only offers .pkg candidates", "low", [S("usb_discover", {"files": ["update.pkg", "notes.txt", ".hidden.pkg", "x.pkg.tmp"]}, {"candidates": ["update.pkg"]})]),
"usb/integrity/usb-integrity": ("Altered USB package rejected", "high", [S("apply_update", {"package": "@corrupted/corrupted-firmware.pkg", "source": "usb"}, rej("hash_mismatch"))]),
"usb/signature/usb-signature": ("Unsigned USB package rejected", "critical", [S("apply_update", {"package": "@invalid/invalid-signature.pkg", "source": "usb"}, rej("invalid_signature"))]),
"usb/malformed/malformed-package": ("Malformed USB package rejected", "medium", [S("apply_update", {"package": "@invalid/invalid-manifest.json", "source": "usb"}, rej("malformed")), S("apply_update", {"package": {"$bytes": "garbage\x00\xff"}, "source": "usb"}, rej("malformed"))]),

"recovery/boot/boot-validation": ("Corrupt boot image falls back to recovery", "critical", [S("boot", {"image": "corrupt"}, {"booted": False, "fallback": "recovery"}), S("boot", {"image": "valid"}, {"booted": True})]),
"recovery/rollback/rollback": ("Rollback restores previous firmware", "medium", [S("apply_update", {"package": "@valid/valid-update.pkg"}, OK), S("rollback", expect={"rolled_back": True, "running_version": "1.4.0"})]),
"recovery/factory/factory-reset": ("Factory reset wipes data and stays locked", "high", [S("apply_entitlement", {"entitlement": E(nonce="nonce-frst-01")}, OK), S("factory_reset", expect={"ok": True, "wiped": True}), S("state", expect=LOCKED)]),
}
for path, (title, sev, steps) in TESTS.items():
    w(f"tests/{path}.test.json", {"title": title, "severity": sev, "steps": steps})
print(f"generated {len(TESTS)} tests")
