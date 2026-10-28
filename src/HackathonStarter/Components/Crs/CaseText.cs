using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using MudBlazor;
using static HackathonStarter.Services.Lang;

namespace HackathonStarter.Components.Crs;

/// <summary>Where a case stands against its deadline on the demo date. Presentation only: the deadline itself is computed by code in Features/Deadlines.</summary>
public enum DeadlineState { NoDeadline, OnTrack, ReminderDue, Overdue, Done }

/// <summary>
/// Words, tones and day counts for the CRS screens. Nothing here decides anything: statuses, deadlines and rule outcomes
/// come from the services; this only says them in the UI language.
/// </summary>
public static class CaseText
{
    public static string Status(CaseStatus status) => status switch
    {
        CaseStatus.Open => T("Open"),
        CaseStatus.AwaitingClient => T("Awaiting client"),
        CaseStatus.Reminded => T("Reminder sent"),
        CaseStatus.Escalated => T("Escalated to tax"),
        CaseStatus.ResponseReceived => T("Response received"),
        CaseStatus.Closed => T("Closed"),
        CaseStatus.Expired => T("Expired"),
        _ => status.ToString(),
    };

    /// <summary>Pill tone (Components/Common/Pill.razor): "accent", "success", "warning", "danger" or null for grey.</summary>
    public static string? StatusTone(CaseStatus status) => status switch
    {
        CaseStatus.Open or CaseStatus.ResponseReceived => "accent",
        CaseStatus.Reminded or CaseStatus.Escalated => "warning",
        CaseStatus.Closed => "success",
        CaseStatus.Expired => "danger",
        _ => null,
    };

    public static string StatusIcon(CaseStatus status) => status switch
    {
        CaseStatus.Open => Icons.Material.Rounded.AssignmentLate,
        CaseStatus.AwaitingClient => Icons.Material.Rounded.HourglassTop,
        CaseStatus.Reminded => Icons.Material.Rounded.NotificationsActive,
        CaseStatus.Escalated => Icons.Material.Rounded.PriorityHigh,
        CaseStatus.ResponseReceived => Icons.Material.Rounded.MarkEmailRead,
        CaseStatus.Closed => Icons.Material.Rounded.CheckCircle,
        CaseStatus.Expired => Icons.Material.Rounded.EventBusy,
        _ => Icons.Material.Rounded.Circle,
    };

    public static string Regime(Regime regime) => regime switch
    {
        Features.Rules.Regime.Crs => "CRS",
        Features.Rules.Regime.Fatca => "FATCA",
        _ => T("CRS and FATCA"),
    };

    public static string Decision(Decision decision) => decision switch
    {
        Features.Rules.Decision.NoCase => T("No case"),
        Features.Rules.Decision.Compliant => T("Compliant"),
        Features.Rules.Decision.Clarification => T("Clarification needed"),
        Features.Rules.Decision.FatcaAlert => T("FATCA alert"),
        Features.Rules.Decision.ShieldAlert => T("Hidden text found"),
        _ => decision.ToString(),
    };

    public static string? DecisionTone(Decision decision) => decision switch
    {
        Features.Rules.Decision.NoCase or Features.Rules.Decision.Compliant => "success",
        Features.Rules.Decision.Clarification => "warning",
        _ => "danger",
    };

    public static string Coverage(Coverage coverage) => coverage switch
    {
        Features.Rules.Coverage.SelfCertification => T("Covered by the self-certification"),
        Features.Rules.Coverage.AcceptedExplanation => T("Covered by an accepted explanation"),
        _ => T("Not covered"),
    };

    public static string RequestKind(RequestKind kind) => kind switch
    {
        Features.Cases.RequestKind.Reminder => T("Reminder"),
        Features.Cases.RequestKind.FollowUp => T("Follow-up request"),
        _ => T("First request"),
    };

    public static string RequestStatus(RequestStatus status) => status switch
    {
        Features.Cases.RequestStatus.Draft => T("Draft"),
        Features.Cases.RequestStatus.Approved => T("Approved, not sent yet"),
        Features.Cases.RequestStatus.Sent => T("Sent"),
        _ => T("Rejected by the operator"),
    };

    public static string? RequestTone(RequestStatus status) => status switch
    {
        Features.Cases.RequestStatus.Draft => "warning",
        Features.Cases.RequestStatus.Sent => "success",
        Features.Cases.RequestStatus.Rejected => "danger",
        _ => "accent",
    };

