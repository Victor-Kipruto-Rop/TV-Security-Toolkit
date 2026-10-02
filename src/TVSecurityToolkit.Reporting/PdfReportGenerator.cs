using System.Text;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Reporting;

/// <summary>Dependency-free text PDF (Helvetica, A4, paginated). Non-ASCII characters become '?'.</summary>
public sealed class PdfReportGenerator : IReportGenerator
{
    public string Format => "pdf";
    private const int LinesPerPage = 52;

    public async Task<string> GenerateAsync(SecurityReport report, string outputDir, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, ReportGenerator.FileName(report, "pdf"));
        await File.WriteAllBytesAsync(path, Build(Lines(report)), ct);
        return path;
    }

    private static List<string> Lines(SecurityReport r)
    {
        var l = new List<string>
        {
            "TV Security Toolkit report",
            $"Version {r.ToolkitVersion} | {r.Session.Environment} | {r.Session.DeviceName} | {r.Session.StartedUtc:u}",
            "",
            $"Total {r.Total}  Passed {r.Passed}  Failed {r.Failed}  Errors {r.Errors}  Skipped {r.Skipped}  Cancelled {r.Cancelled}",
            "", $"Profile {r.TestProfile} | Started {r.Session.StartedUtc:u} | Finished {r.Session.FinishedUtc:u}",
            $"Device {r.Device?.Name ?? r.Session.DeviceName} | Model {r.Device?.Identity?.Model ?? ""} | Firmware {r.Device?.Firmware?.Version ?? ""}",
            "", "Test Results", ""
        };
        foreach (var result in r.Session.Results)
        {
            l.Add($"[{result.Status}] [{result.Severity}] {result.Id}: {result.Title}");
            foreach (var chunk in Wrap(result.Message, 90)) if (chunk.Length > 0) l.Add("    " + chunk);
            foreach (var evidence in result.Evidence)
            {
                l.Add("    Evidence: " + evidence.Call);
                foreach (var chunk in Wrap(evidence.Args + " => " + evidence.Result, 90)) l.Add("      " + chunk);
                foreach (var error in evidence.Errors) l.Add("      Error: " + error);
            }
        }
        l.AddRange(new[] { "", "Findings and Recommendations", "" });
        if (r.Findings.Count == 0) l.Add("No findings.");
        foreach (var f in r.Findings)
        {
            l.Add($"[{f.Severity}] {f.Title} ({f.Status}) — {f.Id}");
            l.Add("    " + f.TestId);
            l.Add("    Expected: " + f.ExpectedBehavior);
            l.Add("    Actual: " + f.ActualBehavior);
            l.Add("    Recommendation: " + f.Recommendation);
            foreach (var chunk in Wrap(f.Message, 90)) l.Add("    " + chunk);
        }
        return l;
    }

    private static IEnumerable<string> Wrap(string s, int width)
    {
        for (var i = 0; i < s.Length; i += width) yield return s.Substring(i, Math.Min(width, s.Length - i));
    }

    private static string Esc(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (c < 32 || c > 126) sb.Append('?');
            else if (c is '(' or ')' or '\\') sb.Append('\\').Append(c);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static byte[] Build(List<string> lines)
    {
        var pages = lines.Chunk(LinesPerPage).ToList();
        var objs = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [" + string.Join(" ", pages.Select((_, i) => $"{4 + 2 * i} 0 R")) + $"] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        for (var i = 0; i < pages.Count; i++)
        {
            var content = new StringBuilder("BT /F1 10 Tf 40 800 Td 14 TL\n");
            foreach (var line in pages[i]) content.Append('(').Append(Esc(line)).Append(") Tj T*\n");
            content.Append("ET");
            objs.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {5 + 2 * i} 0 R /Resources << /Font << /F1 3 0 R >> >> >>");
            objs.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objs.Count; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
