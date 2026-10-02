"""TVSP framing: b'TVSP' | ver u8 | len u32 | JSON payload | crc32 u32 (big-endian).
Request  {"id": n, "cmd": str, "args": {...}}
Response {"id": n, "ok": bool, "result": {...}, "error": str}
bytes values travel as {"$b64": "..."}. Adapt encode/decode if your device differs."""
import base64, json, struct, zlib

MAGIC, VERSION, MAX_LEN = b"TVSP", 1, 1 << 20
HDR = struct.Struct(">4sBI")
COMMANDS = {
    "provision", "identity", "now", "apply_entitlement", "state", "advance", "set_clock", "go_offline",
    "reconnect", "factory_reset", "apply_update", "info", "firmware_hash", "installed_signature",
    "secure_boot_state", "verify_boot_chain", "validate_version", "check_compatibility", "boot",
    "rollback", "usb_discover", "tls_connect", "api_call", "send_message", "storage_audit",
    "credential_storage_audit", "log_audit", "debug_probe", "diagnostic_access_probe"}

class ProtocolError(Exception): pass
class RemoteError(Exception): pass

def _default(o):
    if isinstance(o, (bytes, bytearray)): return {"$b64": base64.b64encode(bytes(o)).decode()}
    raise TypeError(type(o))
def _hook(d): return base64.b64decode(d["$b64"]) if set(d) == {"$b64"} else d

def encode_frame(obj) -> bytes:
    p = json.dumps(obj, default=_default).encode()
    if len(p) > MAX_LEN: raise ProtocolError("frame too large")
    return HDR.pack(MAGIC, VERSION, len(p)) + p + struct.pack(">I", zlib.crc32(p))

def parse_header(h: bytes) -> int:
    magic, ver, n = HDR.unpack(h)
    if magic != MAGIC: raise ProtocolError("bad magic")
    if ver != VERSION: raise ProtocolError(f"unsupported protocol version {ver}")
    if n > MAX_LEN: raise ProtocolError("declared length too large")
    return n

def parse_body(p: bytes, crc: bytes):
    if struct.unpack(">I", crc)[0] != zlib.crc32(p): raise ProtocolError("crc mismatch")
    return json.loads(p, object_hook=_hook)

def decode_frame(raw: bytes):
    n = parse_header(raw[:HDR.size])
    if len(raw) != HDR.size + n + 4: raise ProtocolError("length mismatch")
    return parse_body(raw[HDR.size:HDR.size + n], raw[-4:])

def read_frame(transport, timeout):
    n = parse_header(transport.read_exact(HDR.size, timeout))
    return parse_body(transport.read_exact(n, timeout), transport.read_exact(4, timeout))

class ProtocolServer:
    """Device-side endpoint wrapping any adapter; used by LoopbackTransport."""
    def __init__(self, device): self.dev = device
    def handle(self, raw: bytes) -> bytes:
        try:
            req = decode_frame(raw)
            cmd = req["cmd"]
            if cmd not in COMMANDS: raise ProtocolError(f"unknown command {cmd}")
            res = getattr(self.dev, cmd)(**req.get("args", {}))
            if cmd == "now": res = {"now": res}
            return encode_frame({"id": req["id"], "ok": True, "result": res})
        except Exception as e:
            return encode_frame({"id": locals().get("req", {}).get("id"), "ok": False, "error": f"{type(e).__name__}: {e}"})
