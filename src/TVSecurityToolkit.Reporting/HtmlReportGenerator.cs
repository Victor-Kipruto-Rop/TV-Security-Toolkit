using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using static TVSecurityToolkit.Reporting.ReportTemplateEngine;

namespace TVSecurityToolkit.Reporting;

public sealed class HtmlReportGenerator : IReportGenerator
{
    private readonly string _templateDir;
    public HtmlReportGenerator(string? templateDir = null) =>
        _templateDir = templateDir ?? Path.Combine(AppContext.BaseDirectory, "Templates");
    public string Format => "html";

    public async Task<string> GenerateAsync(SecurityReport report, string outputDir, CancellationToken ct)
    {
        string T(string n) => File.ReadAllText(Path.Combine(_templateDir, n));
        var findings = string.Concat(report.Findings.Select(f => Render(T("finding.html"), new Dictionary<string, string>
        {
            ["severity"] = f.Severity.ToString().ToLowerInvariant(), ["status"] = f.Status.ToString(),
            ["title"] = Html(f.Title), ["id"] = Html(f.TestId), ["message"] = Html(f.Message),
            ["findingId"] = Html(f.Id), ["component"] = Html(f.AffectedComponent),
            ["expected"] = Html(f.ExpectedBehavior), ["actual"] = Html(f.ActualBehavior),
            ["recommendation"] = Html(f.Recommendation), ["device"] = Html(f.DeviceName),
            ["firmware"] = Html(f.FirmwareVersion), ["observed"] = Html(f.ObservedUtc.ToString("u")),
            ["evidence"] = Html(string.Join(", ", f.EvidenceCalls))
        })));
        var severitySummary = string.Join(" | ", Enum.GetValues<TVSecurityToolkit.Core.Enums.Severity>()
            .Select(severity => $"{severity}: {report.Findings.Count(f => f.Severity == severity)}"));
        var summary = Render(T("summary.html"), new Dictionary<string, string>
        {
            ["total"] = report.Total.ToString(), ["passed"] = report.Passed.ToString(), ["failed"] = report.Failed.ToString(),
            ["errors"] = report.Errors.ToString(), ["skipped"] = report.Skipped.ToString(),
            ["cancelled"] = report.Cancelled.ToString(),
            ["severitySummary"] = Html(severitySummary)
        });
        var resultRows = string.Concat(report.Session.Results.Select(r =>
            $"<tr><td>{Html(r.Status.ToString())}</td><td>{Html(r.Severity.ToString())}</td><td>{Html(r.Id)}</td>" +
            $"<td>{Html(r.Title)}</td><td>{Html(r.DurationMs.ToString())}</td><td>{Html(r.Message)}</td>" +
            $"<td>{Html(string.Join(", ", r.Evidence.Select(e => e.Call)))}</td></tr>"));
        var results = resultRows.Length == 0
            ? "<p>No tests were executed.</p>"
            : "<table><thead><tr><th>Status</th><th>Severity</th><th>Test ID</th><th>Title</th><th>Duration (ms)</th><th>Details</th><th>Evidence calls</th></tr></thead>" +
              $"<tbody>{resultRows}</tbody></table>";
        var device = report.Device is null
            ? Html(report.Session.DeviceName)
            : Html($"{report.Device.Name} | {report.Device.Identity?.Model} | {report.Device.Identity?.DeviceId} | " +
                   $"firmware {report.Device.Firmware?.Version} | hardware {report.Device.Hardware?.Revision}");
        var html = Render(T("report.html"), new Dictionary<string, string>
        {
            ["styles"] = T("styles.css"), ["summary"] = summary,
            ["findings"] = findings.Length > 0 ? findings : "<p>No findings.</p>",
            ["results"] = results,
            ["device"] = device,
            ["assessment"] = Html($"Profile: {report.TestProfile} | Environment: {report.Session.Environment} | " +
                                  $"Started: {report.Session.StartedUtc:u} | Finished: {report.Session.FinishedUtc:u}"),
            ["meta"] = Html($"v{report.ToolkitVersion} | {report.Session.Environment} | {report.Session.DeviceName} | {report.Session.StartedUtc:u}")
        });
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, ReportGenerator.FileName(report, "html"));
        await File.WriteAllTextAsync(path, html, ct);
        return path;
    }
}
