using HackathonStarter.Features.Clients;
using static HackathonStarter.Features.Rules.Phrases;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// The seven US indicia (IGA Anexa I, Sec. II.B.1 lit. a-g; guide p. 31). None of the CRS exceptions is inherited: a US
/// phone counts next to a Romanian one, a standing order counts from a depository account, and the service-provider rule
/// of CPF pct. 13 does not exist here. Jurisdiction is always "US".
/// </summary>
internal static class FatcaIndicia
{
    private const string Us = Jurisdictions.UnitedStates;

    public static void Detect(RuleSubject s, AccountScope scope, Findings f)
    {
        var basis = scope.FatcaBasis;

        // lit. a): US citizen or resident. Citizenship counts only here (non-discrimination: no other nationality is read).
        if (s.Citizenships.Any(Jurisdictions.IsUnitedStates))
            Hit(f, RuleIds.FatcaCitizenship, "Citizenships", basis,
                "Cetățenie SUA la dosar: indiciu FATCA (IGA Anexa I, II.B.1 lit. a). Cetățenia contează doar la FATCA.",
                "US citizenship on file: FATCA indicium (IGA Annex I, II.B.1(a)). Citizenship counts only under FATCA.");
        else if (s.RecordedTaxResidences.Any(Jurisdictions.IsUnitedStates))
            Hit(f, RuleIds.FatcaCitizenship, "RecordedTaxResidences", basis,
                "Dosarul băncii îl identifică pe titular ca rezident SUA: indiciu FATCA (lit. a).",
                "The bank's records identify the holder as a US resident: FATCA indicium (a).");

        // lit. b): unambiguous US place of birth.
        if (Jurisdictions.IsUnitedStates(s.BirthCountry))
            Hit(f, RuleIds.FatcaBirthPlace, "BirthCountry", basis,
                "Locul nașterii în SUA: indiciu FATCA (lit. b).", "US place of birth: FATCA indicium (b).");

        // lit. c): current US residence or mailing address.
        foreach (var address in s.Addresses.Where(a => a.IsCurrent && Jurisdictions.IsUnitedStates(a.Country)
                     && a.Kind is AddressKind.Residence or AddressKind.Mailing))
            Hit(f, RuleIds.FatcaAddress, $"Addresses/{address.Kind}", basis,
                "Adresă actuală în SUA: indiciu FATCA (lit. c).", "Current US address: FATCA indicium (c).");

        // lit. d): any current US phone, even next to a Romanian one.
        if (s.Phones.Any(p => p.IsCurrent && Jurisdictions.IsUnitedStates(p.Country)))
        {
            var alsoRo = s.Phones.Any(p => p.IsCurrent && Jurisdictions.IsRomania(p.Country));
            Hit(f, RuleIds.FatcaPhone, "Phones", basis,
                "Telefon actual din SUA: indiciu FATCA (lit. d)" +
                (alsoRo ? ", chiar dacă există și un telefon în România: excepția CRS nu se aplică la FATCA." : "."),
                "Current US phone: FATCA indicium (d)" +
                (alsoRo ? ", even though there is also a Romanian phone: the CRS exception does not apply under FATCA." : "."));
        }

        // lit. e): standing instructions to a US account, from any account (no depository exception).
        foreach (var account in s.Accounts)
        foreach (var order in account.StandingOrders.Where(o => o.IsActive && Jurisdictions.IsUnitedStates(o.BeneficiaryCountry)))
        {
            var depository = account.Type == AccountType.Depository;
            var orderBasis = AccountStatus.IsFatcaPreexisting(account.OpenedOn) ? RuleBasis.Verified : RuleBasis.Interpretation;
            Hit(f, RuleIds.FatcaStandingOrder, "StandingOrders", orderBasis,
                "Ordin permanent activ către un cont din SUA: indiciu FATCA (lit. e)" +
                (depository ? "; e dintr-un cont de depozit, dar excepția CRS pentru conturile de depozit nu există la FATCA." : "."),
                "Active standing order to a US account: FATCA indicium (e)" +
                (depository ? "; it is from a depository account, but the CRS depository exception does not exist under FATCA." : "."));
        }

        // lit. f): currently effective power of attorney to a person with a US address.
        if (s.PowersOfAttorney.Any(p => p.IsActive && Jurisdictions.IsUnitedStates(p.HolderCountry)))
            Hit(f, RuleIds.FatcaPowerOfAttorney, "PowersOfAttorney", basis,
                "Împuternicire valabilă pentru o persoană cu adresă în SUA: indiciu FATCA (lit. f).",
                "Effective power of attorney to a person with a US address: FATCA indicium (f).");

        InCareOf(s, scope, f);

        // lit. b) checked and not applied (golden case 7): said only when the client has a US link, and only for a real
        // country code, so an unknown place of birth is never called "not the US".
        var born = Jurisdictions.Normalize(s.BirthCountry);
        if (Jurisdictions.IsKnown(born) && !Jurisdictions.IsUnitedStates(born) && f.Hits.Any(h => h.Regime == Regime.Fatca))
        {
            var ro = $"Locul nașterii la dosar e {Place(born)}, nu SUA: nu e indiciu FATCA (lit. b).";
            var en = $"The place of birth on file is {Place(born, true)}, not the US: not a FATCA indicium (b).";
            if (basis == RuleBasis.Interpretation)
                (ro, en) = (ro + " Cont nou FATCA: lista de indicii e citită ca «motiv să știe» (GEN-NEW-ACCOUNT, interpretare de design).",
                    en + " New FATCA account: the list of indicia is read as 'reason to know' (GEN-NEW-ACCOUNT, design interpretation).");
            f.Clear(RuleIds.FatcaBirthPlace, Us, "BirthCountry", ro, en, basis);
        }
    }

