using System.Text.Json;
using System.Text.Json.Serialization;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.R2WorkflowHost;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>
/// The real engine inside R2's case workflow (R2WorkflowHost with CrsRuleEngine instead of the scripted one): a change
/// opens a case, the stored outcome reads back for the UI, and an answer closes it.
/// </summary>
public class CaseWorkflowEngineTests
{
    private static Task<R2WorkflowHost> WithRealEngine(Func<IServiceProvider, ICrsRuleEngine>? engine = null) =>
        CreateAsync(s => s.AddSingleton(sp => engine?.Invoke(sp) ?? sp.GetRequiredService<CrsRuleEngine>()));

    private static NewResponse Answer(int selfCertId) =>
        new(ResponseKind.NewSelfCertification, ExplanationCategory.None, "online", "New self-certification", SelfCertificationId: selfCertId);

    private static async Task<string> RequestSentAsync(R2WorkflowHost host, ChangeResult change)
    {
        var code = Assert.IsType<string>(change.CaseCode);
        var draft = await host.As(Member).Cases.DraftRequestAsync(code, "ro", "Buna ziua, va rugam sa ne trimiteti autocertificarea.");
        await host.As(Admin).Cases.ApproveRequestAsync(code, draft.Id, "email");
        return code;
    }

    [Fact]
    public async Task A_crs_move_opens_a_case_its_stored_outcome_reads_back_and_a_new_self_certification_closes_it()
    {
        await using var host = await WithRealEngine();
        await host.SeedClientAsync("CL-0001", new DateOnly(2019, 3, 14));

        var change = await host.MoveAsync("CL-0001", "DE");
        Assert.Equal(Decision.Clarification, change.Outcome.Decision);

        // What the case page reads for RuleEvidencePanel: RuleEvaluation.ResultJson, enums as strings.
        await using (var db = await host.DbAsync())
        {
            var json = (await db.RuleEvaluations.SingleAsync(e => e.ChangeEventId == change.Change.Id)).ResultJson;
            var stored = JsonSerializer.Deserialize<RuleOutcome>(json, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;
            Assert.Equal((change.Outcome.Decision, change.Outcome.Regime), (stored.Decision, stored.Regime));
            Assert.Equal(change.Outcome.Indicia.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy, h.Basis)),
                stored.Indicia.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy, h.Basis)));
            Assert.Equal(change.Outcome.Cleared.Select(h => h.RuleId), stored.Cleared.Select(h => h.RuleId));
        }

        var code = await RequestSentAsync(host, change);
        var selfCertId = await host.SubmitSelfCertificationAsync("CL-0001", code, host.Clock.UtcNow, null, "DE");
        await host.As(Member).Cases.RegisterResponseAsync(code, Answer(selfCertId));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);

        // Closing on a new self-certification cites SC-VALID (p. 34; human decision 28.10, golden case 4).
        Assert.Equal((CaseStatus.Closed, "SC-VALID"), (closed.Status, closed.OutcomeRuleId));
        Assert.Equal(["DE"], closed.ReportableFor);
    }

    [Fact]
    public async Task A_fatca_case_closes_on_a_not_us_person_form_once_close_passes_when_the_indicia_were_added()
    {
        // CaseService.CloseAsync with the one-line change proposed to R2: EvaluateCurrent(snapshot, latest Indicium.AddedAt).
        DateTime? addedAt = null;
        await using var host = await WithRealEngine(sp => new ClosesWithDate(sp.GetRequiredService<CrsRuleEngine>(), () => addedAt));
        await host.SeedClientAsync("CL-0014", new DateOnly(2019, 3, 14));

        var change = await host.As(Member).Changes.RecordAsync(new ChangeRequest("CL-0014", ChangeKind.PhoneAdded, "online",
            Phone: new PhoneSnapshot("+13125550147", "US")));
        Assert.Equal(Decision.FatcaAlert, change.Outcome.Decision);
        var code = await RequestSentAsync(host, change);
        host.AdvanceDays(3);
        var selfCertId = await host.SubmitSelfCertificationAsync("CL-0014", code, host.Clock.UtcNow, null, "RO");
        await host.As(Member).Cases.RegisterResponseAsync(code, Answer(selfCertId));

        await using (var db = await host.DbAsync())
            addedAt = (await db.Cases.Include(c => c.Indicia).SingleAsync(c => c.Code == code)).Indicia.Max(i => i.AddedAt);
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);

        Assert.Equal((CaseStatus.Closed, "FATCA-SC"), (closed.Status, closed.OutcomeRuleId));
        Assert.DoesNotContain("US", closed.ReportableFor);
    }

    /// <summary>The real engine, with the closing re-evaluation dated (what R2's CloseAsync would pass).</summary>
    private sealed class ClosesWithDate(CrsRuleEngine real, Func<DateTime?> changeKnownAt) : ICrsRuleEngine
    {
        public IReadOnlyList<RuleDefinition> Catalog => real.Catalog;
        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => real.Evaluate(before, after, kind);
        public RuleOutcome EvaluateCurrent(ClientSnapshot client) =>
            changeKnownAt() is { } at ? real.EvaluateCurrent(client, at) : real.EvaluateCurrent(client);
        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) =>
            real.EvaluateSelfCertification(client, selfCertification);
    }
}
