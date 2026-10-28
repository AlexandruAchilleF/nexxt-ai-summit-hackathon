using static HackathonStarter.Features.Rules.Phrases;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// The closed list of CRS indicia (CPF Anexa 1, Sec. III.B pct. 12 lit. a-f, refined by pct. 13; guide p. 29). Each
/// detector returns a hit, or a Cleared entry when the data is foreign but an exception applies (the traps).
/// Romanian data produces nothing: GEN-DOMESTIC is added by the engine when nothing at all was found.
/// </summary>
internal static class CrsIndicia
{
    public static void Detect(RuleSubject s, AccountScope scope, Findings f)
    {
        Residence(s, scope, f);
        Addresses(s, scope, f);
        Phones(s, scope, f);
        StandingOrders(s, f);
        PowersOfAttorney(s, scope, f);
        InCareOf(s, scope, f);
    }

    // lit. a): tax residence recorded by the bank outside the self-certification (the self-certification is documentation).
    private static void Residence(RuleSubject s, AccountScope scope, Findings f)
    {
        foreach (var country in s.RecordedTaxResidences.Select(Jurisdictions.Normalize).Distinct())
        {
            if (Jurisdictions.IsRomania(country)) continue;
            if (Jurisdictions.IsCrsReportable(country))
            {
                var (ro, en) = WithBasis(
                    $"Dosarul băncii (în afara autocertificării) îl identifică pe titular ca rezident în {Place(country)}: indiciu CRS (pct. 12 lit. a).",
                    $"The bank's records (outside the self-certification) identify the holder as resident in {Place(country, true)}: CRS indicium (12(a)).",
                    scope.CrsBasis);
                f.Hit(RuleIds.CrsResidence, country, "RecordedTaxResidences", ro, en, scope.CrsBasis);
            }
            else NotReportable(f, RuleIds.CrsResidence, country, "RecordedTaxResidences", "Rezidență fiscală la dosar", "Tax residence on file");
        }
    }

    // lit. b) + pct. 13: current residence / mailing addresses; a service provider's address is ignored when there are others.
    private static void Addresses(RuleSubject s, AccountScope scope, Findings f)
    {
        var current = s.Addresses
            .Where(a => a.IsCurrent && a.Kind is SubjectAddressKind.Residence or SubjectAddressKind.Mailing).ToList();

        foreach (var address in current)
        {
            var country = Jurisdictions.Normalize(address.Country);
            if (Jurisdictions.IsRomania(country)) continue;

            var mailing = address.Kind == SubjectAddressKind.Mailing;
            var ruleId = mailing ? RuleIds.CrsMailing : RuleIds.CrsAddress;
            var field = mailing ? "Addresses/Mailing" : "Addresses/Residence";
            var (whatRo, whatEn) = mailing
                ? ("Adresă de corespondență actuală", "Current mailing address")
                : ("Adresă de reședință actuală", "Current residence address");

            if (!Jurisdictions.IsCrsReportable(country))
            {
                NotReportable(f, ruleId, country, field, whatRo, whatEn);
                continue;
            }

            if (address.IsServiceProvider && current.Count > 1)
            {
                f.Clear(ruleId, country, field,
                    $"Adresa din {Place(country)} e a unui prestator de servicii, iar banca are și alte adrese: nu e indiciu (pct. 13).",
                    $"The address in {Place(country, true)} belongs to a service provider and the bank has other addresses: not an indicium (13).");
                continue;
            }

            var (ro, en) = WithBasis(
                $"{whatRo} în {Place(country)}: indiciu CRS (pct. 12 lit. b).",
                $"{whatEn} in {Place(country, true)}: CRS indicium (12(b)).", scope.CrsBasis);
            f.Hit(ruleId, country, field, ro, en, scope.CrsBasis);
        }
    }

