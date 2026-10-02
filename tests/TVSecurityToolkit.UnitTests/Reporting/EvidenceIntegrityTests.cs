using System.Text.Json;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Integrity;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Reporting;

namespace TVSecurityToolkit.UnitTests.Reporting;

/// <summary>
/// Evidence integrity: a sealed archive must verify, and any edit, reorder, or truncation of the
/// stored records must be detected.
/// </summary>
public class EvidenceIntegrityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tvst-chain-" + Guid.NewGuid().ToString("N"));

    private static SecurityReport ReportWithEvidence(int records = 3)
    {
        var session = new TestSession { Environment = "development", DeviceName = "simulator" };
        var result = new TestResult { Id = "a.b.c", Title = "probe", Severity = Severity.High, Status = TestStatus.Fail };
        for (var i = 0; i < records; i++)
        {
            result.Evidence.Add(new Evidence
            {
                Call = "identity",
                Args = $"{{\"probe\":{i}}}",
                Result = $"{{\"model\":\"sim-{i}\"}}"
            });
        }
        session.Results.Add(result);
        return new SecurityReport { ToolkitVersion = "1.0.0", TestProfile = "development", Session = session };
    }

    [Fact]
    public void Seal_assigns_sequential_numbers_and_links_every_record()
    {
        var records = ReportWithEvidence(3).Session.Results[0].Evidence;

        var head = EvidenceChain.Seal(records);

        Assert.Equal(new long[] { 0, 1, 2 }, records.Select(r => r.Seq));
        Assert.All(records, r => Assert.Equal(64, r.ChainHash.Length));
        Assert.All(records, r => Assert.NotEqual(default, r.RecordedUtc));
        Assert.NotEqual(EvidenceChain.Genesis, head);
        Assert.Equal(records[^1].ChainHash, head);
    }

    [Fact]
    public void Sealing_is_deterministic_for_the_same_records()
    {
        var a = ReportWithEvidence(4).Session.Results[0].Evidence;
        var b = ReportWithEvidence(4).Session.Results[0].Evidence;
        var stamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        foreach (var r in a.Concat(b)) r.RecordedUtc = stamp;

        Assert.Equal(EvidenceChain.Seal(a), EvidenceChain.Seal(b));
    }

    [Fact]
    public void Verify_accepts_an_untouched_chain()
    {
        var records = ReportWithEvidence(3).Session.Results[0].Evidence;
        var head = EvidenceChain.Seal(records);

        var result = EvidenceChain.Verify(records, head);

        Assert.True(result.IsValid, result.Reason);
        Assert.Equal(3, result.RecordCount);
        Assert.Equal(head, result.HeadHash);
    }

    [Fact]
    public void Verify_rejects_an_edited_result_value()
    {
        var records = ReportWithEvidence(3).Session.Results[0].Evidence;
        var head = EvidenceChain.Seal(records);

        records[1].Result = "{\"model\":\"sim-999\"}";

        var result = EvidenceChain.Verify(records, head);
        Assert.False(result.IsValid);
        Assert.Contains("record 1", result.Reason);
    }

    [Fact]
    public void Verify_rejects_permuted_sequence_numbers()
    {
        var records = ReportWithEvidence(4).Session.Results[0].Evidence;
        var head = EvidenceChain.Seal(records);

        // An attacker swapping the logical order swaps the sequence numbers, which are part of the
        // hashed material and therefore break the chain.
        (records[1].Seq, records[2].Seq) = (records[2].Seq, records[1].Seq);

        var result = EvidenceChain.Verify(records, head);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_normalises_physical_order_by_sequence()
    {
        var records = ReportWithEvidence(4).Session.Results[0].Evidence;
        var head = EvidenceChain.Seal(records);

        // Relocating a record in the stored array without touching its content is not a change to the
        // evidence itself: the chain is defined over the sequence numbers, so this stays valid.
        var moved = records[0];
        records.RemoveAt(0);
        records.Add(moved);

        var result = EvidenceChain.Verify(records, head);
        Assert.True(result.IsValid, result.Reason);
    }

    [Fact]
    public void Verify_rejects_records_swapped_in_place()
    {
        var records = ReportWithEvidence(4).Session.Results[0].Evidence;
        var head = EvidenceChain.Seal(records);

        // Two records exchanging their observed result while keeping their positions.
        (records[1].Result, records[2].Result) = (records[2].Result, records[1].Result);

        var result = EvidenceChain.Verify(records, head);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_rejects_truncated_chain()
    {
        var records = ReportWithEvidence(4).Session.Results[0].Evidence;
        var head = EvidenceChain.Seal(records);
        records.RemoveAt(records.Count - 1);

        var result = EvidenceChain.Verify(records, head);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_rejects_unsealed_records()
    {
        var records = ReportWithEvidence(2).Session.Results[0].Evidence;

        var result = EvidenceChain.Verify(records, EvidenceChain.Genesis);
        Assert.False(result.IsValid);
        Assert.Contains("not sealed", result.Reason);
    }

    [Fact]
    public void Verify_rejects_a_manifest_head_that_does_not_match()
    {
        var records = ReportWithEvidence(2).Session.Results[0].Evidence;
        EvidenceChain.Seal(records);

        var result = EvidenceChain.Verify(records, new string('a', 64));
        Assert.False(result.IsValid);
        Assert.Contains("manifest", result.Reason);
    }

    [Fact]
    public async Task Written_archive_verifies_against_its_manifest()
    {
        var report = ReportWithEvidence(3);
        var path = await new EvidenceArchiveWriter().WriteAsync(report, _dir, CancellationToken.None);

        Assert.True(File.Exists(path + ".manifest.json"));

        var result = await EvidenceArchiveWriter.VerifyAsync(path, CancellationToken.None);
        Assert.True(result.IsValid, result.Reason);
        Assert.Equal(3, result.RecordCount);
    }

    [Fact]
    public async Task Tampering_with_a_written_archive_is_detected()
    {
        var report = ReportWithEvidence(3);
        var path = await new EvidenceArchiveWriter().WriteAsync(report, _dir, CancellationToken.None);

        // Rewrite one stored record, as an attacker editing the archive would.
        var text = File.ReadAllText(path);
        Assert.Contains("sim-1", text);
        File.WriteAllText(path, text.Replace("sim-1", "sim-7"));

        var result = await EvidenceArchiveWriter.VerifyAsync(path, CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Removing_the_manifest_fails_verification_closed()
    {
        var report = ReportWithEvidence(2);
        var path = await new EvidenceArchiveWriter().WriteAsync(report, _dir, CancellationToken.None);
        File.Delete(path + ".manifest.json");

        var result = await EvidenceArchiveWriter.VerifyAsync(path, CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("manifest", result.Reason);
    }

    [Fact]
    public async Task Manifest_records_the_chain_head_and_record_count()
    {
        var report = ReportWithEvidence(3);
        var path = await new EvidenceArchiveWriter().WriteAsync(report, _dir, CancellationToken.None);

        using var doc = JsonDocument.Parse(File.ReadAllText(path + ".manifest.json"));
        var manifest = doc.RootElement;

        Assert.Equal(3, manifest.GetProperty("recordCount").GetInt32());
        Assert.Equal(64, manifest.GetProperty("chainHeadSha256").GetString()!.Length);
        Assert.Equal(report.Session.Id, manifest.GetProperty("sessionId").GetString());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
    }
}
