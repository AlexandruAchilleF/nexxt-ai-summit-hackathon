using HackathonStarter.Features.Tin;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// The deterministic CRS / FATCA rules engine (docs/PLAN.md 8). Pure: no database, no model, no network, no clock. It
/// finds indicia, marks the ones a valid self-certification or an accepted explanation covers, and decides. It never
/// computes a deadline (R2) and never returns ShieldAlert (the shield does). <paramref name="tinFormat"/> is R3's
/// synchronous, offline TIN format check (SC-TIN-FORMAT, SC-TIN-MISSING); without it every TIN is "format not checked".
/// </summary>
public sealed partial class CrsRuleEngine(ITinFormat? tinFormat = null) : ICrsRuleEngine
{
    public const string Version = "crs-rules/1.0";

    private readonly ITinFormat tins = tinFormat ?? new TinFormatNotChecked();

    public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

    /// <summary>Evaluates the whole client after the change (PLAN 8.3); before only says which indicia are new.</summary>
    public RuleOutcome Evaluate(RuleSubject before, RuleSubject after, RuleChange change) =>
        Run(after, change, Detect(before, AccountScope.Of(before)).Hits.Select(h => (h.RuleId, h.Jurisdiction)).ToHashSet(), null);

    /// <summary>The client's current state with no change (the agent's tool, metrics). Only a US-person form covers FATCA.</summary>
    public RuleOutcome EvaluateCurrent(RuleSubject client) => Run(client, new RuleChange(""), [], null);

    /// <summary>
    /// The current state of a client whose change the bank has known since <paramref name="changeKnownAt"/> (re-evaluation
    /// before closing a case). A self-certification signed that day or later is a new one, so a "not a US person" form can
    /// cover a FATCA indicium where the agreement allows it (<see cref="FatcaIndicia.CoveredByDenial"/>).
    /// </summary>
    public RuleOutcome EvaluateCurrent(RuleSubject client, DateTime changeKnownAt) => Run(client, new RuleChange(""), [], changeKnownAt);

