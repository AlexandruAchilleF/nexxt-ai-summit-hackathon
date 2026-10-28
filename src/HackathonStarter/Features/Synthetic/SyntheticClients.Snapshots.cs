using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Synthetic;

/// <summary>
/// The 50 synthetic clients as the rule engine takes them (PLAN 7.1, 16.1): the ClientSnapshot before and after, the
/// ChangeEvent, the label and the expected decision, all from generation. Nothing here runs the engine.
///
/// ClientId is the number in the code (CL-1001 = 1001), clear of the golden cases (CL-00nn). The change is never saved
/// (Id = 0, EvaluationId = null) and uses ChangeEvent's channel names ("ghișeu" becomes "branch"). OldValueJson and
/// NewValueJson hold the ClientSnapshot properties that differ, as the snapshot has them; an upload adds "documents"
/// (kind and file name, never the content) to NewValueJson. R5's shape until ChangeService (R2) sets one.
///
/// What the snapshot cannot hold stays in <see cref="SyntheticSnapshotCase.Source"/>, marked INCOMPLET below.
/// </summary>
public static class SyntheticSnapshots
{
    // Values stay literal ("+40...", diacritics), not \u-escaped, so PiiVault's patterns still find them when the change is masked.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Built on every call: ChangeEvent is a mutable EF entity, so each caller gets its own.</summary>
    public static IReadOnlyList<SyntheticSnapshotCase> Create(int seed = SyntheticClients.Seed) =>
        [.. (seed == SyntheticClients.Seed ? SyntheticClients.All : SyntheticClients.Generate(seed)).Select(ToSnapshotCase)];

    public static SyntheticSnapshotCase ToSnapshotCase(this SyntheticClientCase c)
    {
        var (before, after) = (c.Before.ToSnapshot(), c.After.ToSnapshot());
        var (oldValue, newValue) = Values(before, after, c.After.Documents.Where(d => !c.Before.Documents.Contains(d)));
        return new(c, before, after, new ChangeEvent
        {
            ClientId = before.ClientId,
            Kind = As<ChangeKind>(c.Change.Kind),
            Summary = c.Change.Summary,
            OldValueJson = oldValue,
            NewValueJson = newValue,
            SourceChannel = Channel(c.Change.SourceChannel),
            ReceivedAt = c.Change.ReceivedAt,
        });
    }

    // INCOMPLET: Faza 0 nu are FirstName, LastName (doar FullName), Cnp (rămâne doar ca TIN-ul RO din autocertificare)
    // și documentele clientului (Kind, FileName, KeyLine; la upload, Kind și FileName ajung în NewValueJson).
    public static ClientSnapshot ToSnapshot(this SyntheticClient c) => new(
        ClientId(c.Code), c.Code, c.FullName, c.BirthDate, c.BirthCountry, [.. c.Citizenships],
        [.. c.Addresses.Select(ToSnapshot)],
        [.. c.Phones.Select(p => new PhoneSnapshot(p.Number, p.Country, p.IsCurrent))],
        [.. c.Accounts.Select(ToSnapshot)],
        [.. c.PowersOfAttorney.Select(ToSnapshot)],
        c.SelfCertifications.OrderByDescending(s => s.SignedAt).Select(ToSnapshot).FirstOrDefault(),
        []);

