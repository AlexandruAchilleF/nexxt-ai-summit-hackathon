using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Rules;

// What the engine reads about a client: an immutable view with no EF, no clock and no raw values it does not need.
// SubjectMapper builds it from the shared ClientSnapshot (docs/PLAN.md 7.1); tests and the golden cases can build it
// directly. Countries are ISO 3166-1 alpha-2, derived upstream (from the phone prefix, the IBAN, the address). The enums
// are the shared ones from Features/Clients.

public sealed record SubjectAddress(AddressKind Kind, string Country, bool IsCurrent = true, string Text = "",
    bool IsServiceProvider = false);

public sealed record SubjectPhone(string Number, string Country, bool IsCurrent = true, bool IsServiceProvider = false);

public sealed record SubjectStandingOrder(string BeneficiaryAccount, string BeneficiaryCountry, bool IsActive = true,
    string Beneficiary = "");

public sealed record SubjectAccount(string Number, AccountType Type, DateOnly OpenedOn, decimal Balance = 0m,
    string Currency = "RON", string Product = "")
{
    public IReadOnlyList<SubjectStandingOrder> StandingOrders { get; init; } = [];
}

public sealed record SubjectPowerOfAttorney(string HolderName, string HolderCountry, bool IsActive = true);

public sealed record SubjectTaxResidence(string Country, string? Tin,
    NoTinReason NoTinReason = NoTinReason.None, string? NoTinExplanation = null);

/// <summary>A self-certification as received. IsValid = the bank currently relies on it (Status Valid in the shared model).</summary>
public sealed record SubjectSelfCertification(DateTime SignedAt, IReadOnlyList<SubjectTaxResidence> Residences,
    bool DeclaresUsPerson = false, bool IsValid = true)
{
    public bool SignedOrConfirmed { get; init; } = true;

    /// <summary>False when the data model does not carry the form's name, address and birth date (PLAN 7.1): SC-VALID
    /// then leaves them to the operator instead of reporting them missing.</summary>
    public bool FormFieldsKnown { get; init; } = true;

    public string FullName { get; init; } = "";
    public string ResidenceAddress { get; init; } = "";
    public string? ResidenceAddressCountry { get; init; }
    public DateOnly? BirthDate { get; init; }
}

/// <summary>An explanation a person accepted for a jurisdiction (RESP-EXPLANATION, CRS only), e.g. ("DE", "Student").</summary>
public sealed record SubjectExplanation(string Jurisdiction, string Category);

public sealed record RuleSubject
{
    public string Code { get; init; } = "";
    public string FullName { get; init; } = "";
    public DateOnly? BirthDate { get; init; }
    public string? BirthCountry { get; init; }
    public IReadOnlyList<string> Citizenships { get; init; } = [];
    public IReadOnlyList<SubjectAddress> Addresses { get; init; } = [];
    public IReadOnlyList<SubjectPhone> Phones { get; init; } = [];
    public IReadOnlyList<SubjectAccount> Accounts { get; init; } = [];
    public IReadOnlyList<SubjectPowerOfAttorney> PowersOfAttorney { get; init; } = [];
    public IReadOnlyList<SubjectSelfCertification> SelfCertifications { get; init; } = [];
    public IReadOnlyList<SubjectExplanation> AcceptedExplanations { get; init; } = [];

    /// <summary>Tax residences on file outside the self-certification (KYC records): indicium lit. a).</summary>
    public IReadOnlyList<string> RecordedTaxResidences { get; init; } = [];

    /// <summary>The latest self-certification the bank relies on, or null.</summary>
    public SubjectSelfCertification? CurrentSelfCertification =>
        SelfCertifications.Where(s => s.IsValid).OrderByDescending(s => s.SignedAt).FirstOrDefault();
}

/// <summary>The change that triggered the evaluation. Kind is the shared ChangeKind as text ("AddressChanged").</summary>
public sealed record RuleChange(string Kind, string Summary = "", string SourceChannel = "");