    private RuleOutcome Run(RuleSubject after, RuleChange change, HashSet<(string RuleId, string Jurisdiction)> earlier,
        DateTime? changeKnownAt)
    {
        var scope = AccountScope.Of(after);
        var found = Detect(after, scope);

        var sc = after.CurrentSelfCertification;
        // By day: a form signed the day the change became known answers it. Both moments are read as Romanian calendar days
        // (01:00 in Bucharest is still the previous day in UTC); a date-only SignedAt at 00:00 UTC keeps its day.
        var renewed = sc is not null && changeKnownAt is { } knownAt && RomanianDay(sc.SignedAt) >= RomanianDay(knownAt);
        var hits = found.Hits.Select(h => Cover(h, after, sc, renewed, scope, earlier.Contains((h.RuleId, h.Jurisdiction)))).ToList();
        var cleared = found.ClearedWithoutHits().ToList();
        // The form on file declares US status without the US TIN: open until a form with it arrives (guide p. 38). Added after
        // Cover, because a form declaring US status covers the indicia, never its own missing TIN.
        if (sc is not null)
        {
            var usTin = new Findings();
            SelfCertRules.UsTinOnFile(sc, tins, usTin);
            hits.AddRange(usTin.Hits);
        }

        var fatca = hits.Where(h => h.Regime == Regime.Fatca).ToList();
        if (renewed && !DeclaresUs(sc!) && fatca.Count > 0 && fatca.All(h => h.CoveredBy == Coverage.SelfCertification))
            cleared.Add(Entry(RuleIds.FatcaChange, Jurisdictions.UnitedStates, "SelfCertification",
                $"Autocertificarea nouă din {sc!.SignedAt:yyyy-MM-dd} spune că titularul nu e persoană SUA și acoperă indiciile SUA: contul nu e raportabil SUA (IGA Anexa I, III.B.2; II.B.4).",
                $"The new self-certification of {sc.SignedAt:yyyy-MM-dd} says the holder is not a US person and covers the US indicia: the account is not US reportable (IGA Annex I, III.B.2; II.B.4).",
                fatca.All(h => h.Basis == RuleBasis.Verified) ? RuleBasis.Verified : RuleBasis.Interpretation));

        foreach (var declared in hits.Where(h => h.CoveredBy == Coverage.SelfCertification && h.Regime == Regime.Crs)
                     .Select(h => h.Jurisdiction).Distinct())
            cleared.Add(Entry(RuleIds.SelfCertConsistent, declared, "SelfCertification",
                $"Autocertificarea din {sc!.SignedAt:yyyy-MM-dd} declară {Phrases.Place(declared)}: indiciile sunt consecvente cu ea, iar contul e raportabil pentru {declared} (Sec. IV.B pct. 1).",
                $"The self-certification of {sc.SignedAt:yyyy-MM-dd} declares {Phrases.Place(declared, true)}: the indicia are consistent with it and the account is reportable for {declared} (IV.B(1))."));

        foreach (var explained in hits.Where(h => h.CoveredBy == Coverage.AcceptedExplanation).Select(h => h.Jurisdiction).Distinct())
        {
            var category = after.AcceptedExplanations.First(e => Jurisdictions.Normalize(e.Jurisdiction) == explained).Category;
            cleared.Add(Entry(RuleIds.Explanation, explained, "AcceptedExplanations",
                $"Explicație rezonabilă acceptată pentru {Phrases.Place(explained)} ({category}): indiciul e acoperit, iar {Phrases.Place(explained)} nu devine raportabilă (Sec. IV.C pct. 1 lit. b).",
                $"Reasonable explanation accepted for {Phrases.Place(explained, true)} ({category}): the indicium is covered and {Phrases.Place(explained, true)} does not become reportable (IV.C(1)(b))."));
        }

        if (hits.Count == 0 && cleared.Count == 0)
        {
            var (ro, en) = DomesticText(change);
            cleared.Add(Entry(RuleIds.Domestic, Jurisdictions.Romania, "", ro, en, RuleBasis.Interpretation));
        }

        var reportable = ReportableFor(sc);
        var decision = Decide(hits);
        var (text, textEn) = OutcomeText(decision, hits, cleared, reportable, scope);
        return new RuleOutcome(decision, hits, cleared, reportable, textEn, text, Version);
    }

    /// <summary>Checks a received self-certification against itself and the client's file (golden cases 2 and 3).</summary>
    public RuleOutcome EvaluateSelfCertification(RuleSubject client, SubjectSelfCertification selfCert)
    {
        var found = new Findings();
        SelfCertRules.Check(client, selfCert, tins, found);
        var hits = found.Hits.ToList();
        var decision = Decide(hits);
        var reportable = decision is Decision.NoCase or Decision.Compliant ? ReportableFor(selfCert) : [];
        var (text, textEn) = SelfCertText(decision, hits, reportable);
        return new RuleOutcome(decision, hits, found.ClearedWithoutHits(), reportable, textEn, text, Version);
    }

    private static Findings Detect(RuleSubject s, AccountScope scope)
    {
        var f = new Findings();
        CrsIndicia.Detect(s, scope, f);
        FatcaIndicia.Detect(s, scope, f);
        return f;
    }

    private static readonly TimeZoneInfo Bucharest =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Bucharest", out var zone) ? zone : TimeZoneInfo.Utc;

    private static DateTime RomanianDay(DateTime moment) =>
        TimeZoneInfo.ConvertTimeFromUtc(moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : DateTime.SpecifyKind(moment, DateTimeKind.Utc), Bucharest).Date;

    private static bool DeclaresUs(SubjectSelfCertification sc) =>
        sc.DeclaresUsPerson || sc.Residences.Any(r => Jurisdictions.IsUnitedStates(r.Country));

