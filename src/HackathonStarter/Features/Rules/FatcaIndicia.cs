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
                     && a.Kind is SubjectAddressKind.Residence or SubjectAddressKind.Mailing))
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
            var depository = account.Type == SubjectAccountType.Depository;
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
    }

    // lit. g): in-care-of or hold-mail as the only address, in any country. On a preexisting lower value account a non-US
    // in-care-of address or a hold-mail instruction is not an indicium.
    private static void InCareOf(RuleSubject s, AccountScope scope, Findings f)
    {
        var current = s.Addresses.Where(a => a.IsCurrent).ToList();
        if (current.Count != 1 || current[0].Kind is not (SubjectAddressKind.InCareOf or SubjectAddressKind.HoldMail)) return;

        var address = current[0];
        var field = $"Addresses/{address.Kind}";
        var holdMail = address.Kind == SubjectAddressKind.HoldMail;

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

        Hit(f, RuleIds.FatcaInCareOf, field, scope.FatcaBasis,
            $"Adresă «în grija» / «păstrare corespondență» în {Place(address.Country)}, singura adresă actuală: indiciu FATCA (lit. g), în orice țară.",
            $"In-care-of / hold-mail address in {Place(address.Country, true)}, the only current address: FATCA indicium (g), in any country.");
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
