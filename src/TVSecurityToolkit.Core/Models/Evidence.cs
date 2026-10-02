namespace TVSecurityToolkit.Core.Models;

public sealed class Evidence
{
    public string Call { get; set; } = "";
    public string Args { get; set; } = "";
    public string Result { get; set; } = "";
    public List<string> Errors { get; set; } = new();

    /// <summary>Position of this record in the session-wide evidence chain (0-based).</summary>
    public long Seq { get; set; }

    /// <summary>When the record was captured, in UTC.</summary>
    public DateTimeOffset RecordedUtc { get; set; }

    /// <summary>
    /// Link hash binding this record to its predecessor. Empty until the archive is sealed by
    /// <see cref="TVSecurityToolkit.Core.Integrity.EvidenceChain"/>.
    /// </summary>
    public string ChainHash { get; set; } = "";
}
