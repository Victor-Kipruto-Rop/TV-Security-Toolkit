import time

class Transport:
    """Byte pipe. Subclasses implement open/close/write/_recv_chunk."""
    def __init__(self): self._buf = b""
    def open(self): pass
    def close(self): pass
    def write(self, data: bytes): raise NotImplementedError
    def _recv_chunk(self, timeout: float) -> bytes: raise NotImplementedError
    def read_exact(self, n, timeout):
        end = time.time() + timeout
        while len(self._buf) < n:
            left = end - time.time()
            if left <= 0: raise TimeoutError(f"read timeout ({len(self._buf)}/{n} bytes)")
            self._buf += self._recv_chunk(left)
        out, self._buf = self._buf[:n], self._buf[n:]
        return out
    def flush_input(self): self._buf = b""

class UsbTransport(Transport):
    """Bulk-endpoint transport over libusb (pip install pyusb; libusb must be installed)."""
    def __init__(self, vid, pid, serial=None, interface=0, ep_out=0x01, ep_in=0x81, chunk=512):
        super().__init__()
        self.vid, self.pid, self.serial = vid, pid, serial
        self.interface, self.ep_out, self.ep_in, self.chunk, self.dev = interface, ep_out, ep_in, chunk, None

    def open(self):
        try: import usb.core, usb.util
        except ImportError: raise SystemExit("pyusb not installed: pip install pyusb (and install libusb)")
        match = lambda d: self.serial is None or usb.util.get_string(d, d.iSerialNumber) == self.serial
        self.dev = usb.core.find(idVendor=self.vid, idProduct=self.pid, custom_match=match)
        if self.dev is None: raise SystemExit(f"USB device {self.vid:04x}:{self.pid:04x} not found")
        try:
            if self.dev.is_kernel_driver_active(self.interface): self.dev.detach_kernel_driver(self.interface)
        except (NotImplementedError, usb.core.USBError): pass
        self.dev.set_configuration(); usb.util.claim_interface(self.dev, self.interface)

    def close(self):
        if self.dev is not None:
            import usb.util; usb.util.release_interface(self.dev, self.interface); usb.util.dispose_resources(self.dev); self.dev = None

    def write(self, data): self.dev.write(self.ep_out, data, 5000)

    def _recv_chunk(self, timeout):
        import usb.core
        try: return bytes(self.dev.read(self.ep_in, self.chunk, max(1, int(timeout * 1000))))
        except usb.core.USBError as e:
            if getattr(e, "errno", None) in (110, 60, 10060) or "timed out" in str(e).lower(): return b""
            raise

def list_usb():
    try: import usb.core, usb.util
    except ImportError: raise SystemExit("pyusb not installed: pip install pyusb (and install libusb)")
    out = []
    for d in usb.core.find(find_all=True):
        def s(i):
            try: return usb.util.get_string(d, i) if i else ""
            except Exception: return ""
        out.append(dict(vid=f"{d.idVendor:04x}", pid=f"{d.idProduct:04x}", manufacturer=s(d.iManufacturer),
                        product=s(d.iProduct), serial=s(d.iSerialNumber)))
    return out

class LoopbackTransport(Transport):
    """In-process 'USB' link to a ProtocolServer; answers in small chunks to exercise reassembly."""
    def __init__(self, server, chunk=16):
        super().__init__(); self.server, self.chunk, self.out = server, chunk, b""
    def write(self, data): self.out += self.server.handle(data)
    def _recv_chunk(self, timeout):
        c, self.out = self.out[: self.chunk], self.out[self.chunk:]; return c
