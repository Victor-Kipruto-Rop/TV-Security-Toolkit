using System.Text.Json;
using System.Text.Json.Serialization;
using TVSecurityToolkit.Core.Integrity;
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

        // Seal before serialising so the chain covers exactly the records that land in this file.
        var records = report.Session.Results.SelectMany(r => r.Evidence).ToList();
        var headHash = EvidenceChain.Seal(records);

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, report, Options, ct);
                await stream.FlushAsync(ct);
            }

            File.Move(temporaryPath, path);

            // Written after the archive so a manifest never advertises an archive that is not there.
            await WriteManifestAsync(path, report, records.Count, headHash, ct);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Writes the integrity manifest that accompanies an evidence archive. It records the chain head so
    /// later edits to the archive can be detected by <see cref="VerifyAsync"/>.
    /// </summary>
    private static async Task WriteManifestAsync(
        string archivePath, SecurityReport report, int recordCount, string headHash, CancellationToken ct)
    {
        var manifest = new
        {
            schema = "tv-security-toolkit/evidence-manifest",
            version = 1,
            archive = Path.GetFileName(archivePath),
            sessionId = report.Session.Id,
            sessionStartedUtc = report.Session.StartedUtc,
            toolkitVersion = report.ToolkitVersion,
            testProfile = report.TestProfile,
            recordCount,
            chainHeadSha256 = headHash,
            generatedUtc = DateTimeOffset.UtcNow,
            guarantees = "Detects modification, reordering, and truncation of the evidence records. " +
                         "Unkeyed hash: it is tamper evidence, not tamper proofing, and is not a " +
                         "signature or proof of origin."
        };

        var manifestPath = archivePath + ".manifest.json";
        var temporaryPath = manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, Options, ct);
                await stream.FlushAsync(ct);
            }
            File.Move(temporaryPath, manifestPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Reloads an evidence archive and recomputes its chain, checking the head hash against the sibling
    /// manifest when one is present.
    /// </summary>
    public static async Task<EvidenceChainResult> VerifyAsync(string archivePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        if (!File.Exists(archivePath))
            return EvidenceChainResult.Failed($"evidence archive not found: {archivePath}");

        SecurityReport? report;
        try
        {
            await using var archiveStream = File.OpenRead(archivePath);
            report = await JsonSerializer.DeserializeAsync<SecurityReport>(archiveStream, Options, ct);
        }
        catch (JsonException)
        {
            return EvidenceChainResult.Failed("evidence archive could not be parsed");
        }

        if (report is null)
            return EvidenceChainResult.Failed("evidence archive could not be parsed");

        var records = report.Session.Results.SelectMany(r => r.Evidence).ToList();

        var manifestPath = archivePath + ".manifest.json";
        if (!File.Exists(manifestPath))
            return EvidenceChainResult.Failed("evidence manifest not found; integrity cannot be confirmed");

        using var manifestDoc = await ReadManifestAsync(manifestPath, ct);
        if (manifestDoc is null)
            return EvidenceChainResult.Failed("evidence manifest could not be parsed");

        var expectedHead = manifestDoc.RootElement.TryGetProperty("chainHeadSha256", out var head)
            ? head.GetString()
            : null;

        if (string.IsNullOrEmpty(expectedHead))
            return EvidenceChainResult.Failed("evidence manifest has no chain head hash");

        return EvidenceChain.Verify(records, expectedHead);
    }

    /// <summary>Reads and parses a manifest file, returning null when it cannot be parsed.</summary>
    private static async Task<JsonDocument?> ReadManifestAsync(string manifestPath, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
