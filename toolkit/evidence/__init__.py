"""EvidenceCollector: records each step with secrets redacted."""
from ..security.crypto import sha256_bytes

def summarize(v, redact):
    if isinstance(v, (bytes, bytearray)):
        return f"<{len(v)} bytes sha256={sha256_bytes(bytes(v))[:12]}>"
    if isinstance(v, dict):
        return {k: ("[redacted]" if k in redact else summarize(x, redact)) for k, x in v.items()}
    if isinstance(v, list): return [summarize(x, redact) for x in v]
    return v

class EvidenceCollector:
    def __init__(self, redact): self.redact, self.steps = set(redact), []
    def record(self, call, args, result, errors):
        self.steps.append(dict(call=call, args=summarize(args, self.redact),
                               result=summarize(result, self.redact), errors=errors))
