using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Reporting;

/// <summary>Dispatches to the format generators ("html", "json", "pdf").</summary>
public sealed class ReportGenerator
{
    private readonly Dictionary<string, IReportGenerator> _gens;

    public ReportGenerator(IEnumerable<IReportGenerator>? generators = null) =>
        _gens = (generators ?? new IReportGenerator[] { new HtmlReportGenerator(), new JsonReportGenerator(), new PdfReportGenerator() })
            .ToDictionary(g => g.Format, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Formats => _gens.Keys;

    public static string FileName(SecurityReport r, string ext) =>
        $"report-{r.Session.Id}-{r.Session.StartedUtc:yyyyMMdd-HHmmss}.{ext}";

    public async Task<List<string>> GenerateAsync(SecurityReport report, IEnumerable<string> formats, string outputDir, CancellationToken ct)
    {
        var paths = new List<string>();
        foreach (var f in formats)
        {
            if (!_gens.TryGetValue(f, out var g)) throw new ArgumentException("unknown report format: " + f);
            paths.Add(await g.GenerateAsync(report, outputDir, ct));
        }
        return paths;
    }
}
