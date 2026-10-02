import json, html, datetime
from pathlib import Path
from string import Template

LEVELS = ["info", "low", "medium", "high", "critical"]

def summarize(results):
    s = {k: 0 for k in ("pass", "fail", "error", "skipped")}
    for r in results: s[r["status"]] += 1
    s["total"] = len(results); return s

def exit_code(results, threshold):
    if any(r["status"] == "error" for r in results): return 2
    t = LEVELS.index(threshold)
    return 1 if any(r["status"] == "fail" and LEVELS.index(r["severity"]) >= t for r in results) else 0

def write_reports(results, meta, root, formats):
    root, stamp, paths = Path(root), datetime.datetime.now().strftime("%Y%m%d-%H%M%S"), []
    summary = summarize(results)
    if "json" in formats:
        p = root / "reports/json" / f"run-{stamp}.json"
        p.write_text(json.dumps(dict(meta=meta, summary=summary, results=results), indent=2)); paths.append(p)
    if "html" in formats:
        t = root / "toolkit/reporting/templates"
        load = lambda n: Template((t / n).read_text())
        esc = html.escape
        rows = "".join(load("finding.html").safe_substitute(
            id=esc(r["id"]), title=esc(r["title"]), severity=r["severity"], status=r["status"],
            message=esc(r["message"])) for r in results if r["status"] in ("fail", "error"))
        summ = load("summary.html").safe_substitute(**summary)
        body = load("report.html").safe_substitute(
            title="TV Security Toolkit report", summary=summ, findings=rows or "<p>No findings.</p>",
            meta=esc(json.dumps(meta)))
        p = root / "reports/html" / f"run-{stamp}.html"; p.write_text(body); paths.append(p)
    return paths
