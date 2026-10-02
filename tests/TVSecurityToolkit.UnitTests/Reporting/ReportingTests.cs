using System.Text.Json;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Reporting;

namespace TVSecurityToolkit.UnitTests.Reporting;

public class ReportingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tvst-" + Guid.NewGuid().ToString("N"));
    private static string Templates => Path.Combine(TestPaths.Root, "src", "TVSecurityToolkit.Reporting", "Templates");

    private static SecurityReport Report()
    {
        var s = new TestSession { Environment = "development", DeviceName = "simulator" };
        s.Results.Add(new TestResult { Id = "a.b.c", Title = "Replay <rejected>", Severity = Severity.Critical, Status = TestStatus.Fail, Message = "step 2 (apply): oops (x)" });
        s.Results.Add(new TestResult { Id = "a.b.d", Title = "ok", Severity = Severity.Low, Status = TestStatus.Pass });
        return new SecurityReport
        {
            ToolkitVersion = "1.0.0", Session = s,
            Findings = { new Finding { TestId = "a.b.c", Title = "Replay <rejected>", Severity = Severity.Critical, Status = TestStatus.Fail, Message = "step 2 (apply): oops (x)" } }
        };
    }

    [Fact]
    public async Task Html_report_encodes_content()
    {
        var path = await new HtmlReportGenerator(Templates).GenerateAsync(Report(), _dir, CancellationToken.None);
        var html = File.ReadAllText(path);
        Assert.Contains("Replay &lt;rejected&gt;", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public async Task Json_report_contains_summary_data()
    {
        var path = await new JsonReportGenerator().GenerateAsync(Report(), _dir, CancellationToken.None);
        Assert.Contains("\"Status\": \"Fail\"", File.ReadAllText(path));
    }

    [Fact]
    public async Task Evidence_archive_persists_report_and_step_evidence()
    {
        var report = Report();
        report.Session.Results[0].Evidence.Add(new Evidence
        {
            Call = "probe",
            Args = "{\"token\":\"[redacted]\"}",
            Result = "{\"secure\":true}",
            Errors = { "expected rejection" }
        });

        var path = await new EvidenceArchiveWriter().WriteAsync(report, Path.Combine(_dir, "evidence"), CancellationToken.None);
        using var archive = JsonDocument.Parse(File.ReadAllText(path));
        var json = archive.RootElement;
        var savedEvidence = json.GetProperty("Session").GetProperty("Results")[0].GetProperty("Evidence")[0];

        Assert.Equal("1.0.0", json.GetProperty("ToolkitVersion").GetString());
        Assert.Equal("probe", savedEvidence.GetProperty("Call").GetString());
        Assert.Equal("{\"token\":\"[redacted]\"}", savedEvidence.GetProperty("Args").GetString());
        Assert.Equal("expected rejection", savedEvidence.GetProperty("Errors")[0].GetString());
    }

    [Fact]
    public async Task Evidence_archive_rejects_session_id_that_cannot_form_a_filename()
    {
        var report = Report();
        report.Session.Id = "../";

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new EvidenceArchiveWriter().WriteAsync(report, _dir, CancellationToken.None));
    }

    [Fact]
    public async Task Html_report_includes_cancelled_count()
    {
        var report = Report();
        report.Session.Results.Add(new TestResult { Id = "a.b.e", Title = "cancelled", Status = TestStatus.Cancelled });

        var path = await new HtmlReportGenerator(Templates).GenerateAsync(report, _dir, CancellationToken.None);

        Assert.Contains("<span>Cancelled <b>1</b></span>", File.ReadAllText(path));
    }

    [Fact]
    public async Task Html_report_renders_device_results_and_finding_recommendation()
    {
        var report = Report();
        report.TestProfile = "development";
        report.Device = new TVSecurityToolkit.Core.Models.Device
        {
            Name = "test-device",
            Firmware = new FirmwareInfo { Version = "2.0.0" }
        };
        report.Findings[0].Id = "F-123";
        report.Findings[0].Recommendation = "Review the boot chain.";
        report.Findings[0].ExpectedBehavior = "signature must be valid";
        report.Session.Results[0].Evidence.Add(new Evidence { Call = "verify_signature", Result = "{\"valid\":false}" });

        var path = await new HtmlReportGenerator(Templates).GenerateAsync(report, _dir, CancellationToken.None);
        var html = File.ReadAllText(path);

        Assert.Contains("development", html);
        Assert.Contains("2.0.0", html);
        Assert.Contains("verify_signature", html);
        Assert.Contains("F-123", html);
        Assert.Contains("Review the boot chain.", html);
        Assert.Contains("signature must be valid", html);
    }

    [Fact]
    public async Task Pdf_report_has_valid_envelope()
    {
        var path = await new PdfReportGenerator().GenerateAsync(Report(), _dir, CancellationToken.None);
        var text = File.ReadAllText(path);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("%%EOF", text);
        Assert.Contains("\\(x\\)", text); // parentheses escaped
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
