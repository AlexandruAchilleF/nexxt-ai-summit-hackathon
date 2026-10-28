namespace HackathonStarter.Features.Rules;

/// <summary>
/// The deterministic CRS / FATCA rules engine (docs/PLAN.md 8). Pure: no database, no model, no network, no clock. It
/// finds indicia, marks the ones a valid self-certification or an accepted explanation covers, and decides. It never
/// computes a deadline (R2) and never returns ShieldAlert (the shield does).
/// </summary>
public sealed partial class CrsRuleEngine(ITinFormatCheck? tinFormat = null)
{
    public const string Version = "crs-rules/1.0";

    private readonly ITinFormatCheck tins = tinFormat ?? new TinFormatNotChecked();

    public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

    /// <summary>Evaluates the whole client after the change (PLAN 8.3); before only says which indicia are new.</summary>
    public RuleOutcome Evaluate(RuleSubject before, RuleSubject after, RuleChange change)
    {
        var scope = AccountScope.Of(after);
        var found = Detect(after, scope);
        var earlier = Detect(before, AccountScope.Of(before)).Hits.Select(h => (h.RuleId, h.Jurisdiction)).ToHashSet();

        var sc = after.CurrentSelfCertification;
        var hits = found.Hits.Select(h => Cover(h, after, sc, earlier.Contains((h.RuleId, h.Jurisdiction)))).ToList();
        var cleared = found.ClearedWithoutHits().ToList();

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
        return new RuleOutcome(decision, hits, cleared, reportable, text, Version, textEn);
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
        return new RuleOutcome(decision, hits, found.ClearedWithoutHits(), reportable, text, Version, textEn);
    }

    private static Findings Detect(RuleSubject s, AccountScope scope)
    {
        var f = new Findings();
        CrsIndicia.Detect(s, scope, f);
        FatcaIndicia.Detect(s, scope, f);
        return f;
    }

    // CRS: a jurisdiction declared in the current valid self-certification covers it (reportable); an accepted explanation
    // covers it (not reportable). FATCA: only a self-certification declaring US status covers it; explanations never do.
    private static RuleHit Cover(RuleHit hit, RuleSubject s, SubjectSelfCertification? sc, bool existedBefore)
    {
        var (ro, en) = (hit.Explanation, hit.ExplanationEn);
        if (existedBefore)
            (ro, en) = (ro + " Indiciul exista și înainte de această schimbare.", en + " The indicium was already there before this change.");

        var declared = sc?.Residences.Select(r => Jurisdictions.Normalize(r.Country)).ToHashSet() ?? [];
        if (hit.Regime == Regime.Fatca)
        {
            if (sc is not null && (sc.DeclaresUsPerson || declared.Contains(Jurisdictions.UnitedStates)))
                return hit with
                {
                    CoveredBy = Coverage.SelfCertification,
                    Explanation = ro + $" Acoperit: autocertificarea din {sc.SignedAt:yyyy-MM-dd} declară statut de persoană SUA (cont raportabil SUA).",
                    ExplanationEn = en + $" Covered: the self-certification of {sc.SignedAt:yyyy-MM-dd} declares US person status (US reportable account).",
                };
            return hit with { Explanation = ro, ExplanationEn = en };
        }

        if (sc is not null && declared.Contains(hit.Jurisdiction))
            return hit with
            {
                CoveredBy = Coverage.SelfCertification,
                Explanation = ro + $" Acoperit: autocertificarea din {sc.SignedAt:yyyy-MM-dd} declară {Phrases.Place(hit.Jurisdiction)}.",
                ExplanationEn = en + $" Covered: the self-certification of {sc.SignedAt:yyyy-MM-dd} declares {Phrases.Place(hit.Jurisdiction, true)}.",
            };

        if (s.AcceptedExplanations.Any(e => Jurisdictions.Normalize(e.Jurisdiction) == hit.Jurisdiction))
            return hit with
            {
                CoveredBy = Coverage.AcceptedExplanation,
                Explanation = ro + " Acoperit: explicație rezonabilă acceptată.",
                ExplanationEn = en + " Covered: accepted reasonable explanation.",
            };

        return hit with { Explanation = ro, ExplanationEn = en };
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
        var us = sc.DeclaresUsPerson || sc.Residences.Any(r => Jurisdictions.IsUnitedStates(r.Country))
            ? [Jurisdictions.UnitedStates] : Array.Empty<string>();
        return [.. declared.Concat(us).Distinct()];
    }

    private static RuleHit Entry(string ruleId, string jurisdiction, string field, string ro, string en,
        RuleBasis basis = RuleBasis.Verified)
    {
        var rule = RuleCatalog.Get(ruleId);
        return new RuleHit(ruleId, rule.Regime, jurisdiction, ro, rule.GuidePage, field, Coverage.None, en, basis);
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
        const string deadlineRo = " Termenul îl calculează fluxul de cazuri, nu motorul.";
        const string deadlineEn = " The case workflow computes the deadline, not the engine.";

        return decision switch
        {
            Decision.FatcaAlert => (
                $"Alertă FATCA pe autocertificare: {open}. Formularul nu declară legătura cu SUA din dosar; pentru a înlătura indiciul, documentele din IGA Anexa I, II.B.4.",
                $"FATCA alert on the self-certification: {open}. The form does not declare the US link on file; to remove the indicium, the documents in IGA Annex I, II.B.4."),
            Decision.Clarification => (
                $"Autocertificarea nu poate fi acceptată așa: {open}. Se cere clientului corectarea (TIN, motiv sau formular nou).{deadlineRo}",
                $"The self-certification cannot be accepted as it is: {open}. The client is asked for a correction (TIN, reason or a new form).{deadlineEn}"),
            Decision.Compliant => (
                $"Autocertificare acceptată. Fără TIN, cu motiv acceptat și înregistrat: {accepted}.{reportableRo}",
                $"Self-certification accepted. No TIN, with an accepted and recorded reason: {accepted}.{reportableEn}"),
            _ => (
                $"Autocertificare acceptată: completă și consecventă cu dosarul.{reportableRo}",
                $"Self-certification accepted: complete and consistent with the file.{reportableEn}"),
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

        var onlyInCareOf = openCrs.Count > 0 && openCrs.All(h => h.RuleId == RuleIds.CrsInCareOf);
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
        const string deadlineRo = " Termenul îl calculează fluxul de cazuri, nu motorul.";
        const string deadlineEn = " The case workflow computes the deadline, not the engine.";

        return decision switch
        {
            Decision.FatcaAlert => (
                $"Alertă FATCA: {List(openFatca)} {NotCoveredRo(openFatca.Count)}. Se cere autocertificare nouă (persoană SUA sau nu); pentru a înlătura indiciul, documentele din IGA Anexa I, II.B.4. Fără autocertificare, contul e raportabil SUA; acordul nu dă un termen." +
                (crsRo.Length > 0 ? " " + crsRo : "") + reportableRo,
                $"FATCA alert: {List(openFatca)} {NotCoveredEn(openFatca.Count)}. A new self-certification is needed (US person or not); to remove the indicium, the documents in IGA Annex I, II.B.4. Without it, the account is US reportable; the agreement sets no deadline." +
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
