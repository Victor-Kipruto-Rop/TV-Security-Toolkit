"""Signature/hash helpers (SignatureVerifier + HashVerifier roles).

The lab format uses HMAC-SHA256 as a stand-in for the vendor's asymmetric
signature so the toolkit runs on the stdlib only. Real-device adapters
should verify against the vendor PUBLIC key instead.
"""
import hashlib, hmac, json

def canonical(obj) -> bytes:
    return json.dumps(obj, sort_keys=True, separators=(",", ":")).encode()

def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def sign(key: bytes, obj) -> str:
    return hmac.new(key, canonical(obj), hashlib.sha256).hexdigest()

def verify(key: bytes, obj, sig) -> bool:
    return isinstance(sig, str) and hmac.compare_digest(sign(key, obj), sig)
