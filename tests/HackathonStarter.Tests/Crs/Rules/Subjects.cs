using HackathonStarter.Features.Rules;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using AddressKind = HackathonStarter.Features.Clients.AddressKind;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>
/// Test data builder for the engine. "Standard RO resident" as in docs/GOLDEN-CASES.md 1: current RO residence, a current
/// RO mobile, citizenship RO, born in RO, a valid self-certification [RO, TIN = CNP]. Tests only state what differs.
/// </summary>
internal static class Subjects
{
    public static readonly DateTime ChangeAt = new(2026, 10, 28, 7, 0, 0, DateTimeKind.Utc);

    public static RuleSubject RoResident(string code = "CL-0099", string cnp = "1900101400004",
        DateOnly? openedOn = null, string phone = "+40721000000", string fullName = "Client Sintetic",
        DateOnly? birthDate = null, string iban = "RO19RZBR0000060021458391", string residence = "București")
    {
        var opened = openedOn ?? new DateOnly(2019, 3, 14);
        var born = birthDate ?? new DateOnly(1990, 1, 1);
        return new()
        {
            Code = code,
            FullName = fullName,
            BirthDate = born,
            BirthCountry = "RO",
            Citizenships = ["RO"],
            Addresses = [new SubjectAddress(AddressKind.Residence, "RO", Text: residence)],
            Phones = [new SubjectPhone(phone, "RO")],
            Accounts = [new SubjectAccount(iban, AccountType.Depository, opened, 18_450m, "RON", "Cont curent")],
            // Signed when the first account was opened (GOLDEN-CASES 1).
            SelfCertifications = [SelfCert(opened.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), new SubjectTaxResidence("RO", cnp)) with
            {
                FullName = fullName, ResidenceAddress = residence, BirthDate = born,
            }],
        };
    }

    public static SubjectSelfCertification SelfCert(DateTime signedAt, params SubjectTaxResidence[] residences) =>
        new(signedAt, residences)
        {
            FullName = "Client Sintetic",
            ResidenceAddress = "Adresă sintetică",
            ResidenceAddressCountry = residences.Length > 0 ? residences[0].Country : null,
            BirthDate = new DateOnly(1990, 1, 1),
        };

    /// <summary>Moves the residence: the current residences become non-current and the new one is current.</summary>
    public static RuleSubject MovedTo(this RuleSubject s, string country, string text = "") => s with
    {
        Addresses = [.. s.Addresses.Select(a => a.Kind == AddressKind.Residence ? a with { IsCurrent = false } : a),
            new SubjectAddress(AddressKind.Residence, country, Text: text)],
    };

    public static RuleSubject WithAddress(this RuleSubject s, AddressKind kind, string country, bool current = true,
        bool serviceProvider = false, string text = "") => s with
    {
        Addresses = [.. s.Addresses, new SubjectAddress(kind, country, current, text, serviceProvider)],
    };

    /// <summary>The given address becomes the only current one (the old ones stay on file as non-current).</summary>
    public static RuleSubject WithOnlyAddress(this RuleSubject s, AddressKind kind, string country, string text = "") => s with
    {
        Addresses = [.. s.Addresses.Select(a => a with { IsCurrent = false }), new SubjectAddress(kind, country, Text: text)],
    };

    public static RuleSubject WithPhone(this RuleSubject s, string number, string country, bool current = true,
        bool serviceProvider = false) => s with
    {
        Phones = [.. s.Phones, new SubjectPhone(number, country, current, serviceProvider)],
    };

    /// <summary>The new phone replaces the current ones (they stay on file as non-current).</summary>
    public static RuleSubject ReplacePhone(this RuleSubject s, string number, string country) => s with
    {
        Phones = [.. s.Phones.Select(p => p with { IsCurrent = false }), new SubjectPhone(number, country)],
    };

    public static RuleSubject WithoutPhones(this RuleSubject s) => s with { Phones = [] };

    public static RuleSubject WithAccounts(this RuleSubject s, params SubjectAccount[] accounts) => s with { Accounts = accounts };

    public static SubjectAccount Account(AccountType type, DateOnly openedOn, string number = "RO46RZBR0000060019823746",
        decimal balance = 10_000m, string currency = "RON") => new(number, type, openedOn, balance, currency);

    public static SubjectAccount WithOrder(this SubjectAccount account, string beneficiaryAccount, string country,
        bool active = true, string beneficiary = "") => account with
    {
        StandingOrders = [.. account.StandingOrders, new SubjectStandingOrder(beneficiaryAccount, country, active, beneficiary)],
    };

    public static RuleSubject WithPowerOfAttorney(this RuleSubject s, string holder, string country, bool active = true) => s with
    {
        PowersOfAttorney = [.. s.PowersOfAttorney, new SubjectPowerOfAttorney(holder, country, active)],
    };

    public static RuleSubject WithSelfCert(this RuleSubject s, SubjectSelfCertification selfCert) => s with
    {
        SelfCertifications = [.. s.SelfCertifications, selfCert],
    };

    public static RuleSubject WithCitizenships(this RuleSubject s, params string[] countries) => s with { Citizenships = countries };

    public static RuleSubject BornIn(this RuleSubject s, string? country) => s with { BirthCountry = country };

    public static RuleSubject WithExplanation(this RuleSubject s, string jurisdiction, string category) => s with
    {
        AcceptedExplanations = [.. s.AcceptedExplanations, new SubjectExplanation(jurisdiction, category)],
    };

    public static RuleSubject WithRecordedResidence(this RuleSubject s, string country) => s with
    {
        RecordedTaxResidences = [.. s.RecordedTaxResidences, country],
    };
}
