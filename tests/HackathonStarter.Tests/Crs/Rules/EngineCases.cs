using HackathonStarter.Features.Rules;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using AddressKind = HackathonStarter.Features.Clients.AddressKind;
using static HackathonStarter.Tests.Crs.RuleEngine.Subjects;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>An expected indicium; SourceField is checked only when the spec states it.</summary>
public sealed record ExpectedHit(string RuleId, string Jurisdiction, Coverage CoveredBy = Coverage.None, string? SourceField = null);

/// <summary>
/// Regime is compared always (null = no case). Says: phrases the spec quotes, found in the outcome explanation or
/// in a Cleared explanation.
/// </summary>
public sealed record EngineCase(string Id, string Title, RuleSubject Before, RuleSubject After, RuleChange Change,
    Decision Expected, ExpectedHit[] Hits, (string RuleId, string Jurisdiction)[] ClearedAtLeast, string[]? ReportableFor = null,
    Regime? Regime = null, string[]? Says = null);

/// <summary>
/// R1's copy of the engine layer of docs/GOLDEN-CASES.md, on RuleSubject, with extra checks (source fields, phrases). The
/// shared data is R5's GoldenCases.cs: GoldenDataEngineTests runs it through the registered engine and EvaluationService;
/// these may differ in data a card does not fix (balances, IBANs) but never in the decision, the hits or the regime
/// (GoldenEngineTests.Engine_cases_never_contradict_the_canonical_golden_cases).
/// </summary>
internal static class EngineCases
{
    private static RuleChange Change(string kind, string channel = "online", string summary = "") => new(kind, summary, channel);

    // Case 4: Maria Popescu (CL-0001), new account, moves to München, German phone replaces the Romanian one.
    public static RuleSubject MariaBefore => RoResident("CL-0001", "2910608400319", new DateOnly(2019, 3, 14), "+40721384516",
        "Maria Popescu", new DateOnly(1991, 6, 8), "RO19RZBR0000060021458391", "Bd. Unirii 15, bl. 7, ap. 22, 030823 București");

    public static RuleSubject MariaAfter => MariaBefore.MovedTo("DE", "Leopoldstraße 12, 80802 München").ReplacePhone("+498945217730", "DE");

    public static SubjectSelfCertification MariaResponseForm =>
        SelfCert(new DateTime(2027, 1, 21, 0, 0, 0, DateTimeKind.Utc), new SubjectTaxResidence("DE", "86095174223")) with
        {
            FullName = "Maria Popescu", ResidenceAddress = "Leopoldstraße 12, 80802 München", BirthDate = new DateOnly(1991, 6, 8),
        };

    public static RuleSubject MariaAfterResponse => MariaAfter.WithSelfCert(MariaResponseForm);

    // Cases 5a / 5b / 5c: Radu Ionescu (CL-0005), a current account (2017) and a custodial account (2020), both new.
    private static SubjectAccount RaduCurrent => Account(AccountType.Depository, new DateOnly(2017, 5, 2),
        "RO46RZBR0000060019823746", 12_000m, "EUR") with { Product = "Cont curent EUR" };

    private static SubjectAccount RaduCustodial => Account(AccountType.Custodial, new DateOnly(2020, 9, 10),
        "RO88RZBR0000069004471852", 25_000m) with { Product = "Cont de instrumente financiare" };

    public static RuleSubject Radu(SubjectAccount current, SubjectAccount custodial) =>
        RoResident("CL-0005", "1820214400458", new DateOnly(2017, 5, 2), fullName: "Radu Ionescu",
            birthDate: new DateOnly(1982, 2, 14), residence: "Calea Moșilor 112, ap. 9, 020871 București").WithAccounts(current, custodial);

    private static SubjectAccount WithKellerOrder(this SubjectAccount a) =>
        a.WithOrder("DE02700100800918273645", "DE", beneficiary: "Hausverwaltung Keller GmbH, Leipzig");

    // A US account has no IBAN (Q2): routing number and account number, the country set explicitly.
    private static SubjectAccount WithUsOrder(this SubjectAccount a) =>
        a.WithOrder("ABA 073909523, cont 4417209358", "US", beneficiary: "Daniel Ionescu, Chicago");

