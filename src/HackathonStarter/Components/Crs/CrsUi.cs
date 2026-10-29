using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using MudBlazor;
using static HackathonStarter.Services.Lang;

namespace HackathonStarter.Components.Crs;

/// <summary>
/// How the CRS pages show backend values: words, tones and icons for the enums, and day counts on the demo date.
/// Display only: every decision, deadline and status comes from the services (code), never from here.
/// </summary>
public static class CrsUi
{
    private static readonly TimeZoneInfo Bucharest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    /// <summary>Dates on the CRS pages are business dates (ISimClock, UTC), shown in Bucharest time.</summary>
    public static System.DateTime Local(System.DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utc, DateTimeKind.Utc), Bucharest);

    private static readonly System.Globalization.CultureInfo En = System.Globalization.CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Dates follow the UI language (RO / EN), not the app's money culture.</summary>
    public static System.Globalization.CultureInfo Culture => IsEn ? En : Services.Format.Culture;

    public static string Date(DateTime utc) => Local(utc).ToString("d MMM yyyy", Culture);
    public static string DateShort(DateTime utc) => Local(utc).ToString("d MMM", Culture);
    public static string DateTimeText(DateTime utc) => Local(utc).ToString("d MMM yyyy, HH:mm", Culture);
    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", Culture);

    /// <summary>
    /// Days left from <paramref name="now"/> to <paramref name="due"/>: a started day counts as left (a case opened today shows
    /// 90, not 89); negative once the deadline has passed. Rounded to the minute, since a case opens milliseconds after its change.
    /// </summary>
    public static int DaysBetween(DateTime now, DateTime due)
    {
        var days = Math.Round((due - now).TotalMinutes) / 1440;
        return days >= 0 ? (int)Math.Ceiling(days) : (int)Math.Floor(days);
    }

    /// <summary>The day number of <paramref name="at"/> counted from <paramref name="start"/> (day 0), to the minute.</summary>
    public static int DayNumber(DateTime start, DateTime at) => Math.Max(0, (int)Math.Floor(Math.Round((at - start).TotalMinutes) / 1440));

    /// <summary>"Germany (DE)" / "Germania (DE)"; the English article of "the US" is dropped in a cell.</summary>
    public static string Country(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "-";
        if (IsEn && Jurisdictions.Normalize(code) == Jurisdictions.UnitedStates) return "United States (US)";
        var name = Jurisdictions.Display(code, IsEn);
        return name.StartsWith("the ", StringComparison.Ordinal) ? name[4..] : name;
    }

    public static string RuleTitle(string ruleId)
    {
        try { var rule = RuleCatalog.Get(ruleId); return IsEn ? rule.Title : rule.TitleRo; }
        catch (KeyNotFoundException) { return ruleId; }
    }

    /// <summary>A short name for a rule, for tags and chips; the full title and the ID go in the tooltip.</summary>
    public static string ShortRule(string ruleId) => ruleId switch
    {
        "CRS-IND-ADDR" or "FATCA-IND-ADDR" => T("Address"),
        "CRS-IND-MAIL" => T("Mailing address"),
        "CRS-IND-CO" or "FATCA-IND-CO" => T("In care of"),
        "CRS-IND-PHONE" or "FATCA-IND-PHONE" => T("Phone"),
        "CRS-IND-SO" or "FATCA-IND-SO" => T("Standing order"),
        "CRS-IND-POA" or "FATCA-IND-POA" => T("Power of attorney"),
        "FATCA-IND-POB" => T("Born in the US"),
        "FATCA-IND-CIT" => T("US citizen"),
        "SC-TIN-FORMAT" => T("TIN format"),
        "SC-TIN-MISSING" => T("Missing TIN"),
        _ => RuleTitle(ruleId),
    };

    /// <summary>
    /// An error from a service, as a person reads it: what went wrong in our words, then the service's own reason (English,
    /// from code) so nothing is hidden. The services decide; this only words the refusal.
    /// </summary>
    public static string Problem(Exception ex) => ex switch
    {
        UnauthorizedAccessException => T("Your role can't do this. Switch to Ana (operator) from the top bar."),
        InvalidOperationException => T("Code kept the case as it is: {0}", ex.Message),
        ArgumentException or KeyNotFoundException => T("Something in the form is missing or unknown: {0}", ex.Message),
        _ => T("Something went wrong: {0}", ex.Message),
    };

    /// <summary>"guide p. 29"; a design interpretation (<see cref="RuleCatalog.NotVerified"/>) says so instead of showing a page.</summary>
    public static string Page(string? page) =>
        string.IsNullOrWhiteSpace(page) || page == "?" ? T("page not verified")
        : page == RuleCatalog.NotVerified ? T("not in the guide: our reading")
        : T("guide p. {0}", page);

    // ---- Case status ----

    public static string Label(CaseStatus s) => s switch
    {
        CaseStatus.Open => T("Open"),
        CaseStatus.AwaitingClient => T("Awaiting client"),
        CaseStatus.Reminded => T("Reminded"),
        CaseStatus.Escalated => T("Escalated"),
        CaseStatus.ResponseReceived => T("Response received"),
        CaseStatus.Closed => T("Closed"),
        CaseStatus.Expired => T("Expired"),
        _ => s.ToString(),
    };

    public static string? Tone(CaseStatus s) => s switch
    {
        CaseStatus.Open => "accent",
        CaseStatus.AwaitingClient => null,
        CaseStatus.Reminded or CaseStatus.Escalated => "warning",
        CaseStatus.ResponseReceived => "accent",
        CaseStatus.Closed => "success",
        CaseStatus.Expired => "danger",
        _ => null,
    };

    public static string Icon(CaseStatus s) => s switch
    {
        CaseStatus.Open => Icons.Material.Rounded.FiberNew,
        CaseStatus.AwaitingClient => Icons.Material.Rounded.HourglassTop,
        CaseStatus.Reminded => Icons.Material.Rounded.NotificationsActive,
        CaseStatus.Escalated => Icons.Material.Rounded.PriorityHigh,
        CaseStatus.ResponseReceived => Icons.Material.Rounded.MarkEmailRead,
        CaseStatus.Closed => Icons.Material.Rounded.TaskAlt,
        CaseStatus.Expired => Icons.Material.Rounded.EventBusy,
        _ => Icons.Material.Rounded.Circle,
    };

    /// <summary>The four stages of the case page stepper: opened, waiting for the client, answer in, decided.</summary>
    public static int Stage(CaseStatus s) => s switch
    {
        CaseStatus.Open => 0,
        CaseStatus.AwaitingClient or CaseStatus.Reminded or CaseStatus.Escalated => 1,
        CaseStatus.ResponseReceived => 2,
        _ => 3,
    };

    // ---- Decision ----

    public static string Label(Decision d) => d switch
    {
        Decision.NoCase => T("No case"),
        Decision.Compliant => T("Compliant"),
        Decision.Clarification => T("Clarification"),
        Decision.FatcaAlert => T("FATCA alert"),
        Decision.ShieldAlert => T("Shield alert"),
        _ => d.ToString(),
    };

    public static string? Tone(Decision d) => d switch
    {
        Decision.NoCase => null,
        Decision.Compliant => "success",
        Decision.Clarification => "warning",
        Decision.FatcaAlert or Decision.ShieldAlert => "danger",
        _ => null,
    };

    public static string Icon(Decision d) => d switch
    {
        Decision.NoCase => Icons.Material.Rounded.DoNotDisturbOn,
        Decision.Compliant => Icons.Material.Rounded.Verified,
        Decision.Clarification => Icons.Material.Rounded.HelpCenter,
        Decision.FatcaAlert => Icons.Material.Rounded.Flag,
        Decision.ShieldAlert => Icons.Material.Rounded.GppMaybe,
        _ => Icons.Material.Rounded.Circle,
    };

    public static string Describe(Decision d) => d switch
    {
        Decision.NoCase => T("No rule from the guide applies: nothing to do, and the rules say why."),
        Decision.Compliant => T("Indicia found, and every one is covered by a self-certification or an accepted explanation."),
        Decision.Clarification => T("At least one CRS indicium is uncovered: a case is open and the deadline runs."),
        Decision.FatcaAlert => T("At least one FATCA indicium is uncovered: a case is open. The guide gives no FATCA deadline."),
        Decision.ShieldAlert => T("Hidden text was removed from the document: it never reaches the model. A person checks it."),
        _ => "",
    };

    public static string Label(Regime r) => r switch { Regime.Crs => "CRS", Regime.Fatca => "FATCA", _ => "CRS + FATCA" };

    public static string Label(Coverage c) => c switch
    {
        Coverage.None => T("Uncovered"),
        Coverage.SelfCertification => T("Covered by self-certification"),
        Coverage.AcceptedExplanation => T("Covered by an accepted explanation"),
        _ => c.ToString(),
    };

    public static string Label(RuleBasis b) => b switch
    {
        RuleBasis.Verified => T("From the guide"),
        RuleBasis.Interpretation => T("Our reading"),
        RuleBasis.Policy => T("Bank policy"),
        _ => b.ToString(),
    };

    // ---- Changes ----

    public static string Label(ChangeKind k) => k switch
    {
        ChangeKind.AddressChanged => T("Address changed"),
        ChangeKind.AddressAdded => T("Address added"),
        ChangeKind.PhoneAdded => T("Phone added"),
        ChangeKind.PhoneChanged => T("Phone changed"),
        ChangeKind.PhoneRemoved => T("Phone removed"),
        ChangeKind.StandingOrderCreated => T("Standing order"),
        ChangeKind.PowerOfAttorneyGranted => T("Power of attorney"),
        ChangeKind.SelfCertSubmitted => T("Self-certification"),
        ChangeKind.DocumentUploaded => T("Document uploaded"),
        ChangeKind.PersonalDataChanged => T("Birth country or citizenship"),
        _ => k.ToString(),
    };

    public static string Icon(ChangeKind k) => k switch
    {
        ChangeKind.AddressChanged or ChangeKind.AddressAdded => Icons.Material.Rounded.Home,
        ChangeKind.PhoneAdded or ChangeKind.PhoneChanged or ChangeKind.PhoneRemoved => Icons.Material.Rounded.Phone,
        ChangeKind.StandingOrderCreated => Icons.Material.Rounded.Repeat,
        ChangeKind.PowerOfAttorneyGranted => Icons.Material.Rounded.Handshake,
        ChangeKind.SelfCertSubmitted => Icons.Material.Rounded.HowToReg,
        ChangeKind.DocumentUploaded => Icons.Material.Rounded.UploadFile,
        ChangeKind.PersonalDataChanged => Icons.Material.Rounded.Badge,
        _ => Icons.Material.Rounded.EditNote,
    };

    // ---- Client data ----

    public static string Label(AddressKind k) => k switch
    {
        AddressKind.Residence => T("Residence"),
        AddressKind.Mailing => T("Mailing"),
        AddressKind.InCareOf => T("In care of"),
        AddressKind.HoldMail => T("Hold mail"),
        _ => k.ToString(),
    };

    public static string Label(AccountType t) => t switch
    {
        AccountType.Depository => T("Depository"),
        AccountType.Custodial => T("Custodial"),
        AccountType.Insurance => T("Insurance"),
        _ => T("Other"),
    };

    public static string Label(SelfCertStatus s) => s switch
    {
        SelfCertStatus.Valid => T("Valid"),
        SelfCertStatus.Doubtful => T("Doubtful"),
        SelfCertStatus.Superseded => T("Superseded"),
        _ => s.ToString(),
    };

    public static string Label(NoTinReason r) => r switch
    {
        NoTinReason.A_CountryDoesNotIssue => T("Reason A: the country issues no TIN"),
        NoTinReason.B_UnableToObtain => T("Reason B: unable to obtain one"),
        NoTinReason.C_NotRequired => T("Reason C: not required"),
        _ => "",
    };

    public static string Label(DocumentKind k) => k switch
    {
        DocumentKind.Passport => T("Passport"),
        DocumentKind.IdCard => T("ID card"),
        DocumentKind.Payslip => T("Payslip"),
        DocumentKind.BankStatement => T("Bank statement"),
        DocumentKind.SelfCertForm => T("Self-certification form"),
        DocumentKind.UtilityBill => T("Utility bill"),
        DocumentKind.Letter => T("Letter"),
        _ => T("Other"),
    };

    public static string Label(ShieldVerdict v) => v switch
    {
        ShieldVerdict.Clean => T("No hidden text"),
        ShieldVerdict.HiddenTextRemoved => T("Hidden text removed"),
        _ => T("Not scanned yet"),
    };

    // ---- Requests, responses, proof ----

    public static string Label(RequestKind k) => k switch
    {
        RequestKind.Initial => T("Request"),
        RequestKind.Reminder => T("Reminder"),
        RequestKind.FollowUp => T("Follow-up"),
        _ => k.ToString(),
    };

    public static string Label(RequestStatus s) => s switch
    {
        RequestStatus.Draft => T("Draft"),
        RequestStatus.Approved => T("Approved"),
        RequestStatus.Sent => T("Sent"),
        RequestStatus.Rejected => T("Rejected"),
        _ => s.ToString(),
    };

    public static string? Tone(RequestStatus s) => s switch
    {
        RequestStatus.Draft => "warning",
        RequestStatus.Sent or RequestStatus.Approved => "success",
        _ => null,
    };

    public static string Label(ResponseKind k) => k switch
    {
        ResponseKind.NewSelfCertification => T("New self-certification"),
        ResponseKind.ReasonableExplanation => T("Reasonable explanation"),
        _ => T("Other answer"),
    };

    public static string Label(ExplanationCategory c) => c switch
    {
        ExplanationCategory.Student => T("Student"),
        ExplanationCategory.TeacherOrTrainee => T("Teacher or trainee"),
        ExplanationCategory.Diplomat => T("Diplomat"),
        ExplanationCategory.EuOfficial => T("EU official"),
        ExplanationCategory.Other => T("Other"),
        _ => T("None"),
    };

    public static string Label(ConfirmationChannel c) => c switch
    {
        ConfirmationChannel.InPerson => T("In person"),
        ConfirmationChannel.Remote => T("Remote"),
        ConfirmationChannel.Online => T("Online"),
        _ => c.ToString(),
    };

    public static string Label(ConfirmationEvidenceKind k) => k switch
    {
        ConfirmationEvidenceKind.SignedDocument => T("Signed document"),
        ConfirmationEvidenceKind.VoiceRecording => T("Voice recording"),
        ConfirmationEvidenceKind.ElectronicAcceptance => T("Electronic acceptance"),
        ConfirmationEvidenceKind.AppInteraction => T("App interaction"),
        _ => k.ToString(),
    };

    /// <summary>Channels the bank's systems write ("branch", "online", "call center", "email").</summary>
    public static readonly string[] Channels = ["online", "branch", "call center", "email", "post"];

    public static string Channel(string? channel) => channel switch
    {
        null or "" => "-",
        "online" => T("Online"),
        "branch" => T("Branch"),
        "call center" => T("Call center"),
        "email" => T("Email"),
        "post" => T("Post"),
        _ => channel,
    };

    /// <summary>The audit kinds (Features/Audit/AuditKinds) as a person reads them.</summary>
    public static string AuditKind(string kind) => kind switch
    {
        "change.received" => T("Change received"),
        "rule.applied" => T("Rule checked"),
        "case.opened" => T("Case opened"),
        "case.indicium-added" => T("Indicium added"),
        "evidence.recorded" => T("Evidence recorded"),
        "signal.recorded" => T("Signal recorded"),
        "request.drafted" => T("Request drafted"),
        "request.approved" => T("Request approved and sent"),
        "request.rejected" => T("Request rejected"),
        "response.registered" => T("Answer registered"),
        "case.reminded" => T("Reminder due"),
        "case.escalated" => T("Escalated"),
        "case.expired" => T("Deadline expired"),
        "case.closed" => T("Case closed"),
        "tin.checked" => T("TIN format checked"),
        "shield.alert" => T("Shield alert"),
        "clock.changed" => T("Demo date moved"),
        _ => kind,
    };

    /// <summary>"system", "system:seed", "user:admin", "agent", "agent:crs-sentinel for user:admin" as a person reads them.</summary>
    public static string Actor(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) return "-";
        if (actor == "system") return T("System");
        if (actor == "system:seed") return T("Seed history");
        if (actor.StartsWith("system", StringComparison.Ordinal)) return T("System");
        if (actor == "agent") return T("Agent");
        const string agentPrefix = "agent:", onBehalf = " for user:";
        if (actor.StartsWith(agentPrefix, StringComparison.Ordinal))
        {
            var cut = actor.IndexOf(onBehalf, StringComparison.Ordinal);
            var agent = cut < 0 ? actor[agentPrefix.Length..] : actor[agentPrefix.Length..cut];
            var agentName = agent == "crs-sentinel" ? Config.SiteConfig.Name : agent;
            return cut < 0 ? agentName : T("{0} for {1}", agentName, UserName(actor[(cut + onBehalf.Length)..]));
        }
        return UserName(actor.StartsWith("user:", StringComparison.Ordinal) ? actor[5..] : actor);
    }

    private static string UserName(string id) => Config.SiteConfig.Users.FirstOrDefault(u => u.Id == id)?.Name ?? id;
}
