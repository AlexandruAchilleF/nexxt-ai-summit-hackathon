using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Changes;

public enum ChangeKind
{
    AddressChanged, AddressAdded, PhoneAdded, PhoneChanged, PhoneRemoved,
    StandingOrderCreated, PowerOfAttorneyGranted, SelfCertSubmitted, DocumentUploaded,
    /// <summary>Birth country or citizenships (FATCA indicia, guide p. 31). Nationality is never a CRS signal.</summary>
    PersonalDataChanged,
}

/// <summary>A change to a client's data, as received from core banking (simulated in the demo).</summary>
public class ChangeEvent
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public ChangeKind Kind { get; set; }
    public string Summary { get; set; } = "";
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string SourceChannel { get; set; } = "";     // "branch", "online", "call center"
    /// <summary>When the bank was notified of the change: the 90 days run from here (guide p. 29, 35-36). Simulated clock.</summary>
    public DateTime ReceivedAt { get; set; }
    public int? EvaluationId { get; set; }
}

/// <summary>The saved result of the rule engine for one change (the outcome serialized as JSON, masked).</summary>
public class RuleEvaluation
{
    public int Id { get; set; }
    public int ChangeEventId { get; set; }
    public Decision Decision { get; set; }
    public string ResultJson { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public DateTime EvaluatedAt { get; set; }
}

/// <summary>
/// A change to record. Exactly one of the payloads matches <see cref="Kind"/>; <see cref="IChangeService"/> validates it,
/// applies it to the client, runs the rules on before/after and opens a case when needed. A DocumentUploaded change
/// (with <see cref="DocumentId"/>) runs the shield; hidden text gives Decision.ShieldAlert. <see cref="CaseCode"/> links a
/// self-certification sent as the answer to an open case.
/// </summary>
public record ChangeRequest(
    string ClientCode,
    ChangeKind Kind,
    string Channel,
    AddressSnapshot? Address = null,
    PhoneSnapshot? Phone = null,
    string? AccountIban = null,
    StandingOrderSnapshot? StandingOrder = null,
    PowerOfAttorneySnapshot? PowerOfAttorney = null,
    SelfCertificationSnapshot? SelfCertification = null,
    int? DocumentId = null,
    string? BirthCountry = null,
    IReadOnlyList<string>? Citizenships = null,
    string? CaseCode = null,
    string? Summary = null);

/// <param name="CaseCode">The case opened (or extended) by this change; null when the decision needs no case.</param>
public record ChangeResult(ChangeEvent Change, RuleOutcome Outcome, string? CaseCode)
{
    /// <summary>The saved self-certification for SelfCertSubmitted; null for other changes. Pass it when registering the response.</summary>
    public int? SelfCertificationId { get; init; }
}
