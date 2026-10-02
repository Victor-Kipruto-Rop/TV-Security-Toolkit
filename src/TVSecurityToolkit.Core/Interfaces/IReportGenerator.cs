using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Core.Interfaces;

public interface IReportGenerator
{
    string Format { get; }
    /// <summary>Writes the report into outputDir and returns the file path.</summary>
    Task<string> GenerateAsync(SecurityReport report, string outputDir, CancellationToken ct);
}