    // lit. g): in-care-of or hold-mail as the only address, in any country. On a preexisting lower value account a non-US
    // in-care-of address or a hold-mail instruction is not an indicium.
    private static void InCareOf(RuleSubject s, AccountScope scope, Findings f)
    {
        var current = s.Addresses.Where(a => a.IsCurrent).ToList();
        if (current.Count != 1 || current[0].Kind is not (AddressKind.InCareOf or AddressKind.HoldMail)) return;

        var address = current[0];
        var field = $"Addresses/{address.Kind}";
        var holdMail = address.Kind == AddressKind.HoldMail;

        if (scope.FatcaLowerValue && (holdMail || !Jurisdictions.IsUnitedStates(address.Country)))
        {
            f.Clear(RuleIds.FatcaInCareOf, Us, field,
                holdMail
                    ? "Cont preexistent cu valoare mică: mențiunea «păstrare corespondență» nu e indiciu FATCA (lit. g)."
                    : "Cont preexistent cu valoare mică: o adresă «în grija» din afara SUA nu e indiciu FATCA (lit. g).",
                holdMail
                    ? "Preexisting lower value account: a hold-mail instruction is not a FATCA indicium (g)."
                    : "Preexisting lower value account: a non-US in-care-of address is not a FATCA indicium (g).");
            return;
        }

        // Old, non-current addresses are not "the sole address on file" (GOLDEN-CASES Q6): our reading, as for CRS-IND-CO.
        var oldOnFile = s.Addresses.Any(a => !a.IsCurrent);
        var ro = $"Adresă «în grija» / «păstrare corespondență» în {Place(address.Country)}, singura adresă actuală: indiciu FATCA (lit. g), în orice țară.";
        var en = $"In-care-of / hold-mail address in {Place(address.Country, true)}, the only current address: FATCA indicium (g), in any country.";
        if (oldOnFile)
        {
            ro += " Adresele vechi, care nu mai sunt actuale, nu au fost luate în calcul (interpretare de design).";
            en += " Old addresses that are no longer current were not counted (design interpretation).";
        }
        if (!scope.FatcaPreexisting) (ro, en) = (ro + NewFatcaRo, en + NewFatcaEn);
        f.Hit(RuleIds.FatcaInCareOf, Us, field, ro, en, oldOnFile ? RuleBasis.Interpretation : scope.FatcaBasis);
    }

