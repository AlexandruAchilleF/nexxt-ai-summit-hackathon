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
        Consistency(client, sc, f);
    }

    // IV.B pct. 3: signed or confirmed, dated, name, residence address, jurisdiction(s), date of birth.
    private static void Validity(SubjectSelfCertification sc, Findings f)
    {
        var missing = new List<(string Ro, string En)>();
        if (!sc.SignedOrConfirmed) missing.Add(("semnătura sau confirmarea", "signature or confirmation"));
        if (sc.SignedAt == default) missing.Add(("data", "date"));
        if (sc.FormFieldsKnown && string.IsNullOrWhiteSpace(sc.FullName)) missing.Add(("numele", "name"));
        if (sc.FormFieldsKnown && string.IsNullOrWhiteSpace(sc.ResidenceAddress)) missing.Add(("adresa de rezidență", "residence address"));
        if (sc.Residences.Count == 0) missing.Add(("jurisdicția de rezidență", "jurisdiction of residence"));
        if (sc.FormFieldsKnown && sc.BirthDate is null) missing.Add(("data nașterii", "date of birth"));

        if (missing.Count == 0 && !sc.FormFieldsKnown)
            f.Clear(RuleIds.SelfCertValid, "", "SelfCertification",
                "Autocertificarea are semnătură / confirmare, dată și jurisdicții. Numele, adresa și data nașterii din formular nu au ajuns la motor: le verifică operatorul pe document (Sec. IV.B pct. 3).",
                "The self-certification has a signature / confirmation, a date and jurisdictions. The form's name, address and date of birth did not reach the engine: the operator checks them on the document (IV.B(3)).",
                RuleBasis.Policy);
        else if (missing.Count == 0)
            f.Clear(RuleIds.SelfCertValid, "", "SelfCertification",
                "Autocertificarea are toate elementele cerute: semnătură / confirmare, dată, nume, adresă, jurisdicții, data nașterii (Sec. IV.B pct. 3).",
                "The self-certification has every required element: signature / confirmation, date, name, address, jurisdictions, date of birth (IV.B(3)).");
        else
            f.Hit(RuleIds.SelfCertValid, "", "SelfCertification",
                $"Autocertificarea nu e validă, lipsește: {string.Join(", ", missing.Select(m => m.Ro))} (Sec. IV.B pct. 3).",
                $"The self-certification is not valid, missing: {string.Join(", ", missing.Select(m => m.En))} (IV.B(3)).");
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
            switch (FormatValid(tins.Check(country, residence.Tin.Trim())))
            {
                case false:
                    f.Hit(RuleIds.TinFormat, country, "SelfCertification/Tin",
                        $"TIN-ul declarat pentru {place} nu are formatul acestei țări (verificare de format, nu de titular): e nevoie de clarificare.",
                        $"The TIN declared for {placeEn} does not match that country's format (a format check, not an ownership check): clarification needed.",
                        RuleBasis.Policy);
                    break;
                case true:
                    f.Clear(RuleIds.TinFormat, country, "SelfCertification/Tin",
                        $"TIN pentru {place}: format valid. Formatul nu confirmă că TIN-ul aparține clientului.",
                        $"TIN for {placeEn}: format valid. The format does not confirm the TIN belongs to the client.");
                    break;
                default:
                    f.Clear(RuleIds.TinFormat, country, "SelfCertification/Tin",
                        $"TIN pentru {place}: formatul nu a fost verificat (nu avem regulile acestei țări).",
                        $"TIN for {placeEn}: format not checked (no rules for this country).", RuleBasis.Policy);
                    break;
            }
            return;
        }

        // No TIN: reason C is enough; A / B only if the jurisdiction does not issue TINs to everyone (VII.A pct. 3 a) iv)).
        // R3's OECD sheets first; our table only where R3 has no answer yet.
        var issuesToAll = tins.IssuesTinToAllResidents(country) ?? Jurisdictions.IssuesTinToAll(country);
        var hasExplanation = !string.IsNullOrWhiteSpace(residence.NoTinExplanation);
        switch (residence.NoTinReason)
        {
            case NoTinReason.None:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Lipsește TIN-ul pentru {place} și nu e dat niciun motiv (A, B sau C).",
                    $"The TIN for {placeEn} is missing and no reason (A, B or C) is given.");
                break;
            case NoTinReason.C_NotRequired:
                Accepted(f, country, $"Fără TIN pentru {place}, motiv C: TIN-ul nu e cerut de legea internă (Sec. I.D).",
                    $"No TIN for {placeEn}, reason C: domestic law does not require it (I.D).");
                break;
            case NoTinReason.B_UnableToObtain when !hasExplanation:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Fără TIN pentru {place}, motiv B, dar fără explicația cerută de formular.",
                    $"No TIN for {placeEn}, reason B, but without the explanation the form asks for.");
                break;
            case var _ when issuesToAll == true:
                f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin",
                    $"Fără TIN pentru {place}, deși fișa OCDE spune că {place} emite TIN tuturor persoanelor fizice: banca are motive să știe că autocertificarea e nesigură (Sec. VII.A pct. 3 lit. a) pct. (iv)).",
                    $"No TIN for {placeEn}, although the OECD sheet says {placeEn} issues TINs to all individuals: the bank has reason to know the self-certification is unreliable (VII.A(3)(a)(iv)).");
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

    private static void Accepted(Findings f, string country, string ro, string en, RuleBasis basis = RuleBasis.Verified)
    {
        f.Hit(RuleIds.TinMissing, country, "SelfCertification/Tin", ro, en, basis);
        var index = f.Hits.FindIndex(h => h.RuleId == RuleIds.TinMissing && h.Jurisdiction == country);
        f.Hits[index] = f.Hits[index] with { CoveredBy = Coverage.SelfCertification };
    }

    // Reasonableness (IV.C pct. 16-18; guide p. 36-37): the form and the bank's file must not contradict the declaration.
    private static void Consistency(RuleSubject client, SubjectSelfCertification sc, Findings f)
    {
        var declared = sc.Residences.Select(r => Jurisdictions.Normalize(r.Country)).ToHashSet();

        var formCountry = Jurisdictions.Normalize(sc.ResidenceAddressCountry);
        if (formCountry.Length > 0 && declared.Count > 0 && !declared.Contains(formCountry))
            f.Hit(RuleIds.SelfCertConsistent, formCountry, "SelfCertification/ResidenceAddress",
                $"Adresa de rezidență din autocertificare e în {Place(formCountry)}, dar {Place(formCountry)} nu e declarată ca jurisdicție de rezidență: testul de rezonabilitate nu trece (ghid p. 37, exemplul 2).",
                $"The residence address on the self-certification is in {Place(formCountry, true)}, but {Place(formCountry, true)} is not declared as a jurisdiction of residence: the reasonableness test fails (guide p. 37, example 2).");

        var onFile = client.Addresses
            .Where(a => a.IsCurrent && a.Kind is AddressKind.Residence or AddressKind.Mailing)
            .Select(a => Jurisdictions.Normalize(a.Country))
            .Where(c => Jurisdictions.IsCrsReportable(c) && !declared.Contains(c) && c != formCountry)
            .Distinct();
        foreach (var country in onFile)
            f.Hit(RuleIds.SelfCertConsistent, country, "Addresses",
                $"Dosarul băncii are o adresă actuală în {Place(country)}, dar autocertificarea nu declară {Place(country)}: testul de rezonabilitate nu trece (ghid p. 36, exemplul 1).",
                $"The bank's file has a current address in {Place(country, true)}, but the self-certification does not declare {Place(country, true)}: the reasonableness test fails (guide p. 36, example 1).");

        var usOnFile = client.Citizenships.Any(Jurisdictions.IsUnitedStates) || Jurisdictions.IsUnitedStates(client.BirthCountry);
        if (usOnFile && !sc.DeclaresUsPerson && !declared.Contains(Jurisdictions.UnitedStates))
            f.Hit(RuleIds.FatcaChange, Jurisdictions.UnitedStates, "SelfCertification",
                "Dosarul are cetățenie SUA sau locul nașterii în SUA, dar autocertificarea nu declară statut de persoană SUA: pentru a înlătura indiciul e nevoie de documentele din IGA Anexa I, II.B.4.",
                "The file shows US citizenship or a US place of birth, but the self-certification does not declare US person status: removing the indicium needs the documents in IGA Annex I, II.B.4.");
    }
}
