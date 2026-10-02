using System.Text.Json;
using System.Text.Json.Serialization;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Reporting;

public sealed class EvidenceArchiveWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<string> WriteAsync(SecurityReport report, string outputDir, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

        var sessionId = new string(report.Session.Id.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (sessionId.Length == 0)
            throw new InvalidDataException("The report session ID cannot be used as an evidence filename.");

        Directory.CreateDirectory(outputDir);
        var fileName = $"evidence-{sessionId}-{report.Session.StartedUtc:yyyyMMddHHmmss}.json";
        var path = Path.Combine(outputDir, fileName);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, report, Options, ct);
                await stream.FlushAsync(ct);
            }

            File.Move(temporaryPath, path);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
