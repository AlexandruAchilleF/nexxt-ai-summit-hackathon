using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Cases;

/// <summary>Case lifecycle (docs/PLAN.md 9.3). Transitions happen only in <see cref="ICaseService"/>.</summary>
public enum CaseStatus { Open, AwaitingClient, Reminded, Escalated, ResponseReceived, Closed, Expired }

public class Case
{
    public int Id { get; set; }
    public string Code { get; set; } = "";               // "CRS-2026-0001"
    public int ClientId { get; set; }
    /// <summary>Copied from the client when the case opens (the queue shows it without a join).</summary>
    public string ClientCode { get; set; } = "";
    public int TriggerChangeId { get; set; }
    public Regime Regime { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    /// <summary>Computed when the case opens (Preexisting.For on the account, guide p. 20). CRS preexisting needs a
    /// self-certification AND new documentary evidence (p. 29); new: self-certification OR explanation (p. 35).</summary>
    public bool IsPreexisting { get; set; }
    public DateTime OpenedAt { get; set; }
    /// <summary>Null when the guide gives no deadline (FATCA, p. 38): shown as "no deadline in the guide", NEVERIFICAT.</summary>
    public DateTime? DueAt { get; set; }
    public DateTime? RemindAt { get; set; }
    public DateTime? RemindedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    /// <summary>Closure or expiry consequence, written by code from the guide (with <see cref="OutcomeRuleId"/>), in both languages.</summary>
    public string? Outcome { get; set; }
    public string? OutcomeRo { get; set; }
    public string? OutcomeRuleId { get; set; }
    /// <summary>Jurisdictions the account is treated as resident in after closing or expiry (17a: RO and ES; 17b: at least ES).</summary>
    public List<string> ReportableFor { get; set; } = [];
    public string AssignedTo { get; set; } = "";
    public string DeadlineRuleId { get; set; } = "";
    public string DeadlineGuidePage { get; set; } = "";
    public List<Indicium> Indicia { get; set; } = [];
    public List<Evidence> Evidence { get; set; } = [];
    public List<ContextSignal> Signals { get; set; } = [];
    public List<ClientRequest> Requests { get; set; } = [];
    public List<ClientResponse> Responses { get; set; } = [];
    public ConfirmationProof? Proof { get; set; }

    public bool IsOpen => Status is not (CaseStatus.Closed or CaseStatus.Expired);
}

/// <summary>A legal indicium found by the rule engine (code). Copied from <see cref="RuleHit"/> when the case opens or grows.</summary>
public class Indicium
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public int? ChangeEventId { get; set; }
    public string RuleId { get; set; } = "";
    public Regime Regime { get; set; }
    public string Jurisdiction { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string ExplanationRo { get; set; } = "";
    public string GuidePage { get; set; } = "";
    public string SourceField { get; set; } = "";
    public DateTime AddedAt { get; set; }
}

public enum EvidenceSource { BankData, Document }

/// <summary>A quote that supports an indicium or a signal. <see cref="Verified"/> is set by code (EvidenceVerifier), never by a model.</summary>
public class Evidence
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? CaseId { get; set; }
    public EvidenceSource Source { get; set; }
    public int? DocumentId { get; set; }
    public int? Page { get; set; }
    public string Quote { get; set; } = "";
    public bool Verified { get; set; }
    public string? VerificationNote { get; set; }
    public string? LinkedRuleId { get; set; }
    public string RecordedBy { get; set; } = "";         // "agent", "user:ana"
    public DateTime At { get; set; }
}

public enum SignalStrength { Weak, Moderate }

/// <summary>
/// A context signal found by the model (e.g. salary from a German employer). Shown with its evidence, labelled
/// "signal, no alert". Invariant (tested): it never changes a Decision and never opens a case. It may exist without a case.
/// </summary>
public class ContextSignal
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? CaseId { get; set; }
    public string Description { get; set; } = "";
    public int EvidenceId { get; set; }
    public SignalStrength Strength { get; set; } = SignalStrength.Weak;
    public DateTime At { get; set; }
}

public enum RequestKind { Initial, Reminder, FollowUp }
public enum RequestStatus { Draft, Approved, Sent, Rejected }

/// <summary>The request to the client. The model writes <see cref="Body"/>; code builds <see cref="RequestedDocuments"/>; a person approves.</summary>
public class ClientRequest
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public RequestKind Kind { get; set; }
    public string Language { get; set; } = "ro";
    public string Body { get; set; } = "";
    public List<string> RequestedDocuments { get; set; } = [];
    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public string Channel { get; set; } = "";
    public string DraftedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public enum ResponseKind { NewSelfCertification, ReasonableExplanation, Other }

public class ClientResponse
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public ResponseKind Kind { get; set; }
    public ExplanationCategory Category { get; set; }
    public string Channel { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public int? DocumentId { get; set; }
    /// <summary>The self-certification sent as the answer (saved through IChangeService); CloseAsync reads its dates.</summary>
    public int? SelfCertificationId { get; set; }
    /// <summary>Preexisting CRS accounts need new documentary evidence besides the self-certification (guide p. 29).</summary>
    public bool HasDocumentaryEvidence { get; set; }
    public string Summary { get; set; } = "";
}

/// <summary>Channels named by the guide (p. 34): face to face, remote, online.</summary>
public enum ConfirmationChannel { InPerson, Remote, Online }

/// <summary>Forms of positive confirmation named by the guide (p. 34).</summary>
public enum ConfirmationEvidenceKind { SignedDocument, VoiceRecording, ElectronicAcceptance, AppInteraction }

public enum SignerCapacity { AccountHolder, Representative }

/// <summary>
/// The record that shows, credibly, how the self-certification was obtained and confirmed (guide p. 34-35): collection
/// steps, moment, channel, transaction/document id, evidence. Kept for audit with the self-certification.
/// </summary>
public class ConfirmationProof
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public ConfirmationChannel Channel { get; set; }
    public DateTime ConfirmedAt { get; set; }
    /// <summary>Must not be later than <see cref="DeclarationReceivedAt"/> (guide p. 34: dated at the latest on the date of receipt).</summary>
    public DateTime DeclarationDatedAt { get; set; }
    public DateTime DeclarationReceivedAt { get; set; }
    public string CollectionSteps { get; set; } = "";
    public string? TransactionOrDocumentId { get; set; }
    public ConfirmationEvidenceKind EvidenceKind { get; set; }
    public string EvidenceSha256 { get; set; } = "";
    public string SignedBy { get; set; } = "";
    public SignerCapacity SignerCapacity { get; set; }
    public string ApprovedBy { get; set; } = "";
}