    // CRS: a jurisdiction declared in the current valid self-certification covers it (reportable); an accepted explanation
    // covers it (not reportable) only when no account is CRS-preexisting (decision Î11, guide p. 29-30, 35, 37). FATCA: a self-certification declaring US status covers it (reportable); a new one saying
    // "not a US person" only where the agreement allows (renewed, FatcaIndicia.CoveredByDenial); explanations never do.
    private static RuleHit Cover(RuleHit hit, RuleSubject s, SubjectSelfCertification? sc, bool renewed, AccountScope scope,
        bool existedBefore)
    {
        var (ro, en) = (hit.ExplanationRo, hit.Explanation);
        if (existedBefore)
            (ro, en) = (ro + " Indiciul exista și înainte de această schimbare.", en + " The indicium was already there before this change.");

        var declared = sc?.Residences.Select(r => Jurisdictions.Normalize(r.Country)).ToHashSet() ?? [];
        if (hit.Regime == Regime.Fatca)
        {
            if (sc is not null && DeclaresUs(sc))
                return hit with
                {
                    CoveredBy = Coverage.SelfCertification,
                    ExplanationRo = ro + $" Acoperit: autocertificarea din {sc.SignedAt:yyyy-MM-dd} declară statut de persoană SUA (cont raportabil SUA).",
                    Explanation = en + $" Covered: the self-certification of {sc.SignedAt:yyyy-MM-dd} declares US person status (US reportable account).",
                };
            if (sc is not null && renewed)
            {
                var (covers, basis, whyRo, whyEn) = FatcaIndicia.CoveredByDenial(hit, s, scope);
                return covers
                    ? hit with
                    {
                        CoveredBy = Coverage.SelfCertification,
                        Basis = basis ?? hit.Basis,
                        ExplanationRo = ro + $" Acoperit: autocertificarea nouă din {sc.SignedAt:yyyy-MM-dd} spune că titularul nu e persoană SUA; {whyRo}",
                        Explanation = en + $" Covered: the new self-certification of {sc.SignedAt:yyyy-MM-dd} says the holder is not a US person; {whyEn}",
                    }
                    : hit with
                    {
                        ExplanationRo = ro + $" Autocertificarea nouă din {sc.SignedAt:yyyy-MM-dd} spune că titularul nu e persoană SUA, dar nu ajunge singură: {whyRo}",
                        Explanation = en + $" The new self-certification of {sc.SignedAt:yyyy-MM-dd} says the holder is not a US person, but it is not enough alone: {whyEn}",
                    };
            }
            return hit with { Explanation = en, ExplanationRo = ro };
        }

        if (sc is not null && declared.Contains(hit.Jurisdiction))
            return hit with
            {
                CoveredBy = Coverage.SelfCertification,
                ExplanationRo = ro + $" Acoperit: autocertificarea din {sc.SignedAt:yyyy-MM-dd} declară {Phrases.Place(hit.Jurisdiction)}.",
                Explanation = en + $" Covered: the self-certification of {sc.SignedAt:yyyy-MM-dd} declares {Phrases.Place(hit.Jurisdiction, true)}.",
            };

        if (s.AcceptedExplanations.Any(e => Jurisdictions.Normalize(e.Jurisdiction) == hit.Jurisdiction))
            return scope.CrsPreexisting
                ? hit with
                {
                    ExplanationRo = ro + " Clientul are un cont preexistent CRS: explicația acceptată nu acoperă indiciul, pentru că explicația rezonabilă e o rută a conturilor noi (p. 35, 37; GOLDEN-CASES Î11). Pentru contul preexistent, ruta e cea din concluzie (p. 29-31).",
                    Explanation = en + " The client has a CRS preexisting account: the accepted explanation does not cover the indicium, because the reasonable explanation is a new-account route (p. 35, 37; GOLDEN-CASES Q11). For the preexisting account, the route is the one in the conclusion (p. 29-31).",
                }
                : hit with
                {
                    CoveredBy = Coverage.AcceptedExplanation,
                    ExplanationRo = ro + " Acoperit: explicație rezonabilă acceptată.",
                    Explanation = en + " Covered: accepted reasonable explanation.",
                };

        return hit with { Explanation = en, ExplanationRo = ro };
    }

    private static Decision Decide(IReadOnlyList<RuleHit> hits)
    {
        var open = hits.Where(h => h.CoveredBy == Coverage.None).ToList();
        if (open.Any(h => h.Regime == Regime.Fatca)) return Decision.FatcaAlert;
        if (open.Count > 0) return Decision.Clarification;
        return hits.Count > 0 ? Decision.Compliant : Decision.NoCase;
    }

