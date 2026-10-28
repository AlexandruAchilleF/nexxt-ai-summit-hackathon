using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Features.Sentinel.Demo;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Tests.Support;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>
/// Maria end to end (docs/PLAN.md 6.3, docs/PITCH.md S2-S4) on the real CRS modules and the seed: the change of golden case 4
/// through IChangeService, run 1 from the "Analizează dosarul" button, the approval, day 85 (reminder and escalation), the
/// answer delivered as a client document, run 2 from "Verifică răspunsul", the approval of the closing, and an intact audit
/// chain. On the mock (no model) in Romanian and English, and on a scripted model (FakeChatClient). No network.
/// </summary>
public class SentinelMariaTests
{
    private const string CaseCode = "CRS-2026-0001";

    [Theory]
    [InlineData("ro-RO")]
    [InlineData("en-GB")]
    public async Task Maria_on_the_mock_two_runs_two_approvals_the_reminder_and_the_proof(string culture)
    {
        using var _ = new UiLanguage(culture);
        var (host, clock) = await RealWorld.CreateAsync();
        await using var __ = host;

        // S2: the change opens the case (code decides the indicia and the 90 days).
        var change = await host.RecordGoldenChangeAsync("4");
        Assert.Equal(CaseCode, change.CaseCode);
        var opened = (await host.CaseAsync(CaseCode))!;
        Assert.Equal(["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"], Hits.Of(opened.Indicia));
        Assert.Equal("DL-NEW-90", opened.DeadlineRuleId);
        Assert.Equal(GoldenCases.NewAccountDueAt, opened.DueAt);

        // S3, run 1: the button's message. It stops at the approval of the request; nothing has been sent.
        var run1 = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(CaseCode));
        Assert.Equal(RunSource.Mock, run1.Source);
        Assert.Equal(RunStatus.AwaitingApproval, run1.Status);
        Assert.Equal(["getCase", "listDocuments", "readDocument", "recordEvidence", "recordSignal", "draftClientRequest"], run1.ToolCalls());
        Assert.Equal(SentinelTools.SendClientRequest, Assert.Single(run1.Steps, s => s.Kind == StepKind.Approval).Tool);
        run1.ChecksPass();
        var answer1 = run1.Answer();
        Assert.Contains("CRS-IND-PHONE", answer1);
        Assert.Contains("„Angajator: Müller Logistik GmbH”", answer1);
        Assert.Contains(SentinelWording.SignalLabel, answer1);
        Assert.DoesNotContain("4521 7730", answer1); // the phone is cited by country, never repeated

        var prepared = (await host.CaseAsync(CaseCode))!;
        Assert.Equal(CaseStatus.Open, prepared.Status);
        var evidence = Assert.Single(prepared.Evidence, e => e.LinkedRuleId == "CRS-IND-PHONE");
        Assert.True(evidence.Verified);
        Assert.Equal("Telefon: +49 89 4521 7730", evidence.Quote);
        Assert.Equal("agent:crs-sentinel for user:admin", evidence.RecordedBy);
        var signal = Assert.Single(prepared.Signals);
        Assert.Equal("Angajator: Müller Logistik GmbH", prepared.Evidence.Single(e => e.Id == signal.EvidenceId).Quote);
        Assert.Equal(RequestStatus.Draft, Assert.Single(prepared.Requests).Status);

        // Ana approves: the tool runs now, without the model.
        var approved = await host.DecideAsync(run1, SentinelTools.SendClientRequest);
        Assert.Equal(RunStatus.Completed, approved.Status);
        Assert.Equal(CaseStatus.AwaitingClient, (await host.CaseAsync(CaseCode))!.Status);

        // S4: day 85, no answer: the watcher's tick reminds and escalates.
        clock.Advance(TimeSpan.FromDays(85));
        Assert.Equal(new DeadlineTick(1, 1, 0), await host.Get<ICaseService>().TickAsync(clock.UtcNow));
        Assert.Equal(CaseStatus.Escalated, (await host.CaseAsync(CaseCode))!.Status);

        // Day 86: the client's new self-certification arrives as a client document (the demo inbox, then DocumentUploaded).
        clock.Advance(TimeSpan.FromDays(1));
        var documentId = await host.Get<IDemoScenarios>().DeliverResponseAsync(CaseCode);
        Assert.Equal(documentId, await host.DocumentIdAsync("CL-0001", SyntheticPdfs.SelfCertMaria));

