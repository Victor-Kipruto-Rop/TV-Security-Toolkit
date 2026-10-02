from .base import DeviceAdapter

class GenericTvAdapter(DeviceAdapter):
    """Skeleton for real hardware. Implement identity(), now() and the methods
    used by your tests over USB/serial/network transport."""
    name = "generic"
    def __init__(self, *a, **k):
        raise NotImplementedError(
            "GenericTvAdapter is a skeleton: implement the transport in toolkit/device/generic.py")
