using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Synthetic;

namespace HackathonStarter.Features.Evaluation;

/// <summary>Who owns the golden case in the tests (docs/GOLDEN-CASES.md, column "Strat"): R1 engine, R2 workflow, R3 shield.</summary>
public enum GoldenOwner { R1, R2, R3 }

/// <summary>
/// One golden case: the expected result written BEFORE the code (docs/GOLDEN-CASES.md, docs/PLAN.md 16.1). Owner of the
/// data: R5; R1 and R2 read it in tests and ask R5 for corrections. Phase 0: id, client, expectation only; R5 adds the input
/// data (Before / After snapshots, the change, documents) as new properties.
/// </summary>
/// <param name="ExpectedHits">Compared exactly as RuleId:Jurisdiction (with coverage in R5's full data).</param>
/// <param name="ExpectedCleared">Compared as a subset: the trap rule whose explanation matters.</param>
public record GoldenCase(
    string Id,
    string Title,
    string ClientCode,
    GoldenOwner Owner,
    Decision ExpectedDecision,
    string[] ExpectedHits,
    string[] ExpectedCleared,
    string? ExpectedDeadlineRule,
    string ExpectedOutcome)
{
    /// <summary>The layers that run the case (GOLDEN-CASES.md, column "Strat"); <see cref="Owner"/> is whose test file it is.</summary>
    public GoldenLayer Layer { get; init; }
    public GoldenStatus Status { get; init; }
    /// <summary>The card's pages of the ANAF guide ("29 b, c; 34; 35-36"), or "design" for an interpretation of ours.</summary>
    public string? GuidePage { get; init; }

    public ClientSnapshot? Before { get; init; }
    public ClientSnapshot? After { get; init; }
    /// <summary>What IChangeService receives; the rule engine needs only <see cref="ChangeRequest.Kind"/>.</summary>
    public ChangeRequest? Change { get; init; }
    /// <summary>When the bank learns of the change (UTC): the simulated clock is set here and the deadline runs from it.</summary>
    public DateTime? ReceivedAt { get; init; }
    /// <summary>The documents of the card, with the quotes the mock and the evidence checks use.</summary>
    public IReadOnlyList<GoldenDocument> Documents { get; init; } = [];

    /// <summary>The regime of the uncovered indicia (<see cref="RuleOutcome.Regime"/>); null when nothing is uncovered.</summary>
    public Regime? ExpectedRegime { get; init; }
    /// <summary><see cref="ExpectedHits"/> in full: compared exactly on (RuleId, Jurisdiction), with CoveredBy.</summary>
    public IReadOnlyList<GoldenHit> ExpectedIndicia { get; init; } = [];
    /// <summary><see cref="ExpectedCleared"/> in full: compared as a subset, with the jurisdiction and the card's reason.</summary>
    public IReadOnlyList<GoldenHit> ExpectedClearedRules { get; init; } = [];
    /// <summary>Null when the card does not say; then not compared.</summary>
    public IReadOnlyList<string>? ExpectedReportableFor { get; init; }
    /// <summary>Null = no case, so no deadline.</summary>
    public GoldenDeadline? ExpectedDeadline { get; init; }
    /// <summary>The client's answer and what is expected after it (workflow cases).</summary>
    public GoldenResponse? Response { get; init; }

    /// <summary>True once R5 has transcribed the card's input; the golden tests run only these.</summary>
    public bool HasInput => Before is not null && After is not null && Change is not null && ReceivedAt is not null;
}

/// <summary>Engine = R1 (no DB), Workflow = R2, Shield and TinFormat = R3, Agent = R4.</summary>
[Flags]
public enum GoldenLayer { None = 0, Engine = 1, Workflow = 2, Shield = 4, TinFormat = 8, Agent = 16 }

/// <summary>PROPUS, APROBAT R5, VALIDAT (GOLDEN-CASES.md): a validated case changes only with a notice on chat.</summary>
public enum GoldenStatus { Proposed, ApprovedR5, Validated }

/// <param name="Jurisdiction">Null: not compared (GEN-* rules).</param>
/// <param name="SourceField">As the card names it ("Addresses/Residence"); null when the card does not say (not compared).</param>
/// <param name="CoveredBy">Indicia only. A cleared rule did not apply at all, so it stays None.</param>
/// <param name="Reason">The card's reason, in Romanian, for reading; never compared as text.</param>
public sealed record GoldenHit(string RuleId, string? Jurisdiction, string? SourceField = null, Coverage CoveredBy = Coverage.None,
                               string? Reason = null, string? GuidePage = null)
{
    public string Key => Jurisdiction is null ? RuleId : $"{RuleId}:{Jurisdiction}";
}

/// <param name="RuleId">The DL-* rule; null when the guide gives none (FATCA and case 2, GOLDEN-CASES.md Î1).</param>
/// <param name="Preexisting">Of the account that sets the deadline, per regime (<see cref="Clients.Preexisting.For"/>).</param>
/// <param name="DueAt">UTC; null = no deadline in the guide: the case opens without one (DeadlineResult.DueAt is null too).</param>
public sealed record GoldenDeadline(string? RuleId, bool Preexisting, DateTime? DueAt, DateTime? RemindAt);

/// <param name="Pages">Null when the card does not say.</param>
public sealed record GoldenDocument(string FileName, DocumentKind Kind, int? Pages, IReadOnlyList<GoldenQuote> Quotes);

/// <param name="EvidenceFor">The rule the quote is evidence for.</param>
/// <param name="IsSignal">A context signal: never changes the decision, never opens a case.</param>
public sealed record GoldenQuote(string Text, string? EvidenceFor = null, bool IsSignal = false);