        // Run 2: the second button. It stops at the approval of the closing.
        var run2 = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Verify(CaseCode));
        Assert.Equal(RunStatus.AwaitingApproval, run2.Status);
        Assert.Equal(["getCase", "listDocuments", "extractSelfCertification", "checkTin", "registerResponse", "evaluateRules"], run2.ToolCalls());
        Assert.Equal(SentinelTools.CloseCase, Assert.Single(run2.Steps, s => s.Kind == StepKind.Approval).Tool);
        run2.ChecksPass();
        var answer2 = run2.Answer();
        Assert.Contains("format valid", answer2);
        Assert.Contains("Compliant", answer2);
        Assert.DoesNotContain("TIN valid", answer2);

        var answered = (await host.CaseAsync(CaseCode))!;
        Assert.Equal(CaseStatus.ResponseReceived, answered.Status);
        var response = Assert.Single(answered.Responses);
        Assert.Equal((ResponseKind.NewSelfCertification, documentId), (response.Kind, response.DocumentId));
        Assert.NotNull(response.SelfCertificationId);

        // Ana approves the closing: code re-evaluates, closes and keeps the proof.
        await host.DecideAsync(run2, SentinelTools.CloseCase);
        var closed = (await host.CaseAsync(CaseCode))!;
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.Equal(["DE"], closed.ReportableFor);
        Assert.Equal(GoldenCases.Get("4").Response!.RuleId, closed.OutcomeRuleId);
        Assert.Equal(ConfirmationChannel.Online, closed.Proof!.Channel);
        Assert.Equal($"DOC-{documentId}", closed.Proof.TransactionOrDocumentId);
        Assert.Equal("Ana Admin", closed.Proof.ApprovedBy);

        // The audit log has every step, in order, and its hash chain is intact.
        var kinds = await host.AuditKindsAsync();
        string[] expected =
        [
            AuditKinds.CaseOpened, AuditKinds.EvidenceRecorded, AuditKinds.EvidenceRecorded, AuditKinds.SignalRecorded, AuditKinds.RequestDrafted,
            AuditKinds.RequestApproved,
            AuditKinds.CaseReminded, AuditKinds.CaseEscalated, AuditKinds.TinChecked, AuditKinds.ResponseRegistered, AuditKinds.CaseClosed,
        ];
        Assert.Equal(expected, kinds.Where(expected.Contains));
        Assert.True(kinds.Take(kinds.IndexOf(AuditKinds.EvidenceRecorded)).Count(k => k == AuditKinds.RuleApplied) >= 2); // the two indicia
        Assert.True((await host.VerifyAuditAsync()).Ok);
    }

    [Fact]
    public async Task Maria_on_the_mock_over_the_startup_history_closes_her_own_case_only()
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, clock) = await RealWorld.CreateAsync(history: true);
        await using var __ = host;
        var others = (await host.Get<ICaseService>().QueueAsync(new CaseQueueFilter(OpenOnly: false))).Select(c => c.Code).ToHashSet();
        Assert.NotEmpty(others);

        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        Assert.DoesNotContain(code, others);

        var run1 = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(code));
        Assert.Equal(RunStatus.AwaitingApproval, run1.Status);
        run1.ChecksPass();
        await host.DecideAsync(run1, SentinelTools.SendClientRequest);
        clock.Advance(TimeSpan.FromDays(85));
        var tick = await host.Get<ICaseService>().TickAsync(clock.UtcNow);
        Assert.True(tick.Reminded >= 1); // Maria's, and whatever the history has due by then
        Assert.Equal(CaseStatus.Escalated, (await host.CaseAsync(code))!.Status);
        clock.Advance(TimeSpan.FromDays(1));
        await host.Get<IDemoScenarios>().DeliverResponseAsync(code);

        var run2 = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Verify(code));
        Assert.Equal(RunStatus.AwaitingApproval, run2.Status);
        run2.ChecksPass();
        await host.DecideAsync(run2, SentinelTools.CloseCase);

        Assert.Equal(CaseStatus.Closed, (await host.CaseAsync(code))!.Status);
        Assert.True((await host.VerifyAuditAsync()).Ok);
    }

    [Fact]
    public async Task Maria_on_a_scripted_model_masks_the_phone_and_the_tools_get_it_back()
    {
        using var _ = new UiLanguage("ro-RO");
        var extract = new FakeChatClient();
        var model = new FakeChatClient();
        var (host, clock) = await RealWorld.CreateAsync(model, profiles: new Dictionary<string, IChatClient> { [SelfCertExtractor.Profile] = extract });
        await using var __ = host;
        await host.RecordGoldenChangeAsync("4");
        var payslip = await host.DocumentIdAsync("CL-0001", SyntheticPdfs.PayslipMaria);

        // Run 1 as a model calls the tools: it sees the phone masked ([PHONE_1]) and passes it on like that.
        model.CallTool("getCase", new { caseCode = CaseCode })
            .CallTool("listDocuments", new { clientCode = "CL-0001" })
            .CallTool("readDocument", new { caseCode = CaseCode, documentId = payslip })
            .CallTool("recordEvidence", new { caseCode = CaseCode, documentId = payslip, page = 1, quote = "Telefon: [PHONE_1]", ruleId = "CRS-IND-PHONE" })
            .CallTool("recordSignal", new { caseCode = CaseCode, description = "Salariu de la un angajator german", documentId = payslip, page = 1, quote = "Angajator: Müller Logistik GmbH" })
            .CallTool("draftClientRequest", new { caseCode = CaseCode, body = "Bună ziua, vă rugăm să ne trimiteți o autocertificare nouă până la 26.01.2027." })
            .CallTool("sendClientRequest", new { caseCode = CaseCode, requestId = 1 })
            .Reply("Cazul CRS-2026-0001: CRS-IND-ADDR (DE, p. 29) și CRS-IND-PHONE (DE, p. 29). Semnal, fără alertă: „Angajator: Müller Logistik GmbH”. Cererea așteaptă aprobarea.");

        var run1 = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(CaseCode));

        Assert.Equal(RunSource.Live, run1.Source);
        Assert.Equal(RunStatus.AwaitingApproval, run1.Status);
        run1.ChecksPass();
        var evidence = Assert.Single((await host.CaseAsync(CaseCode))!.Evidence, e => e.LinkedRuleId == "CRS-IND-PHONE");
        Assert.True(evidence.Verified);
        Assert.Equal("Telefon: +49 89 4521 7730", evidence.Quote); // the tool got the real value back
        var seen = model.Calls.SelectMany(c => c).SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? "");
        Assert.DoesNotContain(seen, r => r.Contains("4521 7730")); // the model saw only [PHONE_1]

        await host.DecideAsync(run1, SentinelTools.SendClientRequest);
        clock.Advance(TimeSpan.FromDays(85));
        await host.Get<ICaseService>().TickAsync(clock.UtcNow);
        clock.Advance(TimeSpan.FromDays(1));
        var documentId = await host.Get<IDemoScenarios>().DeliverResponseAsync(CaseCode);

        // The small model reads the form like the mock would; code verifies every quote.
        var shielded = await host.Get<IShieldedDocuments>().GetAsync(documentId);
        extract.Json(SelfCertExtractor.Parse(shielded!.VisiblePages));
        model.CallTool("getCase", new { caseCode = CaseCode })
            .CallTool("extractSelfCertification", new { caseCode = CaseCode, documentId })
            .CallTool("checkTin", new { country = "DE", tin = "86095174223" })
            .CallTool("registerResponse", new { caseCode = CaseCode, kind = "NewSelfCertification", documentId })
            .CallTool("evaluateRules", new { clientCode = "CL-0001" })
            .CallTool("closeCase", new { caseCode = CaseCode, channel = "Online" })
            .Reply("Cazul CRS-2026-0001: autocertificare nouă, DE, TIN format valid. Reevaluare: Compliant. Închiderea așteaptă aprobarea.");

        var run2 = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Verify(CaseCode));

        Assert.Equal(RunStatus.AwaitingApproval, run2.Status);
        Assert.Equal(["getCase", "extractSelfCertification", "checkTin", "registerResponse", "evaluateRules"], run2.ToolCalls());
        run2.ChecksPass();
        var extracted = JsonDocument.Parse(run2.Steps.Single(s => s is { Kind: StepKind.ToolResult, Tool: "extractSelfCertification" }).Output!).RootElement;
        Assert.Equal("ai", extracted.GetProperty("source").GetString());
        Assert.True(extracted.GetProperty("recorded").GetBoolean());
        Assert.Equal(CaseStatus.ResponseReceived, (await host.CaseAsync(CaseCode))!.Status);

        await host.DecideAsync(run2, SentinelTools.CloseCase);
        Assert.Equal(CaseStatus.Closed, (await host.CaseAsync(CaseCode))!.Status);
        Assert.True((await host.VerifyAuditAsync()).Ok);
    }
}
