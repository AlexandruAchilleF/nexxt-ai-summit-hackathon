using System.Text.Json;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// The golden cases' data (docs/GOLDEN-CASES.md), not the engine: unique ids, a layer per transcribed case, the details
/// agree with the headers, every identifier passes its checksum, every moment is UTC and the deadlines follow section 1.
/// </summary>
public class GoldenCasesDataTests
{
    private static readonly string[] Approved = ["4", "5a", "5b", "5c", "13", "15"];

    private static readonly Dictionary<string, string> PhonePrefixes = new() { ["RO"] = "+40", ["DE"] = "+49", ["AT"] = "+43", ["IT"] = "+39", ["ES"] = "+34", ["FR"] = "+33", ["US"] = "+1" };

    private static IEnumerable<GoldenCase> Transcribed => GoldenCases.All.Where(c => c.HasInput);

    [Fact]
    public void Ids_are_unique()
    {
        var ids = GoldenCases.All.Select(c => c.Id).ToList();
        Assert.Empty(ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.Equal(21, ids.Count);
        Assert.Same(GoldenCases.All.Single(c => c.Id == "5c"), GoldenCases.Get("5c"));
        Assert.Throws<KeyNotFoundException>(() => GoldenCases.Get("99"));
    }

    [Fact]
    public void The_approved_cases_are_transcribed()
    {
        Assert.Equal(Approved, GoldenCases.All.Where(c => c.Status == GoldenStatus.ApprovedR5).Select(c => c.Id));
        Assert.All(Approved, id => Assert.True(GoldenCases.Get(id).HasInput, $"case {id} has no input"));
        Assert.Equal(Approved, GoldenCases.Ready(GoldenLayer.Engine).Select(c => c.Id));
        Assert.Equal(["4"], GoldenCases.Ready(GoldenLayer.Workflow).Select(c => c.Id));
    }

    [Fact]
    public void Every_transcribed_case_has_a_layer_with_its_owner()
    {
        Assert.All(Transcribed, c =>
        {
            Assert.NotEqual(GoldenLayer.None, c.Layer);
            var owner = c.Owner switch { GoldenOwner.R1 => GoldenLayer.Engine, GoldenOwner.R2 => GoldenLayer.Workflow, _ => GoldenLayer.Shield };
            Assert.True(c.Layer.HasFlag(owner), $"case {c.Id}: layer {c.Layer} lacks the owner's {owner}");
            Assert.False(string.IsNullOrWhiteSpace(c.GuidePage), $"case {c.Id}: no guide page");
        });
    }

    [Fact]
    public void Details_agree_with_the_headers()
    {
        var known = RuleCatalog.All.Select(r => r.Id).ToHashSet();
        Assert.All(Transcribed, c =>
        {
            Assert.Equal(c.ExpectedHits, c.ExpectedIndicia.Select(h => h.Key));
            Assert.Equal(c.ExpectedCleared, c.ExpectedClearedRules.Select(h => h.RuleId));
            Assert.Equal(c.ExpectedDeadlineRule, c.ExpectedDeadline?.RuleId);
            Assert.All(c.ExpectedClearedRules, h => Assert.Equal(Coverage.None, h.CoveredBy));
            Assert.All(c.Documents.SelectMany(d => d.Quotes).Where(q => q.EvidenceFor is not null), q => Assert.Contains(q.EvidenceFor!, known));

            Assert.NotNull(c.Change);
            Assert.Equal(c.ClientCode, c.Change.ClientCode);
            Assert.Equal(c.ClientCode, c.Before!.Code);
            Assert.Equal(c.ClientCode, c.After!.Code);
            Assert.Equal(c.Before.ClientId, c.After.ClientId);
        });
    }

    [Fact]
    public void Decisions_follow_the_coverage_of_the_indicia()
    {
        // PLAN 7.2: NoCase = no indicium; Compliant = all covered; an uncovered FATCA / CRS indicium = FatcaAlert / Clarification.
        foreach (var c in Transcribed.Where(c => c.ExpectedDecision != Decision.ShieldAlert && c.ExpectedIndicia.All(IsIndicium)))
        {
            Assert.True(Derived(c.ExpectedIndicia) == c.ExpectedDecision, $"case {c.Id}: {c.ExpectedDecision} does not follow from the indicia");
            Assert.Equal(UncoveredRegime(c.ExpectedIndicia), c.ExpectedRegime);
            Assert.Equal(c.ExpectedDecision is Decision.Clarification or Decision.FatcaAlert, c.ExpectedDeadline is not null);
            if (c.Response is { } r)
                Assert.True(Derived(r.ExpectedIndicia) == r.ExpectedDecision, $"case {c.Id}: {r.ExpectedDecision} after the answer does not follow");
        }
    }

    [Fact]
    public void Identifiers_pass_their_checksums()
    {
        var problems = new List<string>();
        foreach (var code in GoldenClients.Codes)
        {
            var client = GoldenClients.Create(code);
            var cnp = client.Cnp!;
            if (cnp.Length != 13 || SyntheticData.CnpChecksum(cnp[..12]) != cnp[12] - '0') problems.Add($"{code}: CNP {cnp}");
            var century = cnp[0] is '1' or '2' ? 1900 : 2000;
            if (new DateOnly(century + int.Parse(cnp[1..3]), int.Parse(cnp[3..5]), int.Parse(cnp[5..7])) != client.BirthDate)
                problems.Add($"{code}: CNP {cnp} does not match the birth date {client.BirthDate}");
            foreach (var r in client.SelfCertifications.SelectMany(s => s.Residences).Where(r => r.Country == "RO"))
                if (r.Tin != cnp) problems.Add($"{code}: RO TIN {r.Tin} is not the CNP");
        }

        foreach (var c in Transcribed)
        {
            var clients = new[] { c.Before!, c.After! };
            foreach (var iban in clients.SelectMany(s => s.Accounts).Select(a => a.Iban).Append(c.Change!.AccountIban).OfType<string>())
                CheckIban(c.Id, iban, problems);
            foreach (var order in clients.SelectMany(s => s.Accounts).SelectMany(a => a.StandingOrders).Append(c.Change.StandingOrder).OfType<StandingOrderSnapshot>())
            {
                if (order.BeneficiaryCountry != "US") { CheckIban(c.Id, order.BeneficiaryAccount, problems); continue; }
                var routing = order.BeneficiaryAccount.Split(' ')[0];
                if (routing.Length != 9 || SyntheticData.AbaCheckDigit(routing[..8]) != routing[8] - '0') problems.Add($"case {c.Id}: ABA {routing}");
            }
            foreach (var phone in clients.SelectMany(s => s.Phones).Append(c.Change.Phone).OfType<PhoneSnapshot>())
                if (!phone.Number.StartsWith(PhonePrefixes[phone.Country], StringComparison.Ordinal) || !phone.Number[1..].All(char.IsDigit))
                    problems.Add($"case {c.Id}: phone {phone.Number} is not E.164 for {phone.Country}");

            var certifications = clients.Select(s => s.SelfCertification).Append(c.Response?.SelfCertification).OfType<SelfCertificationSnapshot>();
            foreach (var tin in certifications.SelectMany(s => s.Residences).Where(r => r.Country == "DE").Select(r => r.Tin ?? ""))
                if (tin.Length != 11 || SyntheticData.Mod11_10CheckDigit(tin[..10]) != tin[10] - '0') problems.Add($"case {c.Id}: DE TIN {tin}");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Moments_are_utc()
    {
        var problems = new List<string>();
        void Check(string what, DateTime? at)
        {
            if (at is { Kind: not DateTimeKind.Utc }) problems.Add($"{what}: {at} is {at.Value.Kind}");
        }

        foreach (var code in GoldenClients.Codes)
            foreach (var s in GoldenClients.Create(code).SelfCertifications)
            {
                Check($"{code} self-certification signed", s.SignedAt);
                Check($"{code} self-certification received", s.ReceivedAt);
            }
        foreach (var c in Transcribed)
        {
            Check($"case {c.Id} received", c.ReceivedAt);
            Check($"case {c.Id} due", c.ExpectedDeadline?.DueAt);
            Check($"case {c.Id} reminder", c.ExpectedDeadline?.RemindAt);
            Check($"case {c.Id} answer", c.Response?.ReceivedAt);
            Check($"case {c.Id} new self-certification", c.Response?.SelfCertification?.SignedAt);
            foreach (var s in new[] { c.Before!, c.After! })
            {
                Check($"case {c.Id} self-certification", s.SelfCertification?.SignedAt);
                foreach (var e in s.AcceptedExplanations) Check($"case {c.Id} accepted explanation", e.AcceptedAt);
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Deadlines_follow_the_conventions()
    {
        // GOLDEN-CASES.md 1: DueAt = ReceivedAt + 90 days (new account), RemindAt = DueAt - 5 days; FATCA: no deadline (Î1).
        Assert.Equal(GoldenCases.ChangeMoment.AddDays(90), GoldenCases.NewAccountDueAt);
        Assert.Equal(GoldenCases.NewAccountDueAt.AddDays(-5), GoldenCases.NewAccountRemindAt);

        foreach (var c in Transcribed.Where(c => c.ExpectedDeadline is not null))
        {
            var deadline = c.ExpectedDeadline!;
            var regime = c.ExpectedRegime == Regime.Fatca ? Regime.Fatca : Regime.Crs;
            var statuses = c.After!.Accounts.Select(a => Preexisting.For(regime, a.OpenedOn)).Distinct().ToList();
            Assert.True(statuses.Count == 1 && statuses[0] == deadline.Preexisting, $"case {c.Id}: Preexisting {deadline.Preexisting} vs accounts {string.Join(",", statuses)}");

            switch (deadline.RuleId)
            {
                case null:
                    Assert.Equal(Regime.Fatca, c.ExpectedRegime);
                    Assert.Null(deadline.DueAt);
                    Assert.Null(deadline.RemindAt);
                    break;
                case "DL-NEW-90":
                    Assert.False(deadline.Preexisting);
                    Assert.Equal(c.ReceivedAt!.Value.AddDays(90), deadline.DueAt);
                    Assert.Equal(deadline.DueAt!.Value.AddDays(-5), deadline.RemindAt);
                    break;
                case "DL-PRE-YEAR":
                    Assert.True(deadline.Preexisting);
                    Assert.True(deadline.DueAt >= c.ReceivedAt!.Value.AddDays(90), $"case {c.Id}: DL-PRE-YEAR before the 90 days");
                    Assert.Equal(deadline.DueAt!.Value.AddDays(-5), deadline.RemindAt);
                    break;
            }
            if (c.Response is { } r && deadline.DueAt is { } due)
                Assert.True(r.ReceivedAt > c.ReceivedAt && r.ReceivedAt <= due, $"case {c.Id}: the answer is not in time");
        }
    }

    [Fact]
    public void Clients_are_built_once_per_code_and_never_shared()
    {
        Assert.All(Transcribed, c => Assert.Contains(c.ClientCode, GoldenClients.Codes));
        Assert.NotSame(GoldenClients.Create("CL-0005"), GoldenClients.Create("CL-0005"));
        Assert.Throws<KeyNotFoundException>(() => GoldenClients.Create("CL-9999"));

        // Radu's three orders start from the same client; only the order differs.
        var before = new[] { "5a", "5b", "5c" }.Select(id => JsonSerializer.Serialize(GoldenCases.Get(id).Before)).Distinct();
        Assert.Single(before);
        Assert.Equal(JsonSerializer.Serialize(ClientSnapshot.From(GoldenClients.Create("CL-0005"))), before.Single());
    }

    [Fact]
    public async Task Documents_are_generated_and_contain_their_quotes()
    {
        // The quotes must be in the generated PDF as DocumentText extracts it, or EvidenceVerifier rejects them.
        var problems = new List<string>();
        foreach (var c in GoldenCases.All)
            foreach (var d in c.Documents.Append(c.Response?.Document).OfType<GoldenDocument>())
            {
                if (!SyntheticPdfs.FileNames.Contains(d.FileName)) { problems.Add($"case {c.Id}: {d.FileName} is not generated"); continue; }
                var pdf = SyntheticPdfs.Get(d.FileName);
                if (pdf.ClientCode != c.ClientCode) problems.Add($"case {c.Id}: {d.FileName} belongs to {pdf.ClientCode}");
                var file = await DocumentText.ExtractAsync(new MemoryStream(pdf.Bytes), pdf.FileName);
                if (d.Pages is { } pages && pages != file.Pages) problems.Add($"case {c.Id}: {d.FileName} has {file.Pages} pages, not {pages}");
                foreach (var q in d.Quotes.Where(q => !file.Text.Contains(q.Text, StringComparison.Ordinal)))
                    problems.Add($"case {c.Id}: \"{q.Text}\" is not in {d.FileName}");
            }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static bool IsIndicium(GoldenHit hit) => hit.RuleId.Contains("-IND-", StringComparison.Ordinal);

    private static Decision Derived(IReadOnlyList<GoldenHit> indicia)
    {
        var open = indicia.Where(h => h.CoveredBy == Coverage.None).ToList();
        if (open.Any(h => h.RuleId.StartsWith("FATCA-", StringComparison.Ordinal))) return Decision.FatcaAlert;
        if (open.Count > 0) return Decision.Clarification;
        return indicia.Count > 0 ? Decision.Compliant : Decision.NoCase;
    }

    private static Regime? UncoveredRegime(IReadOnlyList<GoldenHit> indicia)
    {
        var regimes = indicia.Where(h => h.CoveredBy == Coverage.None)
            .Select(h => h.RuleId.StartsWith("FATCA-", StringComparison.Ordinal) ? Regime.Fatca : Regime.Crs).Distinct().ToList();
        return regimes.Count switch { 0 => null, 1 => regimes[0], _ => Regime.Both };
    }

    private static void CheckIban(string caseId, string iban, List<string> problems)
    {
        if (iban.Length < 15 || SyntheticData.IbanWithCheckDigits(iban[..2], iban[4..]) != iban) problems.Add($"case {caseId}: IBAN {iban}");
    }
}
