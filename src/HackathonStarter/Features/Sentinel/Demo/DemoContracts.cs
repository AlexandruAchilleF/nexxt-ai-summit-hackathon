using HackathonStarter.Features.Changes;

namespace HackathonStarter.Features.Sentinel.Demo;

/// <summary>A scripted demo scenario: a golden case whose change the operator applies live (docs/GOLDEN-CASES.md).</summary>
/// <param name="HasResponse">True when the scenario also has the client's answer document (Maria: the new self-certification).</param>
public record DemoScenario(string Id, string ClientCode, string GoldenCase, string Title, string TitleRo,
                           string Description, string DescriptionRo, bool HasResponse);

/// <summary>
/// The bank-side events of the live demo, which in production come from core banking and the client: the change of data
/// and the client's answer. Everything goes through the CRS services (IChangeService, ICaseService), so the rules, the
/// deadline and the audit log run as in production. Admin and member only (docs/adr/0001).
/// </summary>
public interface IDemoScenarios
{
    /// <summary>The scenarios in presentation order.</summary>
    IReadOnlyList<DemoScenario> List();

    /// <summary>Records the scenario's change through IChangeService. The result says which case it opened, if any.</summary>
    Task<ChangeResult> ApplyAsync(string scenarioId);

    /// <summary>
    /// Delivers the client's answer for an open case (e.g. autocertificare-maria-2027.pdf) as a new ClientDocument and
    /// records a DocumentUploaded change. The case status does not change: the agent registers the response. Returns the document id.
    /// </summary>
    Task<int> DeliverResponseAsync(string caseCode);
}

/// <summary>Moves the demo date (ISimClock): admin only, with a clock.changed audit entry (docs/PLAN.md 9.1).</summary>
public interface IDemoClock
{
    DateTime UtcNow { get; }
    TimeSpan Offset { get; }
    Task AdvanceAsync(TimeSpan by);
    Task ResetAsync();
}
