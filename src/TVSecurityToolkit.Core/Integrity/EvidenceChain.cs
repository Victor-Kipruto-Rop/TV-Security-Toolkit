using System.Security.Cryptography;
using System.Text;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Core.Integrity;

/// <summary>
/// Links evidence records with a SHA-256 hash chain so a stored archive can be shown to be unaltered.
///
/// Records are sealed immediately before an archive is written, in the exact order they are serialised,
/// and the resulting head hash is stored beside the archive in a manifest. Verification recomputes the
/// chain, which detects modification, reordering, and truncation of the evidence.
///
/// This is tamper evidence, not tamper proofing: it is an unkeyed hash, so an attacker who can rewrite
/// the archive can also recompute the chain. It establishes internal consistency only, and is not
/// legal-grade non-repudiation or proof of origin. Sealing a manifest with a signing certificate (see
/// <c>scripts/verify-package-signatures.ps1</c>) is what makes it attributable.
/// </summary>
public static class EvidenceChain
{
    /// <summary>Chain link that precedes the first record.</summary>
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>
    /// Assigns a sequence number and chain hash to every record in order and returns the head hash.
    /// Any record whose capture time was never set is stamped with the current UTC time.
    /// </summary>
    public static string Seal(IList<Evidence> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var previous = Genesis;
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            record.Seq = i;
            if (record.RecordedUtc == default) record.RecordedUtc = DateTimeOffset.UtcNow;
            record.ChainHash = Link(previous, record);
            previous = record.ChainHash;
        }
        return previous;
    }

    /// <summary>
    /// Hash of one record bound to its predecessor, so editing any record invalidates it and every
    /// record after it.
    /// </summary>
    public static string Link(string previousHash, Evidence record)
    {
        ArgumentNullException.ThrowIfNull(record);
        // Field separator is a newline and the fields are length-delimited by ordering; a record value
        // containing a newline therefore cannot be shifted into a neighbouring field unnoticed, because
        // the recomputed digest would differ.
        var material = string.Join('\n',
            previousHash,
            record.Seq.ToString(),
            record.RecordedUtc.ToUniversalTime().ToString("O"),
            record.Call,
            record.Args,
            record.Result,
            string.Join('\n', record.Errors));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    /// <summary>
    /// Recomputes the chain over <paramref name="records"/> and optionally checks it against the head
    /// hash recorded in a manifest.
    /// </summary>
    public static EvidenceChainResult Verify(IReadOnlyList<Evidence> records, string? expectedHeadHash = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        // Order records the way they were sealed: by chain sequence across the whole session.
        var ordered = records.OrderBy(r => r.Seq).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Seq != i)
                return EvidenceChainResult.Failed($"evidence sequence gap at position {i} (record reports {ordered[i].Seq})");
            if (string.IsNullOrEmpty(ordered[i].ChainHash))
                return EvidenceChainResult.Failed($"evidence record {i} has no chain hash; the archive was not sealed");
        }

        var previous = Genesis;
        foreach (var record in ordered)
        {
            var actual = Link(previous, record);
            if (!string.Equals(actual, record.ChainHash, StringComparison.OrdinalIgnoreCase))
                return EvidenceChainResult.Failed($"evidence record {record.Seq} does not match its chain hash");
            previous = actual;
        }

        if (expectedHeadHash is not null &&
            !string.Equals(previous, expectedHeadHash, StringComparison.OrdinalIgnoreCase))
            return EvidenceChainResult.Failed("archive head hash does not match the manifest");

        return EvidenceChainResult.Ok(previous, ordered.Count);
    }
}

/// <summary>Outcome of an evidence-chain verification.</summary>
public sealed record EvidenceChainResult(bool IsValid, string? Reason, string HeadHash, int RecordCount)
{
    public static EvidenceChainResult Ok(string headHash, int count) => new(true, null, headHash, count);

    public static EvidenceChainResult Failed(string reason) => new(false, reason, "", 0);
}