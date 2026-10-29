using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackathonStarter.Features.Audit;
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
    private static Task<R2WorkflowHost> WithRealEngine() =>
        CreateAsync(s => s.AddSingleton<ICrsRuleEngine>(sp => sp.GetRequiredService<CrsRuleEngine>()));

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
    public async Task A_fatca_case_closes_on_a_post_change_not_us_person_form_with_confirmation_proof()
    {
        await using var host = await WithRealEngine();
        var client = await host.SeedClientAsync("CL-0014", new DateOnly(2019, 3, 14));

        var change = await host.As(Member).Changes.RecordAsync(new ChangeRequest("CL-0014", ChangeKind.PhoneAdded, "online",
            Phone: new PhoneSnapshot("+13125550147", "US")));
        Assert.Equal(Decision.FatcaAlert, change.Outcome.Decision);
        var code = await RequestSentAsync(host, change);
        host.AdvanceDays(3);
        var selfCertId = await host.SubmitSelfCertificationAsync("CL-0014", code, host.Clock.UtcNow, null, "RO");
        var response = await host.As(Member).Cases.RegisterResponseAsync(code, Answer(selfCertId));
        var waiting = Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(code));
        Assert.Equal(CaseStatus.ResponseReceived, waiting.Status);
        Assert.Null(waiting.Proof);

        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);

        Assert.Equal((CaseStatus.Closed, "FATCA-SC"), (closed.Status, closed.OutcomeRuleId));
        Assert.DoesNotContain("US", closed.ReportableFor);
        await using var db = await host.DbAsync();
        var saved = await db.Cases.Include(c => c.Proof).SingleAsync(c => c.Code == code);
        var form = await db.SelfCertifications.SingleAsync(s => s.Id == selfCertId);
        Assert.True(form.SignedAt > change.Change.ReceivedAt);
        Assert.False(form.DeclaresUsPerson);
        Assert.Equal((CaseStatus.Closed, "FATCA-SC"), (saved.Status, saved.OutcomeRuleId));
        Assert.Empty(saved.ReportableFor);
        var proof = Assert.IsType<ConfirmationProof>(saved.Proof);
        Assert.Equal((ConfirmationChannel.Online, response.ReceivedAt, form.SignedAt, form.ReceivedAt),
            (proof.Channel, proof.ConfirmedAt, proof.DeclarationDatedAt, proof.DeclarationReceivedAt));
        Assert.Equal((client.FullName, Admin.Name, $"SC-{selfCertId}"),
            (proof.SignedBy, proof.ApprovedBy, proof.TransactionOrDocumentId));
        Assert.Equal(CaseService.Sha256(Encoding.UTF8.GetBytes(CaseService.CanonicalJson(form))), proof.EvidenceSha256);
    }

    [Fact]
    public async Task A_us_form_without_the_us_tin_opens_a_fatca_case_without_a_deadline()
    {
        await using var host = await WithRealEngine();
        var client = await host.SeedClientAsync("CL-0001", new DateOnly(2019, 3, 14));

        var change = await host.As(Member).Changes.RecordAsync(new ChangeRequest("CL-0001", ChangeKind.SelfCertSubmitted, "online",
            SelfCertification: new SelfCertificationSnapshot(host.Clock.UtcNow, [new("RO", client.Cnp), new("US", null)], DeclaresUsPerson: true)));

        Assert.Equal((Decision.FatcaAlert, Regime.Fatca), (change.Outcome.Decision, change.Outcome.Regime));
        await using var db = await host.DbAsync();
        var item = await db.Cases.Include(c => c.Indicia).SingleAsync(c => c.Code == change.CaseCode);
        // FATCA, and a finding on the form: no deadline in the guide (p. 38; GOLDEN-CASES Î1), never the CRS 90 days.
        Assert.Equal((Regime.Fatca, (DateTime?)null), (item.Regime, item.DueAt));
        Assert.DoesNotContain("DL-", item.DeadlineRuleId);
        Assert.Equal((RuleIds.FatcaTin, "US"), (Assert.Single(item.Indicia).RuleId, Assert.Single(item.Indicia).Jurisdiction));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("521374829", true)]
    public async Task A_fatca_tin_case_accepts_a_response_and_closes_only_on_a_form_with_the_us_tin(string? usTin, bool closes)
    {
        await using var host = await WithRealEngine();
        var client = await host.SeedClientAsync("CL-0001", new DateOnly(2019, 3, 14));
        Task<ChangeResult> Form(string? tin) => host.As(Member).Changes.RecordAsync(new ChangeRequest("CL-0001", ChangeKind.SelfCertSubmitted, "online",
            SelfCertification: new SelfCertificationSnapshot(host.Clock.UtcNow, [new("RO", client.Cnp), new("US", tin)], DeclaresUsPerson: true)));

        var code = await RequestSentAsync(host, await Form(null));
        host.AdvanceDays(3);
        await Form(usTin);
        int answer;
        await using (var db = await host.DbAsync())
            answer = await db.SelfCertifications.Where(x => x.ClientId == client.Id && x.Status == SelfCertStatus.Valid).Select(x => x.Id).SingleAsync();

        if (closes)
        {
            await host.As(Member).Cases.RegisterResponseAsync(code, Answer(answer));
            var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
            Assert.Equal((CaseStatus.Closed, "FATCA-SC"), (closed.Status, closed.OutcomeRuleId));
            Assert.Equal(["US"], closed.ReportableFor);
            Assert.NotNull(closed.Proof);
            Assert.Equal($"SC-{answer}", closed.Proof.TransactionOrDocumentId);
        }
        else
        {
            var before = Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(code));
            var entries = host.Audit.Entries.ToArray();
            Assert.Contains("FATCA-TIN:US", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                host.As(Member).Cases.RegisterResponseAsync(code, Answer(answer)))).Message);

            var after = Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(code));
            Assert.Equal(CaseStatus.AwaitingClient, after.Status);
            Assert.Empty(after.Responses);
            Assert.Null(after.Proof);
            Assert.Null(after.ClosedAt);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
            Assert.Equal(entries, host.Audit.Entries);
            Assert.Empty(host.Audit.OfKind(AuditKinds.ResponseRegistered));
            Assert.Empty(host.Audit.OfKind(AuditKinds.CaseClosed));

            // Refusal leaves the case waiting; FATCA still has no reminder or expiry deadline (p. 38).
            Assert.Null(after.DueAt);
            Assert.Null(after.RemindAt);
            host.AdvanceDays(90);
            Assert.Equal(new DeadlineTick(0, 0, 0), await host.As(Member).Cases.TickAsync(host.Clock.UtcNow));
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await host.As(Member).Cases.GetAsync(code)));
        }
    }
}