    /// <summary>Every CRS-reportable jurisdiction declared in the self-certification (IV.B pct. 1), plus "US" for a US person.</summary>
    private static IReadOnlyList<string> ReportableFor(SubjectSelfCertification? sc)
    {
        if (sc is null) return [];
        var declared = sc.Residences.Select(r => Jurisdictions.Normalize(r.Country)).Where(Jurisdictions.IsCrsReportable);
        var us = DeclaresUs(sc) ? [Jurisdictions.UnitedStates] : Array.Empty<string>();
        return [.. declared.Concat(us).Distinct()];
    }

    private static RuleHit Entry(string ruleId, string jurisdiction, string field, string ro, string en,
        RuleBasis basis = RuleBasis.Verified)
    {
        var rule = RuleCatalog.Get(ruleId);
        return new RuleHit(ruleId, rule.Regime, jurisdiction, en, ro, rule.GuidePage, field, Coverage.None, basis);
    }

    private static (string Ro, string En) DomesticText(RuleChange change) => change.Kind switch
    {
        "AddressChanged" or "AddressAdded" =>
            ("Noua adresă e în România: nicio regulă din lista închisă de indicii nu se aplică.",
             "The new address is in Romania: no rule on the closed list of indicia applies."),
        "PhoneAdded" or "PhoneChanged" =>
            ("Noul telefon e din România: nicio regulă din lista închisă de indicii nu se aplică.",
             "The new phone is Romanian: no rule on the closed list of indicia applies."),
        "DocumentUploaded" =>
            ("Documentul nu schimbă nicio dată din lista închisă de indicii; ce reiese din el (ex. un angajator străin) e cel mult un semnal, nu un indiciu.",
             "The document changes nothing on the closed list of indicia; what it shows (e.g. a foreign employer) is at most a signal, not an indicium."),
        _ =>
            ("Datele din lista închisă de indicii rămân în România: nicio regulă nu se aplică.",
             "Everything on the closed list of indicia stays in Romania: no rule applies."),
    };

    private static string List(IEnumerable<RuleHit> items) =>
        string.Join(", ", items.Select(h => h.Jurisdiction.Length > 0 ? $"{h.RuleId} ({h.Jurisdiction})" : h.RuleId));