/// <param name="SelfCertification">The new self-certification, for ResponseKind.NewSelfCertification.</param>
/// <param name="ExpectedDecision">On re-evaluation with the answer.</param>
/// <param name="RuleId">The rule the closing rests on (SC-VALID, RESP-EXPLANATION).</param>
/// <param name="ExpectedStatus">After the approved closeCase.</param>
public sealed record GoldenResponse(
    DateTime ReceivedAt, string Channel, ResponseKind Kind, ExplanationCategory Category,
    SelfCertificationSnapshot? SelfCertification, GoldenDocument? Document,
    Decision ExpectedDecision, IReadOnlyList<GoldenHit> ExpectedIndicia, IReadOnlyList<string> ExpectedReportableFor,
    string RuleId, CaseStatus ExpectedStatus, ConfirmationChannel ProofChannel);

public static class GoldenCases
{
    // Declared before All: static fields initialize in order.
    /// <summary>The default moment of a change (GOLDEN-CASES.md, 1): 2026-10-28 09:00 in Bucharest.</summary>
    public static readonly DateTime ChangeMoment = new(2026, 10, 28, 7, 0, 0, DateTimeKind.Utc);
    /// <summary>New account (DL-NEW-90): <see cref="ChangeMoment"/> + 90 days; the reminder 5 days before (day 85).</summary>
    public static readonly DateTime NewAccountDueAt = new(2027, 1, 26, 7, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime NewAccountRemindAt = new(2027, 1, 21, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TeodoraAnsweredAt = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Case 18 after the answer (GOLDEN-CASES.md, "Snapshot după răspuns"): After plus the student explanation for DE. The
    /// engine re-evaluates it to Compliant; CaseService.CloseAsync saves the same explanation, with the real case code.
    /// </summary>
    public static readonly ClientSnapshot TeodoraAfterAnswer = Snapshot("CL-0018", c =>
    {
        MoveTo(c, TeodoraMunich());
        // The card: "caz CRS-2026-xxxx" (the database gives the code) and 2026-11-16, taken at the answer's moment: midnight
        // would come before the answer.
        c.AcceptedExplanations.Add(new() { Jurisdiction = "DE", Category = ExplanationCategory.Student, CaseCode = "CRS-2026-xxxx", AcceptedAt = TeodoraAnsweredAt });
    });

    private const string RaduCurrentEur = "RO46RZBR0000060019823746";
    private const string RaduCustodial = "RO88RZBR0000069004471852";

    public static readonly IReadOnlyList<GoldenCase> All =
    [
        new("1", "RO phone replaced by another RO phone", "CL-0015", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], null, "No case")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "design",
            // Ioana after the move to Iași (After of case 15). The card gives no moment: the default one.
            Before = Snapshot("CL-0015", c => MoveTo(c, IoanaIasi())),
            After = Snapshot("CL-0015", c => { MoveTo(c, IoanaIasi()); ReplacePhones(c, IoanaNewPhone()); }),
            // Summary added by R5, not on the card.
            Change = new("CL-0015", ChangeKind.PhoneChanged, "online", Phone: Of(IoanaNewPhone()),
                Summary: "Telefon nou +40733902614, care înlocuiește +40744518207"),
            ReceivedAt = ChangeMoment,
            ExpectedClearedRules = [new("GEN-DOMESTIC", null)],
            ExpectedReportableFor = [], // derived, not on the card
        },
        new("2", "Self-certification with an Italian TIN in the wrong format", "CL-0002", GoldenOwner.R1, Decision.Clarification, ["SC-TIN-FORMAT:IT"], [], null, "Clarification: TIN format invalid")
        {
            Layer = GoldenLayer.Engine | GoldenLayer.TinFormat,
            Status = GoldenStatus.Validated,
            GuidePage = "36-37",
            // SelfCertSubmitted: the engine checks Change.SelfCertification against the bank's data (EvaluateSelfCertification).
            // The snapshot changes only when IChangeService stores the declaration (with a status R2 sets), so After = Before.
            // Before has only the Milan address (the card): with an RO residence on file other rules would apply (p. 36).
            Before = Snapshot("CL-0002"),
            After = Snapshot("CL-0002"),
            // One character short of a codice fiscale; the valid variant DBRCST90A41Z129T (format valid) is for R3's TinFormat tests.
            // Summary added by R5, not on the card.
            Change = new("CL-0002", ChangeKind.SelfCertSubmitted, "online",
                SelfCertification: new(GoldenClients.Midnight(2026, 10, 27), [new("IT", "DBRCST90A41Z129")], DeclaresUsPerson: false),
                Summary: "Autocertificare nouă: Via Padova 18, 20127 Milano; rezidență fiscală IT"),
            ReceivedAt = ChangeMoment,
            Documents =
            [
                // The card's data as the PDF writes it: an evidence quote must be found in the document text.
                new(SyntheticPdfs.SelfCertCristina, DocumentKind.SelfCertForm, null,
                [
                    new("Cristina Dobre"), new("Via Padova 18, 20127 Milano"), new("01/01/1990"),
                    new("DBRCST90A41Z129", EvidenceFor: "SC-TIN-FORMAT"), new("Semnătură: confirmată online de titular"), new("27/10/2026"),
                ]),
            ],
            ExpectedRegime = Regime.Crs,
            // A wrong format gives Clarification: a design choice of ours (the card).
            ExpectedIndicia = [new("SC-TIN-FORMAT", "IT", Reason: "codice fiscale are 16 caractere; aici lipsește caracterul de control", GuidePage: "36-37")],
            // No deadline in the guide (Î1, the same as FATCA): the case opens without one.
            ExpectedDeadline = new(null, Preexisting: false, null, null),
        },
        new("3", "Self-certification without a TIN for Austria, with a reason", "CL-0003", GoldenOwner.R1, Decision.Compliant, ["SC-TIN-MISSING:AT"], [], null, "Compliant, reason recorded")
        {
            Layer = GoldenLayer.Engine | GoldenLayer.TinFormat,
            Status = GoldenStatus.Validated,
            GuidePage = "53, 37",
            // As case 2: After = Before, the engine reads Change.SelfCertification.
            // ÎNTREBARE (the card): what Before has is not decided; for now a standard RO resident (GoldenClients).
            Before = Snapshot("CL-0003"),
            After = Snapshot("CL-0003"),
            // Reason B, "unable to obtain" (Î8, p. 53): A ("the country does not issue") contradicts his own explanation.
            // Summary added by R5, not on the card.
            Change = new("CL-0003", ChangeKind.SelfCertSubmitted, "branch",
                SelfCertification: new(GoldenClients.Midnight(2026, 10, 26),
                    [new("AT", null, NoTinReason.B_UnableToObtain, "Nu sunt încă înregistrat la administrația fiscală austriacă și nu am primit Steuernummer.")],
                    DeclaresUsPerson: false),
                Summary: "Autocertificare nouă: Annenstraße 24, 8020 Graz; rezidență fiscală AT, fără TIN (motivul B)"),
            ReceivedAt = ChangeMoment,
            Documents =
            [
                new(SyntheticPdfs.SelfCertBogdan, DocumentKind.SelfCertForm, null,
                [
                    new("Bogdan Lazăr"), new("Annenstraße 24, 8020 Graz"), new("22/05/1987"),
                    new("Nu sunt încă înregistrat la administrația fiscală austriacă și nu am primit Steuernummer.", EvidenceFor: "SC-TIN-MISSING"),
                    new("Semnătură: Bogdan Lazăr"), new("26/10/2026"),
                ]),
            ],
            // "Acceptat" with Decision.Compliant (PLAN 7.2: every indicium covered): AT is declared, so the hit is covered and reportable.
            // Compliant rests on the OECD sheet for AT, saved in docs/surse/oecd-tin-at.md (Q1 "Individuals No").
            ExpectedIndicia =
            [
                new("SC-TIN-MISSING", "AT", CoveredBy: Coverage.SelfCertification, GuidePage: "53, 37",
                    Reason: "acceptat: fișa OCDE AT: „TINs are only issued to individuals who are liable to tax.”; motivul B înregistrat"),
            ],
            ExpectedReportableFor = ["AT"],
        },
        new("4", "Maria: Munich, German phone, no RO phone, new account", "CL-0001", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"], [], "DL-NEW-90", "Case opened, 90-day deadline, then closed")
        {
            Layer = GoldenLayer.Engine | GoldenLayer.Workflow | GoldenLayer.Agent,
            Status = GoldenStatus.ApprovedR5,
            GuidePage = "29 b, c; 34; 35-36",
            Before = Snapshot("CL-0001"),
            // One AddressChanged event carries both changes (Î3): the engine evaluates the whole After snapshot.
            After = Snapshot("CL-0001", c => { MoveTo(c, MariaMunich()); ReplacePhones(c, MariaGermanPhone()); }),
            Change = new("CL-0001", ChangeKind.AddressChanged, "online", Address: Of(MariaMunich()), Phone: Of(MariaGermanPhone()),
                Summary: "Adresă nouă în München și telefon DE, care înlocuiește telefonul RO"),
            ReceivedAt = ChangeMoment,
            Documents =
            [
                new("fluturas-maria-2026-09.pdf", DocumentKind.Payslip, 1,
                    [new("Angajator: Müller Logistik GmbH", IsSignal: true), new("Telefon: +49 89 4521 7730", EvidenceFor: "CRS-IND-PHONE")]),
            ],
            ExpectedRegime = Regime.Crs,
            // CRS-IND-PHONE holds: the card's After has "+40721384516 necurent", and p. 29 lit. c is read on current phones only.
            ExpectedIndicia = [new("CRS-IND-ADDR", "DE", "Addresses/Residence"), new("CRS-IND-PHONE", "DE", "Phones")],
            ExpectedReportableFor = [],
            ExpectedDeadline = new("DL-NEW-90", Preexisting: false, NewAccountDueAt, NewAccountRemindAt),
            Response = new(
                ReceivedAt: new(2027, 1, 22, 8, 0, 0, DateTimeKind.Utc), // day 86, after the reminder, before DueAt
                Channel: "online", Kind: ResponseKind.NewSelfCertification, Category: ExplanationCategory.None,
                SelfCertification: new(GoldenClients.Midnight(2027, 1, 21), [new("DE", "86095174223")], DeclaresUsPerson: false),
                // Everything SC-VALID asks for (p. 34), plus a signature. The card writes the same dates in ISO (1991-06-08,
                // 2027-01-21); the form prints them as dd/mm/yyyy, and the quotes must match the document.
                Document: new("autocertificare-maria-2027.pdf", DocumentKind.SelfCertForm, null,
                [
                    new("Maria Popescu"), new("Leopoldstraße 12, 80802 München"), new("08/06/1991"), new("86095174223"),
                    new("Confirm că datele sunt corecte și complete.", EvidenceFor: "SC-VALID"), new("21/01/2027"),
                ]),
                ExpectedDecision: Decision.Compliant,
                ExpectedIndicia:
                [
                    new("CRS-IND-ADDR", "DE", "Addresses/Residence", Coverage.SelfCertification),
                    new("CRS-IND-PHONE", "DE", "Phones", Coverage.SelfCertification),
                ],
                ExpectedReportableFor: ["DE"],
                RuleId: "SC-VALID", ExpectedStatus: CaseStatus.Closed, ProofChannel: ConfirmationChannel.Online),
        },
        new("5a", "Standing order to Germany from a current account", "CL-0005", GoldenOwner.R1, Decision.NoCase, [], ["CRS-IND-SO"], null, "No case: not an indicium on depository accounts (p. 29)")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.ApprovedR5,
            GuidePage = "29 d",
            Before = Snapshot("CL-0005"),
            After = Snapshot("CL-0005", c => AddOrder(c, RaduCurrentEur, KellerOrder())),
            // Summary added by R5, not on the card.
            Change = OrderChange(RaduCurrentEur, KellerOrder(),
                "Ordin permanent lunar din Cont curent EUR către Hausverwaltung Keller GmbH, Leipzig, DE02700100800918273645, 450,00 EUR (chiria fiicei, studentă)"),
            ReceivedAt = ChangeMoment,
            ExpectedClearedRules = [new("CRS-IND-SO", "DE", Reason: "ordin dintr-un cont de depozit, exceptat la CRS", GuidePage: "29 d")],
            ExpectedReportableFor = [], // derived, not on the card
        },
        new("5b", "Same standing order from a custodial account", "CL-0005", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-SO:DE"], [], "DL-NEW-90", "Clarification: indicium")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.ApprovedR5,
            GuidePage = "29 d",
            Before = Snapshot("CL-0005"),
            After = Snapshot("CL-0005", c => AddOrder(c, RaduCustodial, KellerOrder())),
            // Summary added by R5, not on the card.
            Change = OrderChange(RaduCustodial, KellerOrder(),
                "Ordin permanent lunar din Cont de instrumente financiare către Hausverwaltung Keller GmbH, Leipzig, DE02700100800918273645, 450,00 EUR (chiria fiicei, studentă)"),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Crs,
            ExpectedIndicia = [new("CRS-IND-SO", "DE", "StandingOrders")],
            ExpectedReportableFor = [], // derived, not on the card
            ExpectedDeadline = new("DL-NEW-90", Preexisting: false, NewAccountDueAt, NewAccountRemindAt),
        },
        new("5c", "Standing order to the US from a current account", "CL-0005", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-SO:US"], ["CRS-IND-SO"], null, "FATCA alert, no deadline in the guide")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.ApprovedR5,
            GuidePage = "31, 38",
            // The current EUR account is over the FATCA threshold for new depository accounts (p. 25 lit. b): see GoldenClients.
            Before = Snapshot("CL-0005"),
            After = Snapshot("CL-0005", c => AddOrder(c, RaduCurrentEur, ChicagoOrder())),
            // Summary added by R5, not on the card.
            Change = OrderChange(RaduCurrentEur, ChicagoOrder(),
                "Ordin permanent lunar din Cont curent EUR către Daniel Ionescu (fratele, Chicago), cont 4417209358, ABA 073909523 (SUA), 300,00 EUR"),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Fatca,
            ExpectedIndicia = [new("FATCA-IND-SO", "US")],
            ExpectedClearedRules =
            [
                // The guide's reason first (Î12, p. 29 lit. d); "SUA nu e jurisdicție CRS raportabilă" is not in the guide, so it comes after.
                new("CRS-IND-SO", "US", GuidePage: "29 d", Reason: "ordin dintr-un cont de depozit, exceptat la CRS; în plus, SUA nu e jurisdicție CRS raportabilă"),
            ],
            // No deadline in the guide (Î1): the case opens without one; DeadlineCalculator answers FATCA-SC, p. 38, DueAt null.
            ExpectedDeadline = new(null, Preexisting: false, null, null),
        },
        new("6", "Passport with place of birth in the US", "CL-0006", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-POB:US"], [], null, "FATCA alert")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "31, 38, 25",
            // The account is over the FATCA threshold for new depository accounts (Î9, p. 25 lit. b): see GoldenClients.
            Before = Snapshot("CL-0006"),
            // The engine golden has the place of birth in After already; extraction from the PDF is measured apart (the card).
            After = Snapshot("CL-0006", c => c.BirthCountry = "US"),
            // Summary added by R5, not on the card.
            Change = Upload("CL-0006", "Pașaport RO încărcat: locul nașterii Chicago, Illinois, S.U.A."),
            ReceivedAt = ChangeMoment,
            Documents =
            [
                new(SyntheticPdfs.PassportAlexandra, DocumentKind.Passport, null,
                    [new("Locul nașterii / Place of birth: CHICAGO, ILLINOIS, S.U.A.", EvidenceFor: "FATCA-IND-POB")]),
            ],
            ExpectedRegime = Regime.Fatca,
            ExpectedIndicia = [new("FATCA-IND-POB", "US")],
            // No deadline in the guide (Î1), as 5c.
            ExpectedDeadline = new(null, Preexisting: false, null, null),
        },
        new("7", "US citizenship (second passport)", "CL-0007", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-CIT:US"], ["FATCA-IND-POB"], null, "FATCA alert")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "31, 38, 25",
            // Over the FATCA threshold, as case 6 (Î9, p. 25 lit. b).
            Before = Snapshot("CL-0007"),
            After = Snapshot("CL-0007", c => c.Citizenships.Add("US")),
            // Summary added by R5, not on the card.
            Change = Upload("CL-0007", "Pașaport SUA încărcat: cetățenie SUA, locul nașterii România"),
            ReceivedAt = ChangeMoment,
            Documents =
            [
                new(SyntheticPdfs.PassportMarius, DocumentKind.Passport, null,
                    [new("Nationality: UNITED STATES OF AMERICA", EvidenceFor: "FATCA-IND-CIT"), new("Place of birth: ROMANIA")]),
            ],
            ExpectedRegime = Regime.Fatca,
            ExpectedIndicia = [new("FATCA-IND-CIT", "US")],
            ExpectedClearedRules = [new("FATCA-IND-POB", null, Reason: "născut în România", GuidePage: "31")],
            ExpectedDeadline = new(null, Preexisting: false, null, null),
        },
        new("8", "Andrei: salary from a German company", "CL-0013", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], null, "No case; one weak signal, explained")
        {
            Layer = GoldenLayer.Engine | GoldenLayer.Agent,
            Status = GoldenStatus.Validated,
            GuidePage = "29",
            // Andrei as in case 13's Before (RO phone only). Documents are not in the snapshot, so After = Before.
            Before = Snapshot("CL-0013"),
            After = Snapshot("CL-0013"),
            // Summary added by R5, not on the card.
            Change = Upload("CL-0013", "Fluturaș de salariu încărcat: septembrie 2026"),
            ReceivedAt = ChangeMoment,
            // One ContextSignal (R4), weak: "Salariu plătit de Brenner Software GmbH, Stuttgart". It never changes the decision.
            Documents = [new(SyntheticPdfs.PayslipAndrei, DocumentKind.Payslip, null, [new("Angajator: Brenner Software GmbH", IsSignal: true)])],
            // The explanation does not say he "rămâne în România" (the card).
            ExpectedClearedRules = [new("GEN-DOMESTIC", null, Reason: "venitul din străinătate nu e în lista din p. 29", GuidePage: "29")],
            ExpectedReportableFor = [], // derived, not on the card
        },
        new("9", "Dual residence RO + DE declared, address in DE", "CL-0009", GoldenOwner.R1, Decision.Compliant, ["CRS-IND-ADDR:DE"], [], null, "Compliant, reportable for DE")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "29 b; 33-34",
            Before = Snapshot("CL-0009"),
            After = Snapshot("CL-0009", c => c.Addresses.Add(SorinBerlin())),
            Change = new("CL-0009", ChangeKind.AddressAdded, "online", Address: Of(SorinBerlin()), // channel added: the card gives none
                Summary: "A doua reședință: Kastanienallee 30, 10435 Berlin (lucrează la Berlin în timpul săptămânii)"),
            ReceivedAt = ChangeMoment,
            // DE is declared in the self-certification on file; the explanation cites SC-CONSISTENT (p. 33-34).
            ExpectedIndicia = [new("CRS-IND-ADDR", "DE", CoveredBy: Coverage.SelfCertification)],
            ExpectedReportableFor = ["DE"],
        },
        new("10", "PDF with hidden white text", "CL-0010", GoldenOwner.R3, Decision.ShieldAlert, [], [], null, "Red signal from the shield; the text never reaches the model")
        {
            Layer = GoldenLayer.Shield,
            Status = GoldenStatus.Validated,
            GuidePage = "design",
            Before = Snapshot("CL-0010"),
            After = Snapshot("CL-0010"),
            // Summary added by R5, not on the card.
            Change = Upload("CL-0010", "Extras de cont încărcat: septembrie 2026"),
            ReceivedAt = ChangeMoment,
            // ShieldVerdict.HiddenTextRemoved, then ShieldAlert from ChangeService (never the engine). The fragment hidden on page 1
            // (SyntheticPdfs.Injection, white and 1 pt) must reach neither VisibleText nor the model, so it is no quote.
            Documents = [new(SyntheticPdfs.StatementVictor, DocumentKind.BankStatement, null, [])],
        },
        new("11", "Power of attorney for a person with an address in Austria", "CL-0011", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-POA:AT"], [], "DL-NEW-90", "Clarification: indicium")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "29 e",
            Before = Snapshot("CL-0011"),
            After = Snapshot("CL-0011", c => c.PowersOfAttorney.Add(MateiPowerOfAttorney())),
            // Summary added by R5, not on the card.
            Change = new("CL-0011", ChangeKind.PowerOfAttorneyGranted, "branch", PowerOfAttorney: Of(MateiPowerOfAttorney()),
                Summary: "Împuternicire pentru Matei Cazacu (fiul), Mariahilfer Straße 45, 1060 Wien: operațiuni pe contul curent"),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Crs,
            ExpectedIndicia = [new("CRS-IND-POA", "AT", "PowersOfAttorney")],
            ExpectedReportableFor = [], // derived, not on the card
            ExpectedDeadline = new("DL-NEW-90", Preexisting: false, NewAccountDueAt, NewAccountRemindAt),
        },
        new("12", "In-care-of address in Italy, the only current address (preexisting, low value)", "CL-0012", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-CO:IT", "CRS-IND-MAIL:IT"], ["FATCA-IND-CO"], "DL-PRE-YEAR", "Clarification: indicium")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "29 b, f; 31; 48",
            Before = Snapshot("CL-0012"),
            // A c/o address is not a residence (p. 48), so the letter also says he no longer lives in Cluj (the card, variant A):
            // the Cluj residence is no longer current and the c/o address is the only current one.
            After = Snapshot("CL-0012", c => MoveTo(c, PetruTorino())),
            Change = new("CL-0012", ChangeKind.AddressChanged, "poștă", Address: Of(PetruTorino()),
                Summary: "Scrisoare: „Nu mai locuiesc la Cluj. Vă rog să trimiteți corespondența la avocatul meu din Torino.”"),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Crs,
            // "No other address" (p. 29 lit. f) reads current addresses only (Î6, a design interpretation). A c/o address is also
            // a mailing address, so CRS-IND-MAIL holds too (Î10, p. 29 lit. b).
            ExpectedIndicia = [new("CRS-IND-CO", "IT"), new("CRS-IND-MAIL", "IT", GuidePage: "29 b")],
            ExpectedClearedRules =
            [
                new("FATCA-IND-CO", null, GuidePage: "31",
                    Reason: "cont preexistent cu valoare mică: o adresă «în grija» din afara SUA nu e indiciu FATCA"),
            ],
            ExpectedReportableFor = [], // derived, not on the card
            // max(2026-12-31, 2027-01-26): the 90 days win, so the same moment as a new account's deadline. "The later of" is our
            // interpretation on the indicia route (N1): the law says only "ori" (Anexa nr. 1, secț. III, subsecț. B, pct. 16), the
            // OECD Commentary "until the later of" (Section III, para. 16, p. 116).
            ExpectedDeadline = new("DL-PRE-YEAR", Preexisting: true, NewAccountDueAt, NewAccountRemindAt),
        },
        new("13", "Andrei: German phone next to the RO phone", "CL-0013", GoldenOwner.R1, Decision.NoCase, [], ["CRS-IND-PHONE"], null, "No case")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.ApprovedR5,
            GuidePage = "29 c",
            Before = Snapshot("CL-0013"),
            After = Snapshot("CL-0013", c => c.Phones.Add(AndreiGermanPhone())),
            // Summary added by R5, not on the card.
            Change = new("CL-0013", ChangeKind.PhoneAdded, "call center", Phone: Of(AndreiGermanPhone()),
                Summary: "Telefon nou +4915144201187 (DE), lângă telefonul RO"),
            ReceivedAt = ChangeMoment,
            ExpectedClearedRules = [new("CRS-IND-PHONE", "DE", Reason: "există și un telefon în România", GuidePage: "29 c")],
            ExpectedReportableFor = [], // derived, not on the card
        },
        new("14", "US phone next to the RO phone", "CL-0014", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-PHONE:US"], ["CRS-IND-PHONE"], null, "FATCA alert")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.Validated,
            GuidePage = "31, 38; 29 c; 25",
            // Over the FATCA threshold, as case 6 (Î9, p. 25 lit. b).
            Before = Snapshot("CL-0014"),
            After = Snapshot("CL-0014", c => c.Phones.Add(LucianUsPhone())),
            // Summary added by R5, not on the card.
            Change = new("CL-0014", ChangeKind.PhoneAdded, "online", Phone: Of(LucianUsPhone()), // channel added: the card gives none
                Summary: "Telefon nou +13125550147 (SUA), lângă telefonul RO"),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Fatca,
            ExpectedIndicia = [new("FATCA-IND-PHONE", "US")],
            // The guide's reason first (Î12, p. 29 lit. c), as 5c.
            ExpectedClearedRules =
            [
                new("CRS-IND-PHONE", "US", GuidePage: "29 c", Reason: "există și un telefon în România; în plus, SUA nu e jurisdicție CRS raportabilă"),
            ],
            ExpectedDeadline = new(null, Preexisting: false, null, null),
        },
        new("15", "Cluj to Iasi", "CL-0015", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], null, "No case, with an explanation")
        {
            Layer = GoldenLayer.Engine,
            Status = GoldenStatus.ApprovedR5,
            GuidePage = "design",
            Before = Snapshot("CL-0015"),
            After = Snapshot("CL-0015", c => MoveTo(c, IoanaIasi())),
            // Summary added by R5, not on the card.
            Change = new("CL-0015", ChangeKind.AddressChanged, "branch", Address: Of(IoanaIasi()),
                Summary: "Adresă nouă: Bd. Ștefan cel Mare și Sfânt 8, ap. 12, 700064 Iași"),
            ReceivedAt = ChangeMoment,
            ExpectedClearedRules = [new("GEN-DOMESTIC", null, Reason: "noua adresă e în România; nicio regulă din lista închisă nu se aplică")],
            ExpectedReportableFor = [], // derived, not on the card
        },
        new("16", "Move to Spain, new account, no answer, day 85", "CL-0016", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:ES"], [], "DL-NEW-90", "Reminded, then escalated")
        {
            Layer = GoldenLayer.Workflow,
            Status = GoldenStatus.Validated,
            GuidePage = "35-36",
            Before = Snapshot("CL-0016"),
            After = Snapshot("CL-0016", c => MoveTo(c, IrinaMadrid())),
            Change = IrinaMoves(),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Crs,
            ExpectedIndicia = [new("CRS-IND-ADDR", "ES")],
            ExpectedReportableFor = [], // derived, not on the card
            // Request approved (AwaitingClient), no answer: TickAsync(NewAccountRemindAt) gives Reminded and Escalated, once
            // (a second tick repeats neither).
            ExpectedDeadline = new("DL-NEW-90", Preexisting: false, NewAccountDueAt, NewAccountRemindAt),
        },
        new("17a", "Same case after the deadline", "CL-0016", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:ES"], [], "DL-EXPIRED-NEW", "Expired: resident in RO and ES")
        {
            Layer = GoldenLayer.Workflow,
            Status = GoldenStatus.Validated,
            GuidePage = "35-36",
            // Case 16, later: TickAsync(NewAccountDueAt + 1 s) gives Expired.
            Before = Snapshot("CL-0016"),
            After = Snapshot("CL-0016", c => MoveTo(c, IrinaMadrid())),
            Change = IrinaMoves(),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Crs,
            ExpectedIndicia = [new("CRS-IND-ADDR", "ES")],
            // After expiry (Case.ReportableFor): resident in RO (the initial self-certification) and in ES; RO is not reportable
            // for an RO resident. At the change the engine gives [], as in 16.
            ExpectedReportableFor = ["ES"],
            // The header names the expiry rule (Case.OutcomeRuleId); the deadline is case 16's.
            ExpectedDeadline = new("DL-EXPIRED-NEW", Preexisting: false, NewAccountDueAt, NewAccountRemindAt),
        },
        new("17b", "Move to Spain in March, preexisting account", "CL-0017", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:ES"], [], "DL-PRE-YEAR", "Deadline 31 Dec; on expiry resident at least in ES (DL-EXPIRED-PRE)")
        {
            Layer = GoldenLayer.Workflow,
            Status = GoldenStatus.Validated,
            GuidePage = "29",
            Before = Snapshot("CL-0017"),
            After = Snapshot("CL-0017", c => MoveTo(c, GheorgheBarcelona())),
            // Summary added by R5, not on the card.
            Change = new("CL-0017", ChangeKind.AddressChanged, "branch", Address: Of(GheorgheBarcelona()),
                Summary: "Adresă nouă: Avenida Diagonal 450, 08006 Barcelona"),
            ReceivedAt = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            ExpectedRegime = Regime.Crs,
            ExpectedIndicia = [new("CRS-IND-ADDR", "ES")],
            // On expiry (a tick after 31.12.2026), DL-EXPIRED-PRE: resident at least in ES. The card says ⊇ [ES]: compare as a subset.
            ExpectedReportableFor = ["ES"],
            // The later of 31.12 and ReceivedAt + 90 days (2026-06-08): our interpretation on the indicia route (N1, the card's
            // variant A), as case 12. 31.12 at 23:59:59 in Bucharest (Î5, UTC+2).
            ExpectedDeadline = new("DL-PRE-YEAR", Preexisting: true,
                new DateTime(2026, 12, 31, 21, 59, 59, DateTimeKind.Utc), new DateTime(2026, 12, 26, 21, 59, 59, DateTimeKind.Utc)),
        },
        new("18", "Answer in time: student", "CL-0018", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:DE"], [], "DL-NEW-90", "Compliant on re-evaluation, closed with proof; not reportable for DE")
        {
            Layer = GoldenLayer.Engine | GoldenLayer.Workflow,
            Status = GoldenStatus.Validated,
            GuidePage = "35, 37",
            Before = Snapshot("CL-0018"),
            After = Snapshot("CL-0018", c => MoveTo(c, TeodoraMunich())),
            // Summary added by R5, not on the card.
            Change = new("CL-0018", ChangeKind.AddressChanged, "online", Address: Of(TeodoraMunich()),
                Summary: "Adresă nouă: Helene-Mayer-Ring 9, 80809 München (cămin studențesc)"),
            ReceivedAt = ChangeMoment,
            ExpectedRegime = Regime.Crs,
            ExpectedIndicia = [new("CRS-IND-ADDR", "DE")],
            ExpectedReportableFor = [], // derived, not on the card
            ExpectedDeadline = new("DL-NEW-90", Preexisting: false, NewAccountDueAt, NewAccountRemindAt),
            // Re-evaluated on TeodoraAfterAnswer. EU citizens need no student visa in DE (Î7): the proof is the certificate.
            Response = new(
                ReceivedAt: TeodoraAnsweredAt,
                Channel: "online", Kind: ResponseKind.ReasonableExplanation, Category: ExplanationCategory.Student,
                SelfCertification: null,
                // The card's quote "Technische Universität München … immatrikuliert im Wintersemester 2026/27" is two fragments.
                Document: new(SyntheticPdfs.StudentTeodora, DocumentKind.Other, null,
                [
                    new("Technische Universität München", EvidenceFor: "RESP-EXPLANATION"),
                    new("immatrikuliert im Wintersemester 2026/27", EvidenceFor: "RESP-EXPLANATION"),
                ]),
                ExpectedDecision: Decision.Compliant,
                ExpectedIndicia: [new("CRS-IND-ADDR", "DE", CoveredBy: Coverage.AcceptedExplanation)],
                ExpectedReportableFor: [],
                RuleId: "RESP-EXPLANATION", ExpectedStatus: CaseStatus.Closed, ProofChannel: ConfirmationChannel.Online),
        },
    ];

    public static GoldenCase Get(string id) =>
        All.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"Unknown golden case {id}");

    /// <summary>The transcribed cases a layer runs, e.g. <c>Ready(GoldenLayer.Engine)</c> in GoldenEngineTests.</summary>
    public static IEnumerable<GoldenCase> Ready(GoldenLayer layer) => All.Where(c => c.HasInput && c.Layer.HasFlag(layer));

    /// <summary>The client of <see cref="GoldenClients"/>, after <paramref name="change"/> (none = Before).</summary>
    private static ClientSnapshot Snapshot(string code, Action<Client>? change = null)
    {
        var client = GoldenClients.Create(code);
        change?.Invoke(client);
        return ClientSnapshot.From(client);
    }

    /// <summary>A new residence: the current ones stop being current (AddressChanged).</summary>
    private static void MoveTo(Client client, Address residence)
    {
        foreach (var a in client.Addresses.Where(a => a.Kind == AddressKind.Residence)) a.IsCurrent = false;
        client.Addresses.Add(residence);
    }

    private static void ReplacePhones(Client client, Phone phone)
    {
        foreach (var p in client.Phones) p.IsCurrent = false;
        client.Phones.Add(phone);
    }

    private static void AddOrder(Client client, string fromIban, StandingOrder order) =>
        client.Accounts.Single(a => a.Iban == fromIban).StandingOrders.Add(order);

    private static ChangeRequest OrderChange(string fromIban, StandingOrder order, string summary) =>
        new("CL-0005", ChangeKind.StandingOrderCreated, "online", AccountIban: fromIban, StandingOrder: Of(order), Summary: summary);

    /// <summary>DocumentUploaded: the DocumentId is the database's, once the seed inserts the case's PDF. Channel added: the cards give none.</summary>
    private static ChangeRequest Upload(string code, string summary) => new(code, ChangeKind.DocumentUploaded, "online", Summary: summary);

    // Summary added by R5, not on the card.
    private static ChangeRequest IrinaMoves() => new("CL-0016", ChangeKind.AddressChanged, "online", Address: Of(IrinaMadrid()),
        Summary: "Adresă nouă: Calle de Alcalá 120, 28009 Madrid");

    private static AddressSnapshot Of(Address a) => new(a.Kind, a.Line, a.City, a.Country, a.IsCurrent);
    private static PhoneSnapshot Of(Phone p) => new(p.Number, p.Country, p.IsCurrent);
    private static StandingOrderSnapshot Of(StandingOrder o) => new(o.BeneficiaryAccount, o.BeneficiaryCountry, o.IsActive);
    private static PowerOfAttorneySnapshot Of(PowerOfAttorney p) => new(p.HolderName, p.HolderCountry, p.IsActive);

    private static Address MariaMunich() => new() { Kind = AddressKind.Residence, Line = "Leopoldstraße 12", City = "80802 München", Country = "DE", ValidFrom = new(2026, 10, 15) };
    private static Phone MariaGermanPhone() => new() { Number = "+498945217730", Country = "DE" };
    private static Phone AndreiGermanPhone() => new() { Number = "+4915144201187", Country = "DE" };
    // ValidFrom added by R5, not on the card.
    private static Address IoanaIasi() => new() { Kind = AddressKind.Residence, Line = "Bd. Ștefan cel Mare și Sfânt 8, ap. 12", City = "700064 Iași", Country = "RO", ValidFrom = new(2026, 10, 28) };
    private static Phone IoanaNewPhone() => new() { Number = "+40733902614", Country = "RO" };
    // ValidFrom is the day of the change where the card gives no date.
    private static Address SorinBerlin() => new() { Kind = AddressKind.Residence, Line = "Kastanienallee 30", City = "10435 Berlin", Country = "DE", ValidFrom = new(2026, 10, 28) };
    private static Address PetruTorino() => new() { Kind = AddressKind.InCareOf, Line = "c/o Studio Legale Bianchi, Via Roma 10", City = "10121 Torino", Country = "IT", ValidFrom = new(2026, 10, 28) };
    private static Phone LucianUsPhone() => new() { Number = "+13125550147", Country = "US" };
    private static Address IrinaMadrid() => new() { Kind = AddressKind.Residence, Line = "Calle de Alcalá 120", City = "28009 Madrid", Country = "ES", ValidFrom = new(2026, 10, 28) };
    private static Address GheorgheBarcelona() => new() { Kind = AddressKind.Residence, Line = "Avenida Diagonal 450", City = "08006 Barcelona", Country = "ES", ValidFrom = new(2026, 3, 10) };
    private static Address TeodoraMunich() => new() { Kind = AddressKind.Residence, Line = "Helene-Mayer-Ring 9", City = "80809 München", Country = "DE", ValidFrom = new(2026, 10, 28) };

    // The holder's relation (the son) is only in the change summary: the entity has no field for it.
    private static PowerOfAttorney MateiPowerOfAttorney() => new()
    {
        HolderName = "Matei Cazacu", HolderAddress = "Mariahilfer Straße 45, 1060 Wien", HolderCountry = "AT",
        Scope = "operațiuni pe contul curent", ValidFrom = new(2026, 10, 28),
    };

    // Monthly, active (the entity's defaults). The beneficiary's name is only in the change summary: the entity has no field for it.
    private static StandingOrder KellerOrder() => new() { BeneficiaryAccount = "DE02700100800918273645", BeneficiaryCountry = "DE", Amount = 450.00m };

    /// <summary>The US has no IBAN (Î2): the ABA routing number, a space, the account number; the country is explicit.</summary>
    private static StandingOrder ChicagoOrder() => new() { BeneficiaryAccount = "073909523 4417209358", BeneficiaryCountry = "US", Amount = 300.00m };
}

public record EvaluationRow(string CaseId, string Title, string Expected, string Actual, bool Passed, bool FalseAlarm, TimeSpan Duration);

public record EvaluationReport(DateTime RanAt, IReadOnlyList<EvaluationRow> Rows)
{
    public int Passed => Rows.Count(r => r.Passed);
    public int FalseAlarms => Rows.Count(r => r.FalseAlarm);
}

/// <summary>Runs the rules on the golden cases and the synthetic clients; the only source of the numbers on /metrics. Owner: R5.</summary>
public interface IEvaluationService
{
    Task<EvaluationReport> RunAsync(CancellationToken ct = default);
}

/// <summary>Phase 0 placeholder. R5 replaces it.</summary>
public sealed class EvaluationService : IEvaluationService
{
    public Task<EvaluationReport> RunAsync(CancellationToken ct = default) =>
        Task.FromResult(new EvaluationReport(DateTime.MinValue, []));
}

public static class EvaluationSetup
{
    public static IServiceCollection AddEvaluation(this IServiceCollection services) =>
        services.AddScoped<IEvaluationService, EvaluationService>();
}
