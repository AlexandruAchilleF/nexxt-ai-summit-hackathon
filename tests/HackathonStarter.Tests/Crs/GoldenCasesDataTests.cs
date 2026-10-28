using System.Text.Json;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// The golden cases' data (docs/GOLDEN-CASES.md), not the engine: unique ids, all 21 transcribed with a layer, the details
/// agree with the headers, the change is in After, the documents are the synthetic PDFs, every identifier passes its
/// checksum, every moment is UTC and the deadlines follow section 1.
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
    public void All_21_cases_are_transcribed()
    {
        Assert.All(GoldenCases.All, c => Assert.True(c.HasInput, $"case {c.Id} has no input"));
        Assert.Equal(Approved, GoldenCases.All.Where(c => c.Status == GoldenStatus.ApprovedR5).Select(c => c.Id));
        Assert.All(GoldenCases.All.Where(c => !Approved.Contains(c.Id)), c => Assert.Equal(GoldenStatus.Validated, c.Status));

        // GOLDEN-CASES.md 2, column "Strat".
        Assert.Equal(["1", "2", "3", "4", "5a", "5b", "5c", "6", "7", "8", "9", "11", "12", "13", "14", "15", "18"], Ready(GoldenLayer.Engine));
        Assert.Equal(["4", "16", "17a", "17b", "18"], Ready(GoldenLayer.Workflow));
        Assert.Equal(["10"], Ready(GoldenLayer.Shield));
        Assert.Equal(["2", "3"], Ready(GoldenLayer.TinFormat));
        Assert.Equal(["4", "8"], Ready(GoldenLayer.Agent));
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
            Assert.All(Documents(c).SelectMany(d => d.Quotes).Where(q => q.EvidenceFor is not null), q => Assert.Contains(q.EvidenceFor!, known));

            Assert.NotNull(c.Change);
            Assert.Equal(c.ClientCode, c.Change.ClientCode);
            Assert.Equal(c.ClientCode, c.Before!.Code);
            Assert.Equal(c.ClientCode, c.After!.Code);
            Assert.Equal(c.Before.ClientId, c.After.ClientId);
        });
    }

    [Fact]
    public void The_change_carries_its_payload_and_after_has_it()
    {
        Assert.All(Transcribed, c =>
        {
            var change = c.Change!;
            switch (change.Kind)
            {
                case ChangeKind.AddressChanged or ChangeKind.AddressAdded:
                    Assert.Contains(change.Address!, c.After!.CurrentAddresses);
                    break;
                case ChangeKind.PhoneAdded or ChangeKind.PhoneChanged:
                    Assert.Contains(change.Phone!, c.After!.CurrentPhones);
                    break;
                case ChangeKind.StandingOrderCreated:
                    Assert.Contains(change.StandingOrder!, c.After!.Accounts.Single(a => a.Iban == change.AccountIban).StandingOrders);
                    break;
                case ChangeKind.PowerOfAttorneyGranted:
                    Assert.Contains(change.PowerOfAttorney!, c.After!.PowersOfAttorney);
                    break;
                case ChangeKind.SelfCertSubmitted:
                    // The engine reads the declaration from the change; the snapshot is unchanged.
                    Assert.NotNull(change.SelfCertification);
                    Assert.Equal(Json(c.Before!), Json(c.After!));
                    break;
                case ChangeKind.DocumentUploaded:
                    Assert.NotEmpty(c.Documents);
                    break;
                default:
                    Assert.Fail($"case {c.Id}: no check for {change.Kind}");
                    break;
            }
        });
    }

    [Fact]
    public void Documents_are_the_synthetic_pdfs_of_the_client()
    {
        Assert.All(Transcribed, c => Assert.All(Documents(c), d =>
        {
            Assert.Contains(d.FileName, SyntheticPdfs.FileNames);
            Assert.Equal(c.ClientCode, SyntheticPdfs.Get(d.FileName).ClientCode);
        }));
    }

    [Fact]
    public async Task Evidence_and_signal_quotes_are_in_the_text_of_their_pdf()
    {
        // What EvidenceVerifier will look for; the hidden text of case 10 is never a quote.
        var problems = new List<string>();
        foreach (var c in Transcribed)
            foreach (var d in Documents(c))
            {
                var file = await DocumentText.ExtractAsync(new MemoryStream(SyntheticPdfs.Get(d.FileName).Bytes), d.FileName);
                foreach (var q in d.Quotes.Where(q => q.EvidenceFor is not null || q.IsSignal))
                    if (!file.Text.Contains(q.Text, StringComparison.Ordinal)) problems.Add($"case {c.Id}: {d.FileName} has no \"{q.Text}\"");
                if (d.Quotes.Any(q => q.Text.Contains(SyntheticPdfs.Injection, StringComparison.Ordinal))) problems.Add($"case {c.Id}: the injection is quoted");
            }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
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
                if (!phone.Number.StartsWith(PhonePrefixes[phone.Country], StringComparison.Ordinal) || !phone.Number[1..].All(char.IsDigit)
                    || (phone.Country == "RO" && phone.Number.Length != 12)) // +40 and 9 digits
                    problems.Add($"case {c.Id}: phone {phone.Number} is not E.164 for {phone.Country}");

            var certifications = clients.Select(s => s.SelfCertification).Append(c.Change.SelfCertification).Append(c.Response?.SelfCertification)
                .OfType<SelfCertificationSnapshot>();
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
            Check($"case {c.Id} submitted self-certification", c.Change!.SelfCertification?.SignedAt);
            foreach (var s in new[] { c.Before!, c.After! })
            {
                Check($"case {c.Id} self-certification", s.SelfCertification?.SignedAt);
                foreach (var e in s.AcceptedExplanations) Check($"case {c.Id} accepted explanation", e.AcceptedAt);
            }
        }
        foreach (var e in GoldenCases.TeodoraAfterAnswer.AcceptedExplanations) Check("case 18 after the answer", e.AcceptedAt);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Deadlines_follow_the_conventions()
    {
        // GOLDEN-CASES.md 1: DueAt = ReceivedAt + 90 days (new account), RemindAt = DueAt - 5 days; FATCA and case 2: no deadline (Î1).
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
                    Assert.True(c.ExpectedRegime == Regime.Fatca || c.ExpectedIndicia.All(h => h.RuleId.StartsWith("SC-", StringComparison.Ordinal)),
                        $"case {c.Id}: no deadline rule, but neither FATCA nor a self-certification");
                    Assert.Null(deadline.DueAt);
                    Assert.Null(deadline.RemindAt);
                    break;
                case "DL-NEW-90":
                case "DL-EXPIRED-NEW": // 17a: the expired deadline of 16
                    Assert.False(deadline.Preexisting);
                    Assert.Equal(c.ReceivedAt!.Value.AddDays(90), deadline.DueAt);
                    Assert.Equal(deadline.DueAt!.Value.AddDays(-5), deadline.RemindAt);
                    break;
                case "DL-PRE-YEAR":
                    // The later of 31.12 at 23:59:59 in Bucharest (UTC+2 in winter, Î5) and ReceivedAt + 90 days (N1: our interpretation).
                    Assert.True(deadline.Preexisting);
                    var received = c.ReceivedAt!.Value;
                    var yearEnd = new DateTime(received.Year, 12, 31, 21, 59, 59, DateTimeKind.Utc);
                    Assert.Equal(yearEnd > received.AddDays(90) ? yearEnd : received.AddDays(90), deadline.DueAt);
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

        // The shared clients (GOLDEN-CASES.md 1, "Coduri"): case 1 starts where 15 ends, 8 where 13 starts, 17a is 16 later.
        Assert.Equal(Json(GoldenCases.Get("15").After!), Json(GoldenCases.Get("1").Before!));
        Assert.Equal(Json(GoldenCases.Get("13").Before!), Json(GoldenCases.Get("8").Before!));
        Assert.Equal(Json(GoldenCases.Get("16").Before!), Json(GoldenCases.Get("17a").Before!));
        Assert.Equal(Json(GoldenCases.Get("16").After!), Json(GoldenCases.Get("17a").After!));
        Assert.Equal(GoldenCases.Get("16").Change, GoldenCases.Get("17a").Change);
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

    [Fact]
    public void The_domain_decisions_are_in_the_data()
    {
        // GOLDEN-CASES.md 5 (28.10). Case 2: only the Milan address before the change, no self-certification on file.
        var cristina = GoldenCases.Get("2").Before!;
        Assert.Equal("IT", Assert.Single(cristina.Addresses).Country);
        Assert.Empty(cristina.Phones);
        Assert.Null(cristina.SelfCertification);

        // Î9: over 50.000 USD on the depository accounts of the FATCA cases; in EUR, with a margin, so no exchange rate.
        foreach (var id in new[] { "5c", "6", "7", "14" })
            Assert.All(GoldenCases.Get(id).After!.Accounts.Where(a => a.Type == AccountType.Depository),
                a => Assert.True(a.Currency == "EUR" && a.Balance >= 75_000m, $"case {id}: {a.Balance} {a.Currency}"));

        // 7: born in RO, so FATCA-IND-POB is cleared. 12: the c/o address is the only current one (Î6, variant A).
        Assert.Equal("RO", GoldenCases.Get("7").After!.BirthCountry);
        Assert.Equal(AddressKind.InCareOf, Assert.Single(GoldenCases.Get("12").After!.CurrentAddresses).Kind);
    }

    [Fact]
    public void Case_18_after_the_answer_adds_only_the_accepted_explanation()
    {
        var c = GoldenCases.Get("18");
        var answered = GoldenCases.TeodoraAfterAnswer;

        Assert.Empty(c.After!.AcceptedExplanations);
        Assert.Equal(Json(c.After), Json(answered with { AcceptedExplanations = [] }));
        var explanation = Assert.Single(answered.AcceptedExplanations);
        Assert.Equal(Assert.Single(c.ExpectedIndicia).Jurisdiction, explanation.Jurisdiction);
        Assert.Equal(c.Response!.Category, explanation.Category);
        Assert.InRange(explanation.AcceptedAt, c.Response.ReceivedAt, c.ExpectedDeadline!.DueAt!.Value);
        Assert.Equal(Coverage.AcceptedExplanation, Assert.Single(c.Response.ExpectedIndicia).CoveredBy);
    }

    private static string[] Ready(GoldenLayer layer) => [.. GoldenCases.Ready(layer).Select(c => c.Id)];

    private static IEnumerable<GoldenDocument> Documents(GoldenCase c) => c.Response?.Document is { } answer ? c.Documents.Append(answer) : c.Documents;

    private static string Json(ClientSnapshot snapshot) => JsonSerializer.Serialize(snapshot);

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