    // EvaluateSelfCertification: the findings are about the form, so the route is a corrected form, not an explanation.
    private static (string Ro, string En) SelfCertText(Decision decision, IReadOnlyList<RuleHit> hits, IReadOnlyList<string> reportable)
    {
        var open = List(hits.Where(h => h.CoveredBy == Coverage.None));
        var accepted = List(hits.Where(h => h.CoveredBy != Coverage.None));
        var reportableRo = reportable.Count > 0 ? $" Raportabil pentru: {string.Join(", ", reportable)}." : "";
        var reportableEn = reportable.Count > 0 ? $" Reportable for: {string.Join(", ", reportable)}." : "";
        const string deadlineRo = " Motorul nu calculează termene.";
        const string deadlineEn = " The engine computes no deadline.";

        var openHits = hits.Where(h => h.CoveredBy == Coverage.None).ToList();
        var contradicted = openHits.Any(h => h.RuleId == RuleIds.FatcaChange);
        var usTinMissing = openHits.Any(h => h.RuleId == RuleIds.FatcaTin && h.Basis == RuleBasis.Verified);
        var usTinFormat = openHits.Any(h => h.RuleId == RuleIds.FatcaTin && h.Basis != RuleBasis.Verified);
        var fatcaRo = (contradicted ? " Formularul nu declară legătura cu SUA din dosar (IGA Anexa I, II.B.4 și VI.A)." : "") +
                      (usTinMissing ? " Se cere o autocertificare cu TIN-ul SUA (ghid p. 38; IGA Anexa I, III.B.1)." : "") +
                      (usTinFormat ? " TIN-ul SUA declarat nu are formatul unui TIN SUA: se cere clarificare (politica băncii; verificare de format, nu de titular)." : "") +
                      " Acordul nu dă un termen.";
        var fatcaEn = (contradicted ? " The form does not declare the US link on file (IGA Annex I, II.B.4 and VI.A)." : "") +
                      (usTinMissing ? " A self-certification with the US TIN is needed (guide p. 38; IGA Annex I, III.B.1)." : "") +
                      (usTinFormat ? " The declared US TIN does not match a US TIN format: clarification is needed (bank policy; a format check, not an ownership check)." : "") +
                      " The agreement sets no deadline.";
        // An unrecognized code first goes to the operator, who compares it with the ID document (guide p. 36).
        var operatorRo = openHits.Any(h => h.RuleId == RuleIds.SelfCertValid && h.Basis == RuleBasis.Policy)
            ? " Operatorul compară întâi jurisdicția cu actul de identitate (ghid p. 36)." : "";
        var operatorEn = operatorRo.Length > 0 ? " The operator first compares the jurisdiction with the ID document (guide p. 36)." : "";
        var openCrs = List(openHits.Where(h => h.Regime == Regime.Crs));
        var crsRo = $"{operatorRo} Se cere clientului corectarea (TIN, motiv sau formular nou).{deadlineRo}";
        var crsEn = $"{operatorEn} The client is asked for a correction (TIN, reason or a new form).{deadlineEn}";

        return decision switch
        {
            Decision.FatcaAlert => (
                $"Alertă FATCA pe autocertificare: {open}.{fatcaRo}" + (openCrs.Length > 0 ? $" CRS: {openCrs}.{crsRo}" : ""),
                $"FATCA alert on the self-certification: {open}.{fatcaEn}" + (openCrs.Length > 0 ? $" CRS: {openCrs}.{crsEn}" : "")),
            Decision.Clarification => (
                $"Autocertificarea nu poate fi acceptată așa: {open}.{crsRo}",
                $"The self-certification cannot be accepted as it is: {open}.{crsEn}"),
            Decision.Compliant => (
                $"Autocertificare acceptată. Fără TIN, cu motiv acceptat și înregistrat: {accepted}.{reportableRo}",
                $"Self-certification accepted. No TIN, with an accepted and recorded reason: {accepted}.{reportableEn}"),
            _ => (
                $"Autocertificare acceptată: codul nu a găsit nicio problemă (validitate, TIN, consecvență cu dosarul).{reportableRo}",
                $"Self-certification accepted: code found no problem (validity, TIN, consistency with the file).{reportableEn}"),
        };
    }

