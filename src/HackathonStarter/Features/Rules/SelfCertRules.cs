using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Tin;
using static HackathonStarter.Features.Rules.Phrases;

namespace HackathonStarter.Features.Rules;

/// <summary>Used when no <see cref="ITinFormat"/> is given: every TIN is "not checked", so SC-TIN-FORMAT never fires.</summary>
internal sealed class TinFormatNotChecked : ITinFormat
{
    public TinCheck Check(string country, string tin) =>
        new(country.ToUpperInvariant(), tin.Trim(), false, TinCheckSource.NotChecked, "No TIN format check registered.");

    public bool? IssuesTinToAllResidents(string country) => null;
}

/// <summary>
/// Checks on a self-certification (CPF Anexa 1, Sec. IV.B-IV.C, VII.A; guide p. 33-37): validity elements, TIN per
/// reportable jurisdiction (or a reason), and reasonableness against what the bank already knows.
/// </summary>
internal static class SelfCertRules
{
    public static void Check(RuleSubject client, SubjectSelfCertification sc, ITinFormat tins, Findings f)
    {
        Validity(sc, f);
        foreach (var residence in sc.Residences) Tin(residence, tins, f);
        UsPersonWithoutUsResidence(sc, f);
        Consistency(client, sc, f);
    }

    // A row of the form with nothing in it (the p. 53 form has three); ignored next to a filled-in row (policy).
    private static bool IsBlank(SubjectTaxResidence r) => string.IsNullOrWhiteSpace(r.Country) && string.IsNullOrWhiteSpace(r.Tin)
        && r.NoTinReason == NoTinReason.None && string.IsNullOrWhiteSpace(r.NoTinExplanation);

    // IV.B pct. 3: signed or confirmed, dated, name, residence address, jurisdiction(s), date of birth.
    private static void Validity(SubjectSelfCertification sc, Findings f)
    {
        var missing = new List<(string Ro, string En)>();
        if (!sc.SignedOrConfirmed) missing.Add(("semnătura sau confirmarea", "signature or confirmation"));
        if (sc.SignedAt == default) missing.Add(("data", "date"));
        if (sc.FormFieldsKnown && string.IsNullOrWhiteSpace(sc.FullName)) missing.Add(("numele", "name"));
        if (sc.FormFieldsKnown && string.IsNullOrWhiteSpace(sc.ResidenceAddress)) missing.Add(("adresa de rezidență", "residence address"));
        if (sc.FormFieldsKnown && sc.BirthDate is null) missing.Add(("data nașterii", "date of birth"));

        // Guide p. 34: the form names the jurisdiction(s); p. 36: a missing one is never a minor error, so the file cannot
        // cure it (Verified). A row with a TIN or a reason but no country: the same note, read row by row (our reading). A code
        // that is no real jurisdiction: p. 36 lets the bank accept an abbreviation an ID document reasonably matches, which
        // the engine cannot see, so the operator decides (policy).
        var rows = sc.Residences.Where(r => !IsBlank(r)).ToList();
        var none = rows.All(r => string.IsNullOrWhiteSpace(r.Country));
        var rowWithoutCountry = !none && rows.Any(r => string.IsNullOrWhiteSpace(r.Country));
        var unknown = rows.Where(r => !string.IsNullOrWhiteSpace(r.Country) && !Jurisdictions.IsKnown(r.Country))
            .Select(r => r.Country.Trim()).Distinct().ToList();
        var basis = missing.Count > 0 || none ? RuleBasis.Verified
            : rowWithoutCountry ? RuleBasis.Interpretation
            : RuleBasis.Policy;
        if (none) missing.Add(("jurisdicția de rezidență", "jurisdiction of residence"));
        if (rowWithoutCountry)
            missing.Add(("jurisdicția de rezidență pe un rând cu TIN sau motiv", "jurisdiction of residence on a row with a TIN or a reason"));
        var (unknownRo, unknownEn) = ("", "");
        if (unknown.Count > 0)
        {
            var codes = string.Join(", ", unknown.Select(c => $"«{c}»"));
            (unknownRo, unknownEn) = unknown.Count == 1
                ? ($"jurisdicția de rezidență {codes} nu e un cod de țară ISO 3166-1", $"the jurisdiction of residence {codes} is not an ISO 3166-1 country code")
                : ($"jurisdicțiile de rezidență {codes} nu sunt coduri de țară ISO 3166-1", $"the jurisdictions of residence {codes} are not ISO 3166-1 country codes");
            (unknownRo, unknownEn) = (
                $"Autocertificarea nu poate fi acceptată fără verificare: {unknownRo}; operatorul o compară cu actul de identitate (ghid p. 36; politică).",
                $"The self-certification cannot be accepted without a check: {unknownEn}; the operator compares it with the ID document (guide p. 36; policy).");
        }

        if (missing.Count == 0 && unknownRo.Length == 0 && !sc.FormFieldsKnown)
            f.Clear(RuleIds.SelfCertValid, "", "SelfCertification",
                "Autocertificarea are dată și jurisdicții. Semnătura sau confirmarea, numele, adresa și data nașterii din formular nu au ajuns la motor: le verifică operatorul pe document (Sec. IV.B pct. 3).",
                "The self-certification has a date and jurisdictions. The form's signature or confirmation, name, address and date of birth did not reach the engine: the operator checks them on the document (IV.B(3)).",
                RuleBasis.Policy);
        else if (missing.Count == 0 && unknownRo.Length == 0)
            f.Clear(RuleIds.SelfCertValid, "", "SelfCertification",
                "Autocertificarea are toate elementele cerute: semnătură / confirmare, dată, nume, adresă, jurisdicții, data nașterii (Sec. IV.B pct. 3).",
                "The self-certification has every required element: signature / confirmation, date, name, address, jurisdictions, date of birth (IV.B(3)).");
        else
        {
            var (ro, en) = missing.Count > 0
                ? ($"Autocertificarea nu e validă, lipsește: {string.Join(", ", missing.Select(m => m.Ro))} (Sec. IV.B pct. 3).",
                   $"The self-certification is not valid, missing: {string.Join(", ", missing.Select(m => m.En))} (IV.B(3)).")
                : ("", "");
            f.Hit(RuleIds.SelfCertValid, "", "SelfCertification", $"{ro} {unknownRo}".Trim(), $"{en} {unknownEn}".Trim(), basis);
        }
    }

