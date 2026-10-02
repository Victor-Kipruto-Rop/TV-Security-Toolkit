import argparse, json, logging, sys
from pathlib import Path
from . import VERSION
from .config import Config
from .device import make_device, SimulatedTvAdapter
from .engine import Runner, load_tests
from .formats import build_entitlement, build_package
from .reporting import write_reports, summarize, exit_code
from .security import PolicyEngine

ROOT = Path(__file__).resolve().parent.parent

def _filter(tests, a):
    if a.category: tests = [t for t in tests if t["category"] in a.category.split(",")]
    if a.test: tests = [t for t in tests if a.test in t["id"]]
    return tests

def _hex(v): return None if v is None else int(v, 0)

def cmd_devices(a):
    from .device.transport import list_usb
    for d in list_usb(): print(f"{d['vid']}:{d['pid']}  {d['manufacturer']} {d['product']}  serial={d['serial']}")

def cmd_list(a):
    cfg = Config(ROOT, a.profile); pol = PolicyEngine(cfg)
    tests = _filter(load_tests(ROOT / "tests"), a)
    if (a.category or a.test) and not tests:
        print("error: no tests matched the requested filter", file=sys.stderr)
        return 2
    for t in tests:
        ok, why = pol.decide(t)
        print(f"{'run ' if ok else 'skip'} [{t['severity']:8}] {t['id']}" + ("" if ok else f"  ({why})"))

def cmd_run(a):
    cfg = Config(ROOT, a.profile)
    lc = cfg.logging
    logging.basicConfig(filename=ROOT / lc["file"], level=lc["level"], format=lc["format"])
    flaws = [f for f in (a.flaw or "").split(",") if f]
    usb = dict(vid=_hex(a.vid), pid=_hex(a.pid), serial=a.serial)
    dev = make_device(a.device or cfg.toolkit.get("default_device", "sim"), cfg, flaws, usb)
    required_fields = cfg.test_policy.get("required_fields", ("title", "severity", "steps"))
    tests = _filter(load_tests(ROOT / "tests", required_fields), a)
    if (a.category or a.test) and not tests:
        print("error: no tests matched the requested filter", file=sys.stderr)
        return 2
    results = Runner(cfg, dev, PolicyEngine(cfg)).run(tests)
    meta = dict(toolkit=VERSION, profile=cfg.profile_name, device=dev.name, flaws=flaws)
    paths = write_reports(results, meta, ROOT, (a.report or ",".join(cfg.report["formats"])).split(","))
    s = summarize(results)
    for r in results:
        if r["status"] in ("fail", "error"): print(f"{r['status'].upper():5} [{r['severity']}] {r['id']}\n      {r['message']}")
    print(f"\n{s['pass']} passed, {s['fail']} failed, {s['error']} errors, {s['skipped']} skipped (of {s['total']})")
    for p in paths: print("report:", p.relative_to(ROOT))
    fail_threshold = cfg.severity.get("fail_threshold", cfg.severity.get("failThreshold", "medium"))
    return exit_code(results, fail_threshold.lower())

def cmd_gen_payloads(a):
    cfg = Config(ROOT); key = bytes.fromhex(cfg.security["lab_signing_key_hex"])
    S, T0 = SimulatedTvAdapter, SimulatedTvAdapter.T0
    P = ROOT / "payloads"
    def wj(rel, obj): (P / rel).write_text(json.dumps(obj, indent=2))
    def wb(rel, b): (P / rel).write_bytes(b)
    wj("valid/valid-entitlement.json", build_entitlement(key, S.DEVICE_ID, T0 - 3600, T0 + 2592000, "payload-valid-0001"))
    wb("valid/valid-update.pkg", build_package(key, S.MODEL, S.HW, "1.5.0"))
    wj("invalid/invalid-entitlement.json", build_entitlement(key, S.DEVICE_ID, T0 - 3600, T0 + 2592000, "payload-bad-0001", tamper=True))
    wb("invalid/invalid-signature.pkg", build_package(key, S.MODEL, S.HW, "1.5.0", tamper="signature"))
    wj("invalid/invalid-manifest.json", {"manifest": {"model": S.MODEL}, "body": "00", "sig": "00"})
    wb("corrupted/corrupted-firmware.pkg", build_package(key, S.MODEL, S.HW, "1.5.0", tamper="hash"))
    wb("corrupted/corrupted-manifest.json", build_package(key, S.MODEL, S.HW, "1.5.0", tamper="truncate"))
    wj("expired/expired-entitlement.json", build_entitlement(key, S.DEVICE_ID, T0 - 90 * 86400, T0 - 60, "payload-expired-0001"))
    wb("incompatible/wrong-model.pkg", build_package(key, "OTHER-TV-32", S.HW, "1.5.0"))
    wb("incompatible/wrong-hardware.pkg", build_package(key, S.MODEL, "rev-Z", "1.5.0"))
    print("payloads written")

def cmd_validate_pkg(a):
    cfg = Config(ROOT); dev = make_device("sim", cfg)
    r = dev.apply_update(Path(a.file).read_bytes()); print(json.dumps(r)); return 0 if r["accepted"] else 1

def main(argv=None):
    ap = argparse.ArgumentParser(prog="TV-Security-Toolkit", description=f"TV Security Toolkit {VERSION}")
    sub = ap.add_subparsers(dest="cmd", required=True)
    def common(p):
        p.add_argument("--profile"); p.add_argument("--category"); p.add_argument("--test")
    p = sub.add_parser("list"); common(p); p.set_defaults(fn=cmd_list)
    p = sub.add_parser("run"); common(p)
    p.add_argument("--device", help="sim | usb | usb-loopback | generic")
    p.add_argument("--vid"); p.add_argument("--pid"); p.add_argument("--serial"); p.add_argument("--flaw", help="comma list (sim only)")
    p.add_argument("--report", help="html,json"); p.set_defaults(fn=cmd_run)
    sub.add_parser("devices", help="list attached USB devices").set_defaults(fn=cmd_devices)
    sub.add_parser("gen-payloads").set_defaults(fn=cmd_gen_payloads)
    p = sub.add_parser("validate-package"); p.add_argument("file"); p.set_defaults(fn=cmd_validate_pkg)
    a = ap.parse_args(argv); return a.fn(a) or 0