    /// <summary>
    /// Whether a new "not a US person" self-certification covers a FATCA hit (IGA Annex I; text checked on the treaty as
    /// published by the US Treasury). New account: III.B.2 asks for a new valid self-certification. Preexisting account:
    /// II.B.3 makes it US reportable unless II.B.4 applies: lit. d (power of attorney, in-care-of / hold mail, a US phone
    /// next to a non-US one) takes the self-certification alone; lit. b-c (address, US-only phones, standing order) also
    /// documentary evidence; lit. a (place of birth) also a non-US ID and a certificate of loss of nationality or an
    /// explanation. Citizenship on file has no exception, and VI.A forbids relying on a form the file contradicts. The
    /// engine has no documents as input, so it never covers what needs them. Basis is null when the hit keeps its own.
    /// </summary>
    public static (bool Covers, RuleBasis? Basis, string Ro, string En) CoveredByDenial(RuleHit hit, RuleSubject s, AccountScope scope)
    {
        switch (hit.RuleId)
        {
            case RuleIds.FatcaCitizenship:
                return (false, null,
                    "dosarul băncii îl identifică drept cetățean sau rezident SUA, iar banca nu se poate baza pe o autocertificare pe care are motive să o știe incorectă (IGA Anexa I, VI.A); Acordul nu are o excepție pentru lit. a).",
                    "the bank's records identify the holder as a US citizen or resident, and the bank may not rely on a self-certification it has reason to know is incorrect (IGA Annex I, VI.A); the agreement has no exception for (a).");
            case RuleIds.FatcaBirthPlace:
                return (false, null,
                    "la locul nașterii în SUA, Acordul cere și un act de identitate non-SUA, plus certificatul de pierdere a cetățeniei SUA sau o explicație rezonabilă (IGA Anexa I, II.B.4 lit. a)" +
                    (scope.FatcaPreexisting ? "." : "; pe un cont nou cerem aceleași documente (interpretare)."),
                    "for a US place of birth the agreement also asks for a non-US ID and the certificate of loss of US nationality or a reasonable explanation (IGA Annex I, II.B.4(a))" +
                    (scope.FatcaPreexisting ? "." : "; on a new account we ask for the same documents (interpretation)."));
            case RuleIds.FatcaPowerOfAttorney or RuleIds.FatcaInCareOf:
                return (true, null,
                    "pentru acest indiciu autocertificarea ajunge (IGA Anexa I, II.B.4 lit. d; III.B.2).",
                    "for this indicium the self-certification is enough (IGA Annex I, II.B.4(d); III.B.2).");
            case RuleIds.FatcaPhone when s.Phones.Any(p => p.IsCurrent && p.Country.Length > 0 && !Jurisdictions.IsUnitedStates(p.Country)):
                return (true, null,
                    "există și un telefon din afara SUA, deci autocertificarea ajunge (IGA Anexa I, II.B.4 lit. d; III.B.2).",
                    "there is also a non-US phone, so the self-certification is enough (IGA Annex I, II.B.4(d); III.B.2).");
            default:
                return scope.FatcaPreexisting
                    ? (false, null,
                        "pe un cont preexistent, pentru acest indiciu Acordul cere și documente justificative care stabilesc statutul non-SUA (IGA Anexa I, II.B.4 lit. b-c).",
                        "on a preexisting account the agreement also asks for documentary evidence of non-US status for this indicium (IGA Annex I, II.B.4(b)-(c)).")
                    : (true, RuleBasis.Interpretation,
                        "cont nou: Acordul cere o autocertificare nouă validă (IGA Anexa I, III.B.2); că indiciul nu o face nesigură e lectura noastră, o confirmă operatorul.",
                        "new account: the agreement asks for a new valid self-certification (IGA Annex I, III.B.2); that the indicium does not make it unreliable is our reading, the operator confirms it.");
        }
    }

    private const string NewFatcaRo =
        " Cont nou FATCA: indiciul e folosit ca «motiv să știe» la schimbarea de situație (IGA Anexa I, III.B.2; GEN-NEW-ACCOUNT, interpretare de design).";
    private const string NewFatcaEn =
        " New FATCA account: the indicium is used as 'reason to know' on a change in circumstances (IGA Annex I, III.B.2; GEN-NEW-ACCOUNT, design interpretation).";

    private static void Hit(Findings f, string ruleId, string field, RuleBasis basis, string ro, string en)
    {
        if (basis == RuleBasis.Interpretation) (ro, en) = (ro + NewFatcaRo, en + NewFatcaEn);
        f.Hit(ruleId, Us, field, ro, en, basis);
    }
}
