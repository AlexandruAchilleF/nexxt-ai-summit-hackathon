using HackathonStarter.Features.Shield;

namespace HackathonStarter.Features.Clients;

// Synthetic clients only (Features/Synthetic). Country codes are ISO 3166-1 alpha-2 ("RO", "DE", "US"). Money is decimal.

public class Client
{
    public int Id { get; set; }
    public string Code { get; set; } = "";              // "CL-0001"
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string? BirthCountry { get; set; }            // FATCA: "US" is an indicium (guide p. 31)
    public string? Cnp { get; set; }                     // synthetic; masked before any model
    /// <summary>Not a CRS signal. Only "US" matters, and only for FATCA (guide p. 31).</summary>
    public List<string> Citizenships { get; set; } = [];
    public List<Address> Addresses { get; set; } = [];
    public List<Phone> Phones { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];
    public List<PowerOfAttorney> PowersOfAttorney { get; set; } = [];
    public List<SelfCertification> SelfCertifications { get; set; } = [];
    public List<ClientDocument> Documents { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";
}

public enum AddressKind { Residence, Mailing, InCareOf, HoldMail }

public class Address
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public AddressKind Kind { get; set; }
    public string Line { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "RO";
    public bool IsCurrent { get; set; } = true;
    public DateOnly ValidFrom { get; set; }
}

public class Phone
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Number { get; set; } = "";             // E.164, "+4989..."
    public string Country { get; set; } = "RO";          // derived from the prefix
    public bool IsCurrent { get; set; } = true;
}

/// <summary>Depository = current, savings or deposit account: a standing order from it is not a CRS indicium (guide p. 29, lit. d).</summary>
public enum AccountType { Depository, Custodial, Insurance, Other }

public class Account
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Iban { get; set; } = "";
    public AccountType Type { get; set; }
    public string Product { get; set; } = "";            // "Cont curent"
    public DateOnly OpenedOn { get; set; }
    /// <summary>CRS: held on 31 Dec 2015 = preexisting; opened on or after 1 Jan 2016 = new (guide p. 20).</summary>
    public bool IsPreexisting { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "RON";
    public List<StandingOrder> StandingOrders { get; set; } = [];
}

public class StandingOrder
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string BeneficiaryIban { get; set; } = "";
    public string BeneficiaryCountry { get; set; } = "";  // from the IBAN
    public decimal Amount { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public bool IsActive { get; set; } = true;
}

public class PowerOfAttorney
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string HolderName { get; set; } = "";
    public string HolderAddress { get; set; } = "";
    public string HolderCountry { get; set; } = "";
    public string Scope { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateOnly ValidFrom { get; set; }
}

/// <summary>Reasons for a missing TIN, from the model self-certification (guide p. 53).</summary>
public enum NoTinReason { None, A_CountryDoesNotIssue, B_UnableToObtain, C_NotRequired }

public class TaxResidence
{
    public string Country { get; set; } = "";
    public string? Tin { get; set; }
    public NoTinReason NoTinReason { get; set; }
    public string? NoTinExplanation { get; set; }
}

public enum SelfCertStatus { Valid, Doubtful, Superseded }

public class SelfCertification
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public DateTime SignedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Channel { get; set; } = "";
    public List<TaxResidence> Residences { get; set; } = [];
    public bool DeclaresUsPerson { get; set; }
    public SelfCertStatus Status { get; set; } = SelfCertStatus.Valid;
    public int? DocumentId { get; set; }
}

public enum DocumentKind { Passport, IdCard, Payslip, BankStatement, SelfCertForm, UtilityBill, Letter, Other }

/// <summary>A client document. The PDF bytes stay in the database (small synthetic files) so the demo needs no file storage.</summary>
public class ClientDocument
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public DocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string MediaType { get; set; } = "application/pdf";
    public byte[] Content { get; set; } = [];
    public int Pages { get; set; }
    public ShieldVerdict ShieldVerdict { get; set; } = ShieldVerdict.NotScanned;
    public DateTime UploadedAt { get; set; }
}

/// <summary>A document without its bytes.</summary>
public record DocumentInfo(int Id, int ClientId, DocumentKind Kind, string FileName, int Pages, ShieldVerdict ShieldVerdict, DateTime UploadedAt);
