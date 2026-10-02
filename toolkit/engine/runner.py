"""TestEngine + TestRunner + AssertionEngine + ResultCollector in one module."""
import json, time, logging
from pathlib import Path
from ..formats import build_entitlement, build_package
from ..evidence import EvidenceCollector

log = logging.getLogger("toolkit")

# ---------- loading ----------
def load_tests(tests_dir, required=("title", "severity", "steps")):
    tests_dir = Path(tests_dir); out = []
    for p in sorted(tests_dir.rglob("*.test.json")):
        t = json.loads(p.read_text())
        miss = [k for k in required if k not in t]
        if miss: raise SystemExit(f"{p}: missing fields {miss}")
        rel = p.relative_to(tests_dir).with_suffix("").with_suffix("")
        t["id"] = ".".join(rel.parts); t["category"] = rel.parts[0]
        out.append(t)
    return out

# ---------- assertions ----------
OPS = {"in", "ge", "le", "not"}
def check(actual, exp):
    """Return list of error strings; empty means match (subset semantics)."""
    if isinstance(exp, dict):
        if exp and set(exp) <= OPS:
            e = []
            if "in" in exp and actual not in exp["in"]: e.append(f"expected one of {exp['in']}, got {actual!r}")
            if "ge" in exp and not actual >= exp["ge"]: e.append(f"expected >= {exp['ge']}, got {actual!r}")
            if "le" in exp and not actual <= exp["le"]: e.append(f"expected <= {exp['le']}, got {actual!r}")
            if "not" in exp and actual == exp["not"]: e.append(f"must not equal {exp['not']!r}")
            return e
        if not isinstance(actual, dict): return [f"expected object, got {actual!r}"]
        errs = []
        for k, v in exp.items():
            if k not in actual: errs.append(f"missing '{k}'"); continue
            errs += [f"{k}: {m}" for m in check(actual[k], v)]
        return errs
    return [] if actual == exp else [f"expected {exp!r}, got {actual!r}"]

# ---------- argument resolution ----------
def resolve(v, dev, ctx):
    if isinstance(v, str) and v.startswith("@"):
        return (ctx["payloads"] / v[1:]).read_bytes()
    if isinstance(v, list): return [resolve(x, dev, ctx) for x in v]
    if isinstance(v, dict):
        ident = dev.identity()
        if "$entitlement" in v:
            s, now = v["$entitlement"], dev.now()
            did = ident["device_id"] if s.get("device", "self") == "self" else s["device"]
            return build_entitlement(ctx["key"], did, now + s.get("issued", -3600),
                                     now + s.get("expires", 2592000), s.get("nonce", "nonce-default"),
                                     s.get("tamper", False), s.get("omit", ()))
        if "$package" in v:
            s = v["$package"]
            g = lambda k: ident[k if k != "hardware" else "hardware"] if s.get(k, "self") == "self" else s[k]
            return build_package(ctx["key"], g("model"), g("hardware"), s.get("version", "1.5.0"),
                                 tamper=s.get("tamper"))
        if "$bytes" in v: return v["$bytes"].encode()
        return {k: resolve(x, dev, ctx) for k, x in v.items()}
    return v

# ---------- runner ----------
class Runner:
    def __init__(self, cfg, device, policy):
        self.cfg, self.dev, self.policy = cfg, device, policy
        self.ctx = {"payloads": cfg.root / "payloads", "key": bytes.fromhex(cfg.lab_key_hex)}

    def run(self, tests):
        results = []
        for t in tests:
            ok, why = self.policy.decide(t)
            base = dict(id=t["id"], category=t["category"], title=t["title"], severity=t["severity"])
            if not ok:
                results.append(dict(base, status="skipped", message=why, steps=[], duration_ms=0)); continue
            r = self._run_one(t, base); results.append(r)
            log.info("%s %s", r["status"].upper(), r["id"])
            stop_on_critical_failure = self.cfg.test_policy.get(
                "stop_on_critical_failure", self.cfg.test_policy.get("stopOnCriticalFailure", False)
            )
            if r["status"] == "fail" and t["severity"] == "critical" and stop_on_critical_failure:
                break
        return results

    def _run_one(self, t, base):
        ev = EvidenceCollector(self.cfg.report.get("redact_keys", []))
        start, status, msg = time.time(), "pass", ""
        try:
            self.dev.provision()
            for i, step in enumerate(t["steps"][: self.cfg.test_policy.get("max_steps_per_test", 20)]):
                args = resolve(step.get("args", {}), self.dev, self.ctx)
                res = getattr(self.dev, step["call"])(**args)
                errs = check(res, step["expect"]) if "expect" in step else []
                ev.record(step["call"], args, res, errs)
                if errs:
                    status, msg = "fail", f"step {i+1} ({step['call']}): " + "; ".join(errs); break
        except Exception as e:
            status, msg = "error", f"{type(e).__name__}: {e}"
            log.exception("error in %s", t["id"])
        return dict(base, status=status, message=msg, steps=ev.steps,
                    description=t.get("description", ""), duration_ms=int((time.time() - start) * 1000))
