import itertools, logging
from .base import DeviceAdapter
from .protocol import COMMANDS, RemoteError, ProtocolError, encode_frame, read_frame

log = logging.getLogger("toolkit")

class UsbTvAdapter(DeviceAdapter):
    """Real-device adapter: every test call becomes one TVSP request over the transport."""
    name = "usb"
    def __init__(self, transport, timeout=5.0):
        self.t, self.timeout, self._ids, self._ident = transport, timeout, itertools.count(1), None
        self.t.open()

    def rpc(self, cmd, args=None):
        rid = next(self._ids)
        self.t.flush_input()
        self.t.write(encode_frame({"id": rid, "cmd": cmd, "args": args or {}}))
        resp = read_frame(self.t, self.timeout)
        if resp.get("id") != rid: raise ProtocolError(f"response id {resp.get('id')} != request id {rid}")
        if not resp.get("ok"): raise RemoteError(resp.get("error", "unknown device error"))
        return resp["result"]

    def provision(self):
        try: self.rpc("provision")
        except RemoteError as e: log.warning("device has no provision/test-reset command: %s", e)
    def identity(self):
        if self._ident is None: self._ident = self.rpc("identity")
        return self._ident
    def now(self):
        r = self.rpc("now"); return r["now"] if isinstance(r, dict) else r
    def close(self): self.t.close()

    def __getattr__(self, name):
        if name in COMMANDS: return lambda **kw: self.rpc(name, kw)
        raise AttributeError(name)
