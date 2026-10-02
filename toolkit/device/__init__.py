import json
from pathlib import Path
from .sim import SimulatedTvAdapter
from .generic import GenericTvAdapter
from .protocol import ProtocolServer
from .transport import UsbTransport, LoopbackTransport
from .usb_adapter import UsbTvAdapter

def make_device(name, cfg, flaws=(), usb=None):
    key = bytes.fromhex(cfg.security["lab_signing_key_hex"])
    if name == "sim":
        return SimulatedTvAdapter(key, flaws)
    if name == "usb-loopback":  # full USB protocol path against the simulator, no hardware
        return UsbTvAdapter(LoopbackTransport(ProtocolServer(SimulatedTvAdapter(key, flaws))))
    if name == "usb":
        c = {**json.loads((cfg.root / "device/discovery/discovery.config.json").read_text()),
             **{k: v for k, v in (usb or {}).items() if v is not None}}
        if c.get("vid") is None or c.get("pid") is None:
            raise SystemExit("USB device not configured: pass --vid/--pid or edit device/discovery/discovery.config.json")
        return UsbTvAdapter(UsbTransport(c["vid"], c["pid"], c.get("serial"), c["interface"], c["ep_out"], c["ep_in"]),
                            c.get("timeout_s", 5.0))
    if name == "generic":
        return GenericTvAdapter()
    raise SystemExit(f"unknown device adapter: {name}")