    public static int ClientId(string code) => int.Parse(code.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The synthetic enums are subsets of the contract's, with the same member names.</summary>
    public static T As<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    // INCOMPLET: Faza 0 nu are PostalCode, ValidFrom.
    private static AddressSnapshot ToSnapshot(SyntheticAddress a) => new(As<AddressKind>(a.Kind), a.Line, a.City, a.Country, a.IsCurrent);

    // INCOMPLET: Faza 0 nu are Product.
    private static AccountSnapshot ToSnapshot(SyntheticAccount a) => new(a.Iban, As<AccountType>(a.Type), a.OpenedOn, a.Balance, a.Currency,
        [.. a.StandingOrders.Select(ToSnapshot)]);

    // INCOMPLET: Faza 0 nu are BeneficiaryName, RoutingNumber (ABA-ul ordinului către SUA), Amount, Currency, Frequency.
    private static StandingOrderSnapshot ToSnapshot(SyntheticStandingOrder o) => new(o.BeneficiaryAccount, o.BeneficiaryCountry, o.IsActive);

    // INCOMPLET: Faza 0 nu are HolderAddress, Scope, ValidFrom.
    private static PowerOfAttorneySnapshot ToSnapshot(SyntheticPowerOfAttorney p) => new(p.HolderName, p.HolderCountry, p.IsActive);

    // INCOMPLET: Faza 0 nu are Channel.
    private static SelfCertificationSnapshot ToSnapshot(SyntheticSelfCertification s) =>
        new(s.SignedAt, [.. s.Residences.Select(r => new TaxResidenceSnapshot(r.Country, r.Tin))], s.DeclaresUsPerson);

    private static (string? Old, string? New) Values(ClientSnapshot before, ClientSnapshot after, IEnumerable<SyntheticDocument> uploaded)
    {
        (string Name, object? Before, object? After)[] properties =
        [
            ("birthCountry", before.BirthCountry, after.BirthCountry),
            ("citizenships", before.Citizenships, after.Citizenships),
            ("addresses", before.Addresses, after.Addresses),
            ("phones", before.Phones, after.Phones),
            ("accounts", before.Accounts, after.Accounts),
            ("powersOfAttorney", before.PowersOfAttorney, after.PowersOfAttorney),
            ("selfCertification", before.SelfCertification, after.SelfCertification),
            ("acceptedExplanations", before.AcceptedExplanations, after.AcceptedExplanations),
        ];
        var (oldValue, newValue) = (new JsonObject(), new JsonObject());
        foreach (var (name, was, now) in properties)
        {
            var (wasNode, nowNode) = (JsonSerializer.SerializeToNode(was, Json), JsonSerializer.SerializeToNode(now, Json));
            if (JsonNode.DeepEquals(wasNode, nowNode)) continue;
            (oldValue[name], newValue[name]) = (wasNode, nowNode);
        }
        var documents = uploaded.Select(d => new { Kind = As<DocumentKind>(d.Kind), d.FileName }).ToList();
        if (documents.Count > 0) newValue["documents"] = JsonSerializer.SerializeToNode(documents, Json);
        return (oldValue.Count == 0 ? null : oldValue.ToJsonString(Json), newValue.Count == 0 ? null : newValue.ToJsonString(Json));
    }

    private static string Channel(string synthetic) => synthetic switch
    {
        "ghișeu" => "branch",
        "online" or "call center" => synthetic,
        _ => throw new InvalidOperationException($"No ChangeEvent channel for '{synthetic}'."),
    };
}

/// <summary>
/// One synthetic client for the engine: <c>Evaluate(Before, After, Change.Kind)</c> should give <see cref="Expected"/>,
/// <see cref="ExpectedHits"/> exactly and <see cref="ExpectedCleared"/> as a subset (GOLDEN-CASES.md, 1).
/// </summary>
/// <param name="Source">The generated case, with everything the snapshot cannot hold (documents, postal codes, ABA, products).</param>
public sealed record SyntheticSnapshotCase(SyntheticClientCase Source, ClientSnapshot Before, ClientSnapshot After, ChangeEvent Change)
{
    public string Code => Source.Code;

    /// <summary>The scenario chosen at generation ("capcană: ordin din cont de depozit"), never the engine's result.</summary>
    public string Label => Source.Label;

    public string LabelEn => Source.Scenario.LabelEn;

    public Decision Expected => SyntheticSnapshots.As<Decision>(Source.Expected);

    public IReadOnlyList<ExpectedRule> ExpectedHits => Source.ExpectedHits;

    public IReadOnlyList<ExpectedRule> ExpectedCleared => Source.ExpectedCleared;
}
