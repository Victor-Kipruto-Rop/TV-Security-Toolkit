class DeviceAdapter:
    """Contract every device adapter implements. Each test 'call' maps to a
    method returning a dict. See docs/protocol-reference.md."""
    name = "base"
    def provision(self): """Return device to a known test state."""
    def close(self): pass
    def identity(self): raise NotImplementedError
    def now(self): raise NotImplementedError