    private static (string Ro, string En) OutcomeText(Decision decision, IReadOnlyList<RuleHit> hits,
        IReadOnlyList<RuleHit> cleared, IReadOnlyList<string> reportable, AccountScope scope)
    {
        static string NotCoveredRo(int count) => count == 1 ? "nu e acoperit" : "nu sunt acoperite";
        static string NotCoveredEn(int count) => count == 1 ? "is not covered" : "are not covered";

        var open = hits.Where(h => h.CoveredBy == Coverage.None).ToList();
        var consistent = cleared.Any(c => c.RuleId == RuleIds.SelfCertConsistent);
        var openCrs = open.Where(h => h.Regime == Regime.Crs).ToList();
        var openFatca = open.Where(h => h.Regime == Regime.Fatca).ToList();
        var reportableRo = reportable.Count > 0 ? $" Raportabil pentru: {string.Join(", ", reportable)}." : "";
        var reportableEn = reportable.Count > 0 ? $" Reportable for: {string.Join(", ", reportable)}." : "";

        // The c/o route (p. 31) also when the same c/o address gives CRS-IND-MAIL (decision Î10): it is not "another indicium".
        var onlyInCareOf = openCrs.Count > 0 && openCrs.Any(h => h.RuleId == RuleIds.CrsInCareOf)
            && openCrs.All(h => h.RuleId == RuleIds.CrsInCareOf || h is { RuleId: RuleIds.CrsMailing, SourceField: "Addresses/InCareOf" });
        var (crsRouteRo, crsRouteEn) = (scope.CrsPreexisting, onlyInCareOf) switch
        {
            (true, true) => (
                "Cont preexistent: căutare în registrul pe hârtie sau autocertificare / documente (Sec. III.B pct. 17); altfel, cont nedocumentat.",
                "Preexisting account: paper record search or self-certification / documents (III.B(17)); otherwise an undocumented account."),
            (true, false) => (
                "Cont preexistent: autocertificare și document justificativ (Sec. III.B pct. 19), altfel titularul e tratat ca rezident acolo.",
                "Preexisting account: self-certification and documentary evidence (III.B(19)), otherwise the holder is treated as resident there."),
            _ => (
                "Cont nou: autocertificare nouă sau explicație rezonabilă cu documente (Sec. IV.C pct. 1).",
                "New account: a new self-certification or a reasonable explanation with documents (IV.C(1))."),
        };
        var crsRo = openCrs.Count > 0 ? $"Clarificare CRS: {List(openCrs)} {NotCoveredRo(openCrs.Count)} de autocertificarea actuală. {crsRouteRo}" : "";
        var crsEn = openCrs.Count > 0 ? $"CRS clarification: {List(openCrs)} {NotCoveredEn(openCrs.Count)} by the current self-certification. {crsRouteEn}" : "";
        // IGA Annex I: new account, a new valid self-certification (III.B.2); preexisting, US reportable unless II.B.4 applies.
        var (fatcaRouteRo, fatcaRouteEn) = openFatca.All(h => h.RuleId == RuleIds.FatcaTin)
            ? ("Autocertificarea la dosar declară statut SUA fără TIN-ul SUA: se cere o autocertificare cu TIN-ul SUA (ghid p. 38; IGA Anexa I, III.B.1).",
               "The self-certification on file declares US status without the US TIN: a self-certification with the US TIN is needed (guide p. 38; IGA Annex I, III.B.1).")
            : scope.FatcaPreexisting
            ? ("Cont preexistent: contul e raportabil SUA, dacă banca nu obține ce cere IGA Anexa I, II.B.4 pentru fiecare indiciu (autocertificare «nu sunt persoană SUA» și, după indiciu, documente justificative).",
               "Preexisting account: the account is US reportable unless the bank obtains what IGA Annex I, II.B.4 asks for each indicium (a \"not a US person\" self-certification and, depending on the indicium, documentary evidence).")
            : ("Cont nou: se cere o autocertificare nouă validă care stabilește dacă titularul e persoană SUA (IGA Anexa I, III.B.2); fără ea, contul e raportabil SUA.",
               "New account: a new valid self-certification is needed that establishes whether the holder is a US person (IGA Annex I, III.B.2); without it, the account is US reportable.");
        const string deadlineRo = " Termenul îl calculează fluxul de cazuri, nu motorul.";
        const string deadlineEn = " The case workflow computes the deadline, not the engine.";

        return decision switch
        {
            Decision.FatcaAlert => (
                $"Alertă FATCA: {List(openFatca)} {NotCoveredRo(openFatca.Count)}. {fatcaRouteRo} Acordul nu dă un termen." +
                (crsRo.Length > 0 ? " " + crsRo : "") + reportableRo,
                $"FATCA alert: {List(openFatca)} {NotCoveredEn(openFatca.Count)}. {fatcaRouteEn} The agreement sets no deadline." +
                (crsEn.Length > 0 ? " " + crsEn : "") + reportableEn),
            Decision.Clarification => (crsRo + deadlineRo + reportableRo, crsEn + deadlineEn + reportableEn),
            Decision.Compliant => (
                $"Indicii găsite, toate acoperite: {List(hits)}." +
                (consistent ? " Autocertificarea e consecventă cu dosarul (SC-CONSISTENT)." : "") + reportableRo,
                $"Indicia found, all covered: {List(hits)}." +
                (consistent ? " The self-certification is consistent with the file (SC-CONSISTENT)." : "") + reportableEn),
            _ => (
                $"Nu se deschide caz: nicio regulă de indiciu nu se aplică. Verificat și neaplicat: {List(cleared)}.{reportableRo}",
                $"No case: no indicium rule applies. Checked and not applied: {List(cleared)}.{reportableEn}"),
        };
    }
}