    // lit. c) + pct. 13: current phones only; a foreign phone counts only when there is no Romanian phone.
    private static void Phones(RuleSubject s, AccountScope scope, Findings f)
    {
        var current = s.Phones.Where(p => p.IsCurrent).ToList();
        var counted = current.Count > 1 ? current.Where(p => !p.IsServiceProvider).ToList() : current;
        var hasRomanianPhone = counted.Any(p => Jurisdictions.IsRomania(p.Country));

        foreach (var phone in current)
        {
            var country = Jurisdictions.Normalize(phone.Country);
            if (Jurisdictions.IsRomania(country)) continue;

            if (!Jurisdictions.IsCrsReportable(country))
                NotReportable(f, RuleIds.CrsPhone, country, "Phones", "Telefon actual", "Current phone");
            else if (!counted.Contains(phone))
                f.Clear(RuleIds.CrsPhone, country, "Phones",
                    $"Telefonul din {Place(country)} e al unui prestator de servicii, iar banca are și alte numere: nu e indiciu (pct. 13).",
                    $"The phone in {Place(country, true)} belongs to a service provider and the bank has other numbers: not an indicium (13).");
            else if (hasRomanianPhone)
                f.Clear(RuleIds.CrsPhone, country, "Phones",
                    $"Telefon din {Place(country)}, dar există și un telefon actual în România: nu e indiciu CRS (pct. 12 lit. c).",
                    $"Phone in {Place(country, true)}, but there is also a current Romanian phone: not a CRS indicium (12(c)).");
            else
            {
                // The exception says "no phone in Romania"; a Romanian number that is no longer current is not counted
                // (our conservative reading, Maria's case): then the hit rests on interpretation.
                var oldRomanian = s.Phones.Any(p => !p.IsCurrent && Jurisdictions.IsRomania(p.Country));
                var basis = oldRomanian ? RuleBasis.Interpretation : scope.CrsBasis;
                var ro = $"Telefon actual din {Place(country)} și niciun telefon actual în România: indiciu CRS (pct. 12 lit. c).";
                var en = $"Current phone in {Place(country, true)} and no current Romanian phone: CRS indicium (12(c)).";
                if (oldRomanian)
                {
                    ro += " Telefonul din România care nu mai e actual nu a fost luat în calcul (interpretare de design).";
                    en += " The Romanian number that is no longer current was not counted (design interpretation).";
                }
                if (!scope.CrsPreexisting) (ro, en) = (ro + NewAccountRo, en + NewAccountEn);
                f.Hit(RuleIds.CrsPhone, country, "Phones", ro, en, basis);
            }
        }
    }

    // lit. d) + pct. 13: active standing instructions, except those concerning a depository account (VIII.C pct. 2).
    private static void StandingOrders(RuleSubject s, Findings f)
    {
        foreach (var account in s.Accounts)
        foreach (var order in account.StandingOrders)
        {
            var country = Jurisdictions.Normalize(order.BeneficiaryCountry);
            if (Jurisdictions.IsRomania(country)) continue;
            var depository = account.Type == SubjectAccountType.Depository;

            if (Jurisdictions.IsUnitedStates(country))
            {
                var (why, whyEn) = Jurisdictions.WhyNotCrsReportable(country);
                f.Clear(RuleIds.CrsStandingOrder, country, "StandingOrders",
                    $"Ordin permanent către un cont din SUA: {why}" + (depository ? "; în plus, e dintr-un cont de depozit." : "."),
                    $"Standing order to a US account: {whyEn}" + (depository ? "; besides, it is from a depository account." : "."));
            }
            else if (!Jurisdictions.IsCrsReportable(country))
                NotReportable(f, RuleIds.CrsStandingOrder, country, "StandingOrders", "Ordin permanent", "Standing order");
            else if (!order.IsActive)
                f.Clear(RuleIds.CrsStandingOrder, country, "StandingOrders",
                    $"Ordinul permanent către {Place(country)} nu mai e activ: nu e o instrucțiune permanentă (pct. 13).",
                    $"The standing order to {Place(country, true)} is no longer active: not a standing instruction (13).");
            else if (depository)
                f.Clear(RuleIds.CrsStandingOrder, country, "StandingOrders",
                    $"Ordin permanent către {Place(country)} dintr-un cont de depozit ({ProductRo(account)}): exceptat la CRS " +
                    "(pct. 12 lit. d; contul curent e cont de depozit, Sec. VIII.C pct. 2).",
                    $"Standing order to {Place(country, true)} from a depository account ({ProductEn(account)}): excepted under CRS " +
                    "(12(d); a current account is a depository account, VIII.C(2)).");
            else
            {
                var basis = AccountStatus.IsCrsPreexisting(account.OpenedOn) ? RuleBasis.Verified : RuleBasis.Interpretation;
                var (ro, en) = WithBasis(
                    $"Ordin permanent activ către un cont din {Place(country)}, dintr-un cont care nu e de depozit ({ProductRo(account)}): indiciu CRS (pct. 12 lit. d).",
                    $"Active standing order to an account in {Place(country, true)}, from an account that is not a depository account ({ProductEn(account)}): CRS indicium (12(d)).",
                    basis);
                f.Hit(RuleIds.CrsStandingOrder, country, "StandingOrders", ro, en, basis);
            }
        }
    }

