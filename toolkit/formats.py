"""Builders for lab entitlement documents and update packages."""
import json
from .security.crypto import sign, sha256_bytes

def _flip(sig): return ("0" if sig[0] != "0" else "1") + sig[1:]

def build_entitlement(key, device_id, issued, expires, nonce, tamper=False, omit=()):
    body = {"device_id": device_id, "nonce": nonce, "issued_at": issued, "expires_at": expires}
    sig = sign(key, body)
    doc = dict(body, sig=_flip(sig) if tamper else sig)
    for f in omit: doc.pop(f, None)
    return doc

def build_package(key, model, hardware, version, body=b"firmware-image", tamper=None):
    m = {"model": model, "hardware": hardware, "version": version, "body_sha256": sha256_bytes(body)}
    sig, hexbody = sign(key, m), body.hex()
    if tamper == "signature": sig = _flip(sig)
    if tamper == "hash": hexbody = (body + b"\x00corrupt").hex()
    raw = json.dumps({"manifest": m, "body": hexbody, "sig": sig}).encode()
    if tamper == "truncate": raw = raw[: len(raw) // 2]
    return raw
