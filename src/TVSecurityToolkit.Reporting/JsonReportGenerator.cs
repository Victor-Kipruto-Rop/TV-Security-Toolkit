using System.Text.Json;
using System.Text.Json.Serialization;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Reporting;

public sealed class JsonReportGenerator : IReportGenerator
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string Format => "json";

    public async Task<string> GenerateAsync(SecurityReport report, string outputDir, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, ReportGenerator.FileName(report, "json"));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, Opts), ct);
        return path;
    }
}
