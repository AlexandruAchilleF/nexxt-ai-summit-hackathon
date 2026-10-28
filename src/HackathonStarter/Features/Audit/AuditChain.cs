using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HackathonStarter.Features.Audit;

/// <summary>The hash chain itself, pure: Hash = SHA256(PrevHash + "\n" + Canonical), the first PrevHash is GENESIS.</summary>
public static class AuditChain
{
    public const string Genesis = "GENESIS";

    /// <summary>JSON array [Seq, At, Actor, CaseId, Kind, PayloadJson]: unambiguous, unlike plain concatenation.</summary>
    public static string Canonical(AuditEntry e) => JsonSerializer.Serialize(new object?[]
    {
        e.Seq,
        DateTime.SpecifyKind(e.At, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        e.Actor, e.CaseId, e.Kind, e.PayloadJson,
    });

    public static string ComputeHash(string prevHash, AuditEntry e) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prevHash + "\n" + Canonical(e))));

    /// <summary>
    /// Stops at the first break: a missing Seq (Expected/Actual = the Seq numbers), a broken link to the previous hash, or
    /// a hash that no longer matches (Expected = recomputed, Actual = saved). Checked counts the intact entries before it.
    /// </summary>
    public static AuditVerification Verify(IReadOnlyList<AuditEntry> bySeq)
    {
        var previousSeq = 0L;
        var previousHash = Genesis;
        for (var i = 0; i < bySeq.Count; i++)
        {
            var e = bySeq[i];
            if (e.Seq != previousSeq + 1)
                return new(false, i, e.Seq, (previousSeq + 1).ToString(CultureInfo.InvariantCulture), e.Seq.ToString(CultureInfo.InvariantCulture));
            if (e.PrevHash != previousHash) return new(false, i, e.Seq, previousHash, e.PrevHash);
            var expected = ComputeHash(e.PrevHash, e);
            if (expected != e.Hash) return new(false, i, e.Seq, expected, e.Hash);
            previousSeq = e.Seq;
            previousHash = e.Hash;
        }
        return new(true, bySeq.Count, null, null, null);
    }
}