    // Case 13 / 8: Andrei Pop (CL-0013), the trap persona.
    public static RuleSubject AndreiBefore => RoResident("CL-0013", "1950923350125", new DateOnly(2021, 4, 6), "+40745210963",
        "Andrei Pop", new DateOnly(1995, 9, 23), "RO20RZBR0000060033317205", "Str. Mărășești 6, ap. 3, 300086 Timișoara");

    // Case 15 / 1: Ioana Mureșan (CL-0015), preexisting account (2014).
    public static RuleSubject IoanaInCluj => RoResident("CL-0015", "2880411120231", new DateOnly(2014, 2, 17), "+40744518207",
        "Ioana Mureșan", new DateOnly(1988, 4, 11), "RO43RZBR0000060007719364", "Str. Memorandumului 21, ap. 4, 400114 Cluj-Napoca");

    public static RuleSubject IoanaInIasi => IoanaInCluj.MovedTo("RO", "Bd. Ștefan cel Mare și Sfânt 8, ap. 12, 700064 Iași");

    // Case 9: Sorin Matei (CL-0009), self-certification RO + DE (2025).
    private static RuleSubject SorinBefore => RoResident("CL-0009", "1840930081451", new DateOnly(2019, 1, 21), "+40726447031",
        "Sorin Matei", new DateOnly(1984, 9, 30), "RO87RZBR0000060085527316", "Str. Lungă 40, 500035 Brașov") with
    {
        SelfCertifications = [SelfCert(new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc),
            new SubjectTaxResidence("RO", "1840930081451"), new SubjectTaxResidence("DE", "47302619546")) with
        {
            FullName = "Sorin Matei", ResidenceAddress = "Str. Lungă 40, 500035 Brașov", BirthDate = new DateOnly(1984, 9, 30),
        }],
    };

    // Case 12: Petru Sârbu (CL-0012), preexisting lower value account, no phone.
    public static RuleSubject PetruBefore => RoResident("CL-0012", "1610403121731", new DateOnly(2011, 5, 16), fullName: "Petru Sârbu",
            birthDate: new DateOnly(1961, 4, 3), residence: "Str. Horea 5, 400174 Cluj-Napoca").WithoutPhones()
        .WithAccounts(Account(AccountType.Depository, new DateOnly(2011, 5, 16), "RO20RZBR0000060118852290", 42_300m));

    // Case 18 / 16: Teodora Iliescu (CL-0018), student in München, Romanian phone stays current.
    private static RuleSubject TeodoraBefore => RoResident("CL-0018", "6040502401181", new DateOnly(2022, 9, 1), "+40731904226",
        "Teodora Iliescu", new DateOnly(2004, 5, 2), "RO38RZBR0000060153296860");

    public static RuleSubject TeodoraAfter => TeodoraBefore.MovedTo("DE", "Helene-Mayer-Ring 9, 80809 München");

    private static RuleSubject Alexandra => RoResident("CL-0006", "2930719400974", new DateOnly(2016, 8, 4), fullName: "Alexandra Vlad",
        birthDate: new DateOnly(1993, 7, 19), iban: "RO34RZBR0000060063391027");

    private static RuleSubject Marius => RoResident("CL-0007", "1790305082613", new DateOnly(2018, 11, 12), fullName: "Marius Stoica",
        birthDate: new DateOnly(1979, 3, 5), iban: "RO76RZBR0000060074415582");

    private static RuleSubject Doina => RoResident("CL-0011", "2580117222041", new DateOnly(2018, 6, 5), fullName: "Doina Cazacu",
        birthDate: new DateOnly(1958, 1, 17), iban: "RO56RZBR0000060107741068");

    private static RuleSubject Lucian => RoResident("CL-0014", "1910226354122", new DateOnly(2020, 2, 11), "+40752318640",
        "Lucian Toma", new DateOnly(1991, 2, 26), "RO27RZBR0000060129963514");

    private static RuleSubject Irina => RoResident("CL-0016", "2860614402290", new DateOnly(2020, 6, 15), "+40723655190",
        "Irina Constantin", new DateOnly(1986, 6, 14), "RO06RZBR0000060131074639");

    private static RuleSubject Gheorghe => RoResident("CL-0017", "1670921403155", new DateOnly(2012, 9, 3), "+40722180944",
        "Gheorghe Marin", new DateOnly(1967, 9, 21), "RO03RZBR0000060142185752");

    public static IReadOnlyList<EngineCase> All { get; } =
    [
        new("4", "Maria: München, DE phone, no RO phone, new account", MariaBefore, MariaAfter,
            Change("AddressChanged", summary: "Adresă nouă în München și telefon DE, care înlocuiește telefonul RO"),
            Decision.Clarification,
            [new(RuleIds.CrsAddress, "DE", SourceField: "Addresses/Residence"), new(RuleIds.CrsPhone, "DE", SourceField: "Phones")],
            [], [], Regime.Crs),

        new("4-response", "Maria after the new self-certification [DE]", MariaAfter, MariaAfterResponse,
            Change("SelfCertSubmitted", summary: "autocertificare-maria-2027.pdf"), Decision.Compliant,
            [new(RuleIds.CrsAddress, "DE", Coverage.SelfCertification), new(RuleIds.CrsPhone, "DE", Coverage.SelfCertification)],
            [(RuleIds.SelfCertConsistent, "DE")], ["DE"]),

        new("5a", "Standing order to DE from a current account",
            Radu(RaduCurrent, RaduCustodial), Radu(RaduCurrent.WithKellerOrder(), RaduCustodial),
            Change("StandingOrderCreated"), Decision.NoCase, [], [(RuleIds.CrsStandingOrder, "DE")],
            Says: ["cont de depozit", "exceptat la CRS"]),

        new("5b", "Same order from a custodial account",
            Radu(RaduCurrent, RaduCustodial), Radu(RaduCurrent, RaduCustodial.WithKellerOrder()),
            Change("StandingOrderCreated"), Decision.Clarification, [new(RuleIds.CrsStandingOrder, "DE", SourceField: "StandingOrders")],
            [], Regime: Regime.Crs),

        new("5c", "Standing order to a US account from a current account",
            Radu(RaduCurrent, RaduCustodial), Radu(RaduCurrent.WithUsOrder(), RaduCustodial),
            Change("StandingOrderCreated"), Decision.FatcaAlert, [new(RuleIds.FatcaStandingOrder, "US", SourceField: "StandingOrders")],
            [(RuleIds.CrsStandingOrder, "US")], Regime: Regime.Fatca,
            Says: ["SUA nu e jurisdicție CRS raportabilă", "cont de depozit"]),

        new("13", "Andrei: DE phone next to the RO phone", AndreiBefore, AndreiBefore.WithPhone("+4915144201187", "DE"),
            Change("PhoneAdded", "call center", "+4915144201187"), Decision.NoCase, [], [(RuleIds.CrsPhone, "DE")],
            Says: ["există și un telefon actual în România"]),

        new("15", "Cluj to Iași", IoanaInCluj, IoanaInIasi, Change("AddressChanged", "ghișeu"),
            Decision.NoCase, [], [(RuleIds.Domestic, "RO")], Says: ["Noua adresă e în România", "nicio regulă din lista închisă"]),

        new("1", "RO phone replaced by another RO phone", IoanaInIasi, IoanaInIasi.ReplacePhone("+40733902614", "RO"),
            Change("PhoneChanged", summary: "+40744518207 → +40733902614"), Decision.NoCase, [], [(RuleIds.Domestic, "RO")]),

        new("8", "Salary from a German company (document only)", AndreiBefore, AndreiBefore,
            Change("DocumentUploaded", summary: "fluturas-andrei-2026-09.pdf"), Decision.NoCase, [], [(RuleIds.Domestic, "RO")]),

        new("6", "US place of birth from the passport", Alexandra.BornIn(null), Alexandra.BornIn("US"),
            Change("DocumentUploaded", summary: "pasaport-alexandra.pdf"), Decision.FatcaAlert,
            [new(RuleIds.FatcaBirthPlace, "US", SourceField: "BirthCountry")], [], Regime: Regime.Fatca),

        new("7", "US citizenship (second passport), born in RO", Marius, Marius.WithCitizenships("RO", "US"),
            Change("DocumentUploaded", summary: "pasaport-us-marius.pdf"), Decision.FatcaAlert,
            [new(RuleIds.FatcaCitizenship, "US", SourceField: "Citizenships")], [(RuleIds.FatcaBirthPlace, "US")], Regime: Regime.Fatca,
            Says: ["Locul nașterii la dosar e România (RO), nu SUA"]),

        new("9", "Dual residence RO + DE declared, second residence in DE",
            SorinBefore, SorinBefore.WithAddress(AddressKind.Residence, "DE", text: "Kastanienallee 30, 10435 Berlin"),
            Change("AddressAdded"), Decision.Compliant, [new(RuleIds.CrsAddress, "DE", Coverage.SelfCertification)],
            [(RuleIds.SelfCertConsistent, "DE")], ["DE"], Says: ["SC-CONSISTENT"]),

        new("11", "Power of attorney to a person with an address in AT", Doina, Doina.WithPowerOfAttorney("Matei Cazacu", "AT"),
            Change("PowerOfAttorneyGranted", "ghișeu", "Împuternicit Matei Cazacu, Mariahilfer Straße 45, 1060 Wien, AT"),
            Decision.Clarification, [new(RuleIds.CrsPowerOfAttorney, "AT", SourceField: "PowersOfAttorney")], [],
            Regime: Regime.Crs),

        new("12", "In-care-of address in IT, the only address (preexisting, lower value)",
            PetruBefore, PetruBefore.WithOnlyAddress(AddressKind.InCareOf, "IT", "c/o Studio Legale Bianchi, Via Roma 10, 10121 Torino"),
            Change("AddressChanged", "poștă", "Vă rog să trimiteți corespondența la avocatul meu din Torino."),
            Decision.Clarification,
            [new(RuleIds.CrsInCareOf, "IT", SourceField: "Addresses/InCareOf"), new(RuleIds.CrsMailing, "IT", SourceField: "Addresses/InCareOf")],
            [(RuleIds.FatcaInCareOf, "US")], Regime: Regime.Crs,
            Says: ["Cont preexistent cu valoare mică: o adresă «în grija» din afara SUA nu e indiciu FATCA", "cont nedocumentat"]),

        new("14", "US phone next to the RO phone", Lucian, Lucian.WithPhone("+13125550147", "US"),
            Change("PhoneAdded", summary: "+13125550147"), Decision.FatcaAlert, [new(RuleIds.FatcaPhone, "US", SourceField: "Phones")],
            [(RuleIds.CrsPhone, "US")], Regime: Regime.Fatca),

        new("16", "Move to Spain, new account (engine part of 16 / 17a)", Irina, Irina.MovedTo("ES", "Calle de Alcalá 120, 28009 Madrid"),
            Change("AddressChanged"), Decision.Clarification, [new(RuleIds.CrsAddress, "ES")], [], Regime: Regime.Crs),

        new("17b", "Move to Spain, preexisting account (engine part)", Gheorghe,
            Gheorghe.MovedTo("ES", "Avenida Diagonal 450, 08006 Barcelona"), Change("AddressChanged", "ghișeu"),
            Decision.Clarification, [new(RuleIds.CrsAddress, "ES")], [], Regime: Regime.Crs),

        new("18", "Student in DE: before the explanation", TeodoraBefore, TeodoraAfter, Change("AddressChanged"),
            Decision.Clarification, [new(RuleIds.CrsAddress, "DE")], [], Regime: Regime.Crs),

        // The response is an explanation, not a self-certification; ChangeKind has no value for it, so R2 decides the kind.
        new("18-explained", "Student in DE: explanation accepted", TeodoraAfter, TeodoraAfter.WithExplanation("DE", "Student"),
            Change("DocumentUploaded", summary: "adeverinta-student-teodora.pdf"), Decision.Compliant,
            [new(RuleIds.CrsAddress, "DE", Coverage.AcceptedExplanation)], [(RuleIds.Explanation, "DE")], []),
    ];

    public static EngineCase Get(string id) => All.Single(c => c.Id == id);

    public static TheoryData<string> Ids() => [.. All.Select(c => c.Id)];
}