    // A TIN for each reportable jurisdiction (and the US), or a reason; the format is R3's check. NotChecked (no rules for
    // the country yet) is "not checked", never "wrong format": FormatValid means nothing then.
    private static bool? FormatValid(TinCheck check) => check.Source == TinCheckSource.NotChecked ? null : check.FormatValid;

    private static void Tin(SubjectTaxResidence residence, ITinFormat tins, Findings f)
    {
        var country = Jurisdictions.Normalize(residence.Country);
        if (!Jurisdictions.IsCrsReportable(country) && !Jurisdictions.IsUnitedStates(country)) return;
        var place = Place(country);
        var placeEn = Place(country, true);

        if (!string.IsNullOrWhiteSpace(residence.Tin))
        {
            // The US TIN is a FATCA requirement (guide p. 38): every result about it sits under FATCA-TIN, never a CRS rule.
            var formatRule = Jurisdictions.IsUnitedStates(country) ? RuleIds.FatcaTin : RuleIds.TinFormat;
            switch (FormatValid(tins.Check(country, residence.Tin.Trim())))
            {
                case false when Jurisdictions.IsUnitedStates(country):
                    f.Hit(RuleIds.FatcaTin, country, "SelfCertification/Tin",
                        "TIN-ul SUA declarat nu are formatul unui TIN SUA (verificare de format, nu de titular): banca nu se poate baza pe el fără clarificare (IGA Anexa I, VI.A). Acordul nu dă un termen.",
                        "The declared US TIN does not match a US TIN format (a format check, not an ownership check): the bank may not rely on it without clarification (IGA Annex I, VI.A). The agreement sets no deadline.",
                        RuleBasis.Policy);
                    break;
                case false:
                    f.Hit(RuleIds.TinFormat, country, "SelfCertification/Tin",
                        $"TIN-ul declarat pentru {place} nu are formatul acestei țări (verificare de format, nu de titular): e nevoie de clarificare.",
                        $"The TIN declared for {placeEn} does not match that country's format (a format check, not an ownership check): clarification needed.",
                        RuleBasis.Policy);
                    break;
                case true:
                    f.Clear(formatRule, country, "SelfCertification/Tin",
                        $"TIN pentru {place}: format valid. Formatul nu confirmă că TIN-ul aparține clientului.",
                        $"TIN for {placeEn}: format valid. The format does not confirm the TIN belongs to the client.");
                    break;
                default:
                    f.Clear(formatRule, country, "SelfCertification/Tin",
                        $"TIN pentru {place}: formatul nu a fost verificat (nu avem regulile acestei țări).",
                        $"TIN for {placeEn}: format not checked (no rules for this country).", RuleBasis.Policy);
                    break;
            }
            return;
        }

        if (Jurisdictions.IsUnitedStates(country))
        {
            UsTinMissing(f);
            return;
        }

        // No TIN: guide p. 37, the form is unreliable when the OECD sheet says the jurisdiction issues TINs to all residents,
        // whatever the reason. Otherwise reason C (p. 53: domestic law does not require the TIN) or A / B where the sheet says
        // it does not issue them to everyone. R3's OECD sheets first; our table only where R3 has no answer yet.
        var issuesToAll = tins.IssuesTinToAllResidents(country) ?? Jurisdictions.IssuesTinToAll(country);
        var hasExplanation = !string.IsNullOrWhiteSpace(residence.NoTinExplanation);
        switch (residence.NoTinReason)
        {
            case NoTinReason.None:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Lipsește TIN-ul pentru {place} și nu e dat niciun motiv (A, B sau C).",
                    $"The TIN for {placeEn} is missing and no reason (A, B or C) is given.");
                break;
            case NoTinReason.B_UnableToObtain when !hasExplanation:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Fără TIN pentru {place}, motiv B, dar fără explicația cerută de formular.",
                    $"No TIN for {placeEn}, reason B, but without the explanation the form asks for.");
                break;
            case var _ when issuesToAll == true:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Fără TIN pentru {place}, deși fișa OCDE spune că {place} emite TIN tuturor persoanelor fizice: banca are motive să știe că autocertificarea e nesigură (Sec. VII.A pct. 3 lit. a) pct. (iv); ghid p. 37).",
                    $"No TIN for {placeEn}, although the OECD sheet says {placeEn} issues TINs to all individuals: the bank has reason to know the self-certification is unreliable (VII.A(3)(a)(iv); guide p. 37).");
                break;
            case NoTinReason.C_NotRequired:
                Accepted(f, country,
                    $"Fără TIN pentru {place}, motiv C: clientul declară că legea din {place} nu cere colectarea TIN-ului (ghid p. 53). Motorul nu cunoaște legea acelei țări: confirmă operatorul.",
                    $"No TIN for {placeEn}, reason C: the client states that the law of {placeEn} does not require collecting the TIN (guide p. 53). The engine does not know that country's law: the operator confirms.",
                    RuleBasis.Interpretation);
                break;
            case var _ when issuesToAll == false:
                var letter = residence.NoTinReason == NoTinReason.A_CountryDoesNotIssue ? "A" : "B";
                Accepted(f, country,
                    $"Fără TIN pentru {place}, motiv {letter}: fișa OCDE spune că {place} nu emite TIN tuturor persoanelor fizice. Motivul e înregistrat.",
                    $"No TIN for {placeEn}, reason {letter}: the OECD sheet says {placeEn} does not issue TINs to all individuals. The reason is recorded.",
                    RuleBasis.Interpretation); // A or B: which one fits is open (GOLDEN-CASES Q8); both are accepted
                break;
            default:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Fără TIN pentru {place}, cu motiv, dar fișa OCDE pentru {place} nu e în catalog: verifică operatorul.",
                    $"No TIN for {placeEn}, with a reason, but the OECD sheet for {placeEn} is not in the catalog: the operator checks.",
                    RuleBasis.Policy);
                break;
        }
    }

    // Guide p. 38; IGA Annex I, III.B.1 and art. 2(2)(a)(1): a US resident's (a US citizen counts as one) self-certification
    // includes the US TIN, on a W-9 or a similar agreed form, and the account is US reportable. FATCA, not CRS: no 90 days.
    private static void UsTinMissing(Findings f) =>
        f.Hit(RuleIds.FatcaTin, Jurisdictions.UnitedStates, "SelfCertification/Tin",
            "Lipsește TIN-ul SUA: autocertificarea unei persoane SUA trebuie să includă TIN-ul din SUA (formularul W-9 sau un formular similar agreat), iar contul se tratează drept raportabil SUA (ghid p. 38; IGA Anexa I, III.B.1). Motivele A, B și C de la CRS nu îl înlocuiesc. Acordul nu dă un termen.",
            "The US TIN is missing: a US person's self-certification must include the US TIN (Form W-9 or a similar agreed form), and the account is treated as US reportable (guide p. 38; IGA Annex I, III.B.1). The CRS reasons A, B and C do not replace it. The agreement sets no deadline.");

    // A form declaring US person status (e.g. a US citizen resident in Romania) with no US row has no US TIN either.
    private static void UsPersonWithoutUsResidence(SubjectSelfCertification sc, Findings f)
    {
        if (sc.DeclaresUsPerson && !sc.Residences.Any(r => Jurisdictions.IsUnitedStates(r.Country))) UsTinMissing(f);
    }

    /// <summary>
    /// The US TIN of the form on file, for re-evaluation (EvaluateCurrent, Evaluate): FATCA-TIN when the form declares US
    /// status (a US row or DeclaresUsPerson) without a US TIN, or with one in the wrong format. Cleared entries are not repeated.
    /// </summary>
    public static void UsTinOnFile(SubjectSelfCertification sc, ITinFormat tins, Findings f)
    {
        var us = sc.Residences.Where(r => Jurisdictions.IsUnitedStates(r.Country)).ToList();
        if (us.Count == 0) UsPersonWithoutUsResidence(sc, f);
        else foreach (var residence in us) Tin(residence, tins, f);
        f.Cleared.Clear();
    }

    private static void Accepted(Findings f, string country, string ro, string en, RuleBasis basis = RuleBasis.Verified)
    {
        f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin", ro, en, basis);
        var index = f.Hits.FindIndex(h => h.RuleId == RuleIds.TinMissing && h.Jurisdiction == country);
        f.Hits[index] = f.Hits[index] with { CoveredBy = Coverage.SelfCertification };
    }

    // Reasonableness (IV.C pct. 16-18; guide p. 36-37): the form and the bank's file must not contradict the declaration.
    private static void Consistency(RuleSubject client, SubjectSelfCertification sc, Findings f)
    {
        // Only real jurisdictions: an empty or unknown code is SC-VALID's finding, not a contradiction with the file.
        var declared = sc.Residences.Where(r => Jurisdictions.IsKnown(r.Country)).Select(r => Jurisdictions.Normalize(r.Country)).ToHashSet();

        var formCountry = Jurisdictions.Normalize(sc.ResidenceAddressCountry);
        if (formCountry.Length > 0 && declared.Count > 0 && !declared.Contains(formCountry))
            f.Hit(RuleIds.SelfCertConsistent, formCountry, "SelfCertification/ResidenceAddress",
                $"Adresa de rezidență din autocertificare e în {Place(formCountry)}, dar {Place(formCountry)} nu e declarată ca jurisdicție de rezidență: testul de rezonabilitate nu trece (ghid p. 37, exemplul 2).",
                $"The residence address on the self-certification is in {Place(formCountry, true)}, but {Place(formCountry, true)} is not declared as a jurisdiction of residence: the reasonableness test fails (guide p. 37, example 2).");

        // A c/o address is a mailing address on file too (GOLDEN-CASES Î10; guide p. 29 lit. b, 48).
        var onFile = client.Addresses
            .Where(a => a.IsCurrent && a.Kind is AddressKind.Residence or AddressKind.Mailing or AddressKind.InCareOf)
            .Select(a => Jurisdictions.Normalize(a.Country))
            .Where(c => Jurisdictions.IsCrsReportable(c) && !declared.Contains(c) && c != formCountry)
            .Distinct();
        foreach (var country in onFile)
            f.Hit(RuleIds.SelfCertConsistent, country, "Addresses",
                $"Dosarul băncii are o adresă actuală în {Place(country)}, dar autocertificarea nu declară {Place(country)}: testul de rezonabilitate nu trece (ghid p. 36, exemplul 1).",
                $"The bank's file has a current address in {Place(country, true)}, but the self-certification does not declare {Place(country, true)}: the reasonableness test fails (guide p. 36, example 1).");

        // IGA Annex I: citizenship on file has no exception and contradicts the form (VI.A); a US place of birth needs the
        // documents of II.B.4 lit. a.
        if (sc.DeclaresUsPerson || declared.Contains(Jurisdictions.UnitedStates)) return;
        if (client.Citizenships.Any(Jurisdictions.IsUnitedStates))
            f.Hit(RuleIds.FatcaChange, Jurisdictions.UnitedStates, "SelfCertification",
                "Dosarul are cetățenie SUA, dar autocertificarea nu declară statut de persoană SUA: banca nu se poate baza pe un formular contrazis de dosar (IGA Anexa I, VI.A).",
                "The file shows US citizenship, but the self-certification does not declare US person status: the bank may not rely on a form its file contradicts (IGA Annex I, VI.A).");
        else if (Jurisdictions.IsUnitedStates(client.BirthCountry))
            f.Hit(RuleIds.FatcaChange, Jurisdictions.UnitedStates, "SelfCertification",
                "Dosarul are locul nașterii în SUA, dar autocertificarea nu declară statut de persoană SUA: e nevoie și de un act de identitate non-SUA și de certificatul de pierdere a cetățeniei SUA sau o explicație rezonabilă (IGA Anexa I, II.B.4 lit. a).",
                "The file shows a US place of birth, but the self-certification does not declare US person status: a non-US ID and the certificate of loss of US nationality or a reasonable explanation are also needed (IGA Annex I, II.B.4(a)).");
    }
}