    // lit. e): a currently effective power of attorney or signatory authority.
    private static void PowersOfAttorney(RuleSubject s, AccountScope scope, Findings f)
    {
        foreach (var poa in s.PowersOfAttorney)
        {
            var country = Jurisdictions.Normalize(poa.HolderCountry);
            if (Jurisdictions.IsRomania(country)) continue;

            if (!Jurisdictions.IsCrsReportable(country))
                NotReportable(f, RuleIds.CrsPowerOfAttorney, country, "PowersOfAttorney",
                    "Împuternicire pentru o persoană cu adresă", "Power of attorney to a person with an address");
            else if (!poa.IsActive)
                f.Clear(RuleIds.CrsPowerOfAttorney, country, "PowersOfAttorney",
                    $"Împuternicirea pentru o persoană cu adresă în {Place(country)} nu mai e valabilă: nu e indiciu (pct. 12 lit. e).",
                    $"The power of attorney to a person with an address in {Place(country, true)} is no longer effective: not an indicium (12(e)).");
            else
            {
                var (ro, en) = WithBasis(
                    $"Împuternicire valabilă pentru o persoană cu adresă în {Place(country)}: indiciu CRS (pct. 12 lit. e).",
                    $"Effective power of attorney to a person with an address in {Place(country, true)}: CRS indicium (12(e)).",
                    scope.CrsBasis);
                f.Hit(RuleIds.CrsPowerOfAttorney, country, "PowersOfAttorney", ro, en, scope.CrsBasis);
            }
        }
    }

    // lit. f) + pct. 13, 17: in-care-of / hold-mail in a reportable jurisdiction, when the bank has no other address.
    private static void InCareOf(RuleSubject s, AccountScope scope, Findings f)
    {
        var current = s.Addresses.Where(a => a.IsCurrent).ToList();
        foreach (var address in current.Where(a => a.Kind is SubjectAddressKind.InCareOf or SubjectAddressKind.HoldMail))
        {
            var country = Jurisdictions.Normalize(address.Country);
            if (Jurisdictions.IsRomania(country)) continue;
            var field = $"Addresses/{address.Kind}";

            if (!Jurisdictions.IsCrsReportable(country))
                NotReportable(f, RuleIds.CrsInCareOf, country, field, "Adresă «în atenția» / «post-restant»", "In-care-of / hold-mail address");
            else if (current.Count > 1)
                f.Clear(RuleIds.CrsInCareOf, country, field,
                    $"Adresă «în atenția» / «post-restant» în {Place(country)}, dar banca are și altă adresă actuală: nu e indiciu (pct. 12 lit. f).",
                    $"In-care-of / hold-mail address in {Place(country, true)}, but the bank has another current address: not an indicium (12(f)).");
            else
            {
                // Old, non-current addresses are not "another address on file" (GOLDEN-CASES Q6): our reading, not the text.
                var oldOnFile = s.Addresses.Any(a => !a.IsCurrent);
                var basis = oldOnFile ? RuleBasis.Interpretation : scope.CrsBasis;
                var ro = $"Adresă «în atenția» / «post-restant» în {Place(country)}, singura adresă actuală: indiciu CRS (pct. 12 lit. f). " +
                         "Fără alte indicii: căutare în registrul pe hârtie sau autocertificare / documente; altfel, cont nedocumentat (pct. 17).";
                var en = $"In-care-of / hold-mail address in {Place(country, true)}, the only current address: CRS indicium (12(f)). " +
                         "With no other indicia: paper record search or self-certification / documents; otherwise an undocumented account (17).";
                if (oldOnFile)
                {
                    ro += " Adresele vechi, care nu mai sunt actuale, nu au fost luate în calcul (interpretare de design).";
                    en += " Old addresses that are no longer current were not counted (design interpretation).";
                }
                if (!scope.CrsPreexisting) (ro, en) = (ro + NewAccountRo, en + NewAccountEn);
                f.Hit(RuleIds.CrsInCareOf, country, field, ro, en, basis);
            }
        }
    }

    private static void NotReportable(Findings f, string ruleId, string country, string field, string whatRo, string whatEn)
    {
        var (why, whyEn) = Jurisdictions.WhyNotCrsReportable(country);
        f.Clear(ruleId, country, field, $"{whatRo} în {Place(country)}: {why}.", $"{whatEn} in {Place(country, true)}: {whyEn}.");
    }

    private static string ProductRo(SubjectAccount a) => a.Product is { Length: > 0 } p ? p : a.Type switch
    {
        SubjectAccountType.Depository => "cont de depozit",
        SubjectAccountType.Custodial => "cont de custodie",
        SubjectAccountType.Insurance => "contract de asigurare",
        _ => "alt tip de cont",
    };

    private static string ProductEn(SubjectAccount a) => a.Type switch
    {
        SubjectAccountType.Depository => "depository account",
        SubjectAccountType.Custodial => "custodial account",
        SubjectAccountType.Insurance => "insurance contract",
        _ => "other account",
    };
}