    public static string ResponseKind(ResponseKind kind) => kind switch
    {
        Features.Cases.ResponseKind.NewSelfCertification => T("New self-certification"),
        Features.Cases.ResponseKind.ReasonableExplanation => T("Reasonable explanation"),
        _ => T("Other answer"),
    };

    public static string? Category(ExplanationCategory category) => category switch
    {
        ExplanationCategory.Student => T("Student"),
        ExplanationCategory.TeacherOrTrainee => T("Teacher or trainee"),
        ExplanationCategory.Diplomat => T("Diplomat"),
        ExplanationCategory.EuOfficial => T("EU official"),
        ExplanationCategory.Other => T("Not on the guide's list: a person decides"),
        _ => null,
    };

    public static string Channel(ConfirmationChannel channel) => channel switch
    {
        ConfirmationChannel.InPerson => T("In person"),
        ConfirmationChannel.Remote => T("Remote"),
        _ => T("Online"),
    };

    public static string EvidenceKind(ConfirmationEvidenceKind kind) => kind switch
    {
        ConfirmationEvidenceKind.VoiceRecording => T("Voice recording"),
        ConfirmationEvidenceKind.ElectronicAcceptance => T("Electronic acceptance"),
        ConfirmationEvidenceKind.AppInteraction => T("Confirmed in the app"),
        _ => T("Signed document"),
    };

    public static string Signer(SignerCapacity capacity) =>
        capacity == SignerCapacity.Representative ? T("Representative") : T("Account holder");

    public static string Strength(SignalStrength strength) =>
        strength == SignalStrength.Moderate ? T("Moderate signal") : T("Weak signal");

    public static string Country(string? code) => Jurisdictions.Display(code, IsEn);

    /// <summary>True when the guide page is unknown: the UI shows NEVERIFICAT instead of a page.</summary>
    public static bool PageUnverified(string? page) =>
        string.IsNullOrWhiteSpace(page) || page.Contains(RuleCatalog.NotVerified, StringComparison.OrdinalIgnoreCase);

    /// <summary>"guide p. 29" (the ANAF guide of 29.12.2025).</summary>
    public static string Page(string page) => T("guide p. {0}", page);

    /// <summary>"p. 38", next to a text that already names the guide.</summary>
    public static string PageShort(string page) => T("p. {0}", page);

    /// <summary>The rule title in the UI language, or null for an id the catalog does not know (deadline rules).</summary>
    public static string? RuleTitle(string ruleId) =>
        RuleCatalog.Contains(ruleId) ? Pick(RuleCatalog.Get(ruleId).TitleRo, RuleCatalog.Get(ruleId).Title) : null;

    /// <summary>Calendar days from <paramref name="now"/> to <paramref name="at"/> (negative once it has passed).</summary>
    public static int DaysUntil(DateTime at, DateTime now) => (Local(at).Date - Local(now).Date).Days;

    /// <summary>The day number of <paramref name="at"/> counted from <paramref name="start"/> (day 0).</summary>
    public static int DayOf(DateTime at, DateTime start) => (Local(at).Date - Local(start).Date).Days;

    /// <summary>"5 zile", "85 de zile", "1 zi" / "5 days", "1 day". Romanian puts "de" before 20-99 in every hundred.</summary>
    public static string Days(int n) => Pick(RoDays(n), Math.Abs(n) == 1 ? $"{n} day" : $"{n} days");

    /// <summary>The unit alone, for a big number shown separately: "zile" / "de zile" / "zi".</summary>
    public static string DaysUnit(int n) => Days(n)[(n.ToString().Length + 1)..];

    public static DeadlineState Deadline(Case c, DateTime now)
    {
        if (!c.IsOpen) return DeadlineState.Done;
        if (c.DueAt is not { } due) return DeadlineState.NoDeadline;
        if (now > due) return DeadlineState.Overdue;
        return c.RemindAt is { } remind && now >= remind ? DeadlineState.ReminderDue : DeadlineState.OnTrack;
    }

    /// <summary>Whole days of a demo-clock offset ("+85").</summary>
    public static int OffsetDays(TimeSpan offset) => (int)Math.Floor(offset.TotalDays);

    private static string RoDays(int n)
    {
        var rest = Math.Abs(n) % 100;
        if (Math.Abs(n) == 1) return $"{n} zi";
        return rest >= 20 || (rest == 0 && n != 0) ? $"{n} de zile" : $"{n} zile";
    }

    private static DateTime Local(DateTime value) =>
        (value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value).ToLocalTime();
}
