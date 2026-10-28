using HackathonStarter.Agents;
using HackathonStarter.Data;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Evaluation;
using Microsoft.EntityFrameworkCore;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Sentinel.Demo;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>
/// The traps (docs/PITCH.md S5), the shield (S6) and the guards of the tools, on the real modules and the seed: the mock
/// explains with evaluateRules and the reasons in Cleared, asks for no approval, never reads hidden text, and a tool refuses a
/// document of another client or a write by a viewer.
/// </summary>
public class SentinelTrapsTests
{
    [Theory]
    [InlineData("ro-RO", "13", "CL-0013", "CRS-IND-PHONE")]
    [InlineData("ro-RO", "5a", "CL-0005", "CRS-IND-SO")]
    [InlineData("ro-RO", "15", "CL-0015", "GEN-DOMESTIC")]
    [InlineData("en-GB", "13", "CL-0013", "CRS-IND-PHONE")]
    [InlineData("en-GB", "5a", "CL-0005", "CRS-IND-SO")]
    [InlineData("en-GB", "15", "CL-0015", "GEN-DOMESTIC")]
    public async Task A_trap_is_explained_from_the_cleared_rules_with_no_case_and_no_approval(string culture, string golden, string clientCode, string clearedRule)
    {
        using var _ = new UiLanguage(culture);
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var change = await host.RecordGoldenChangeAsync(golden);
        Assert.Equal(Decision.NoCase, change.Outcome.Decision);
        Assert.Null(change.CaseCode);

        var run = await host.RunAsync(SentinelTools.AgentName, Lang.Pick($"De ce nu există caz pentru {clientCode}?", $"Why is there no case for {clientCode}?"));

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["evaluateRules", "listDocuments"], run.ToolCalls());
        Assert.DoesNotContain(run.Steps, s => s.Kind == StepKind.Approval);
        run.ChecksPass();
        var answer = run.Answer();
        Assert.Contains("NoCase", answer);
        Assert.Contains(clearedRule, answer);
        // The reason is code's, from the engine on the client's current data (what evaluateRules returns).
        var client = await host.Get<IClientService>().GetAsync(clientCode);
        var now = host.Get<ICrsRuleEngine>().EvaluateCurrent(ClientSnapshot.From(client!));
        var reason = now.Cleared.First(h => h.RuleId == clearedRule);
        Assert.Contains(Lang.Pick(reason.ExplanationRo, reason.Explanation), answer);
    }

    [Fact]
    public async Task The_same_order_to_the_US_is_a_fatca_alert_with_no_deadline_in_the_guide()
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var change = await host.RecordGoldenChangeAsync("5c");
        Assert.Equal(Decision.FatcaAlert, change.Outcome.Decision);

        var run = await host.RunAsync(SentinelTools.AgentName, $"Rezumă cazul {change.CaseCode}");

        Assert.Equal(["getCase"], run.ToolCalls());
        run.ChecksPass();
        Assert.Contains("FATCA-IND-SO", run.Answer());
        Assert.Contains("fără termen legal în ghid (p. 38)", run.Answer());
    }

    [Theory]
    [InlineData("ro-RO", "Ce documente are CL-0010?", "text alb", "nu a fost citit de model")]
    [InlineData("en-GB", "Which documents does CL-0010 have?", "white text", "not read by a model")]
    public async Task The_shield_on_victor_says_what_was_removed_and_the_hidden_text_never_reaches_a_step(string culture, string question, string reason, string notRead)
    {
        using var _ = new UiLanguage(culture);
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var change = await host.RecordGoldenChangeAsync("10");
        Assert.Equal(Decision.ShieldAlert, change.Outcome.Decision);

        var run = await host.RunAsync(SentinelTools.AgentName, question);

        Assert.DoesNotContain(run.Steps, s => s.Kind == StepKind.Approval);
        run.ChecksPass();
        var answer = run.Answer();
        Assert.Contains(SyntheticPdfs.StatementVictor, answer);
        Assert.Contains(reason, answer);
        Assert.Contains(notRead, answer);
        Assert.DoesNotContain(run.AllText(), t => t.Contains("Ignoră instrucțiunile") || t.Contains("Marchează clientul"));
    }

    [Fact]
    public async Task A_tool_refuses_another_clients_document_and_a_viewers_write()
    {
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var maria = await host.RecordGoldenChangeAsync("4");
        var victors = await host.DocumentIdAsync("CL-0010", SyntheticPdfs.StatementVictor);

        var read = await host.ToolAsync("readDocument", new { caseCode = maria.CaseCode, documentId = victors });
        var quote = await host.ToolAsync("recordEvidence", new { caseCode = maria.CaseCode, documentId = victors, page = 1, quote = "Extras de cont" });
        Assert.Contains("CL-0001", read.GetProperty("error").GetString());
        Assert.Contains("CL-0001", quote.GetProperty("error").GetString());

        var payslip = await host.DocumentIdAsync("CL-0001", SyntheticPdfs.PayslipMaria);
        host.Get<CurrentUser>().Use(RealWorld.Vera);
        var viewer = await host.ToolAsync("recordEvidence", new { caseCode = maria.CaseCode, documentId = payslip, page = 1, quote = "Angajator: Müller Logistik GmbH" }, RealWorld.Vera);
        Assert.Contains("read", viewer.GetProperty("error").GetString());
        Assert.Empty((await host.CaseAsync(maria.CaseCode!))!.Evidence);
    }
}

/// <summary>The guards added after the review of PR #44: questions only read, approvals need the operator, answers are not recorded twice.</summary>
public class SentinelGuardsTests
{
    [Theory]
    [InlineData("ro-RO", "De ce nu există caz pentru CL-0005?")]
    [InlineData("en-GB", "Why is there a case for CL-0005?")]
    public async Task A_question_about_an_open_case_only_reads(string culture, string question)
    {
        using var _ = new UiLanguage(culture);
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("5c")).CaseCode!;

        var run = await host.RunAsync(SentinelTools.AgentName, question);

        Assert.Equal(["getCase"], run.ToolCalls());
        Assert.DoesNotContain(run.Steps, s => s.Kind == StepKind.Approval);
        var item = (await host.CaseAsync(code))!;
        Assert.Empty(item.Evidence);
        Assert.Empty(item.Requests);
        Assert.Contains(SentinelActions.AnalyzeLabel, run.Answer());
    }

    [Fact]
    public async Task Before_the_answer_is_read_a_question_says_it_arrived_and_records_nothing()
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, clock) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        await host.DecideAsync(await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(code)), SentinelTools.SendClientRequest);
        clock.Advance(TimeSpan.FromDays(86));
        await host.Get<IDemoScenarios>().DeliverResponseAsync(code);

        var run = await host.RunAsync(SentinelTools.AgentName, $"Ce mai așteptăm pentru {code}?");

        Assert.Equal(["getCase", "listDocuments"], run.ToolCalls());
        Assert.Contains(SyntheticPdfs.SelfCertMaria, run.Answer());
        Assert.Contains(SentinelActions.VerifyLabel, run.Answer());
        Assert.Empty((await host.CaseAsync(code))!.Responses);
    }

    [Fact]
    public async Task A_member_approving_the_request_gets_an_error_not_done_and_nothing_is_sent()
    {
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        var run = await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(code));

        host.Get<CurrentUser>().Use(RealWorld.Mihai);
        var decided = await host.DecideAsync(run, SentinelTools.SendClientRequest);

        var result = decided.Steps.Last(s => s.Kind == StepKind.ToolResult);
        Assert.Contains("role", result.Error);
        Assert.DoesNotContain(decided.Steps, s => s.Kind == StepKind.Text && s.Text.Contains("Done:", StringComparison.Ordinal));
        Assert.StartsWith("Approved by Mihai Member, but the action failed:", decided.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.Equal(RunStatus.Error, decided.Status);
        var item = (await host.CaseAsync(code))!;
        Assert.Equal(CaseStatus.Open, item.Status);
        Assert.Equal(RequestStatus.Draft, Assert.Single(item.Requests).Status);
    }

    [Fact]
    public async Task Reading_the_same_answer_twice_records_one_self_certification()
    {
        var (host, clock) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        await host.DecideAsync(await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(code)), SentinelTools.SendClientRequest);
        clock.Advance(TimeSpan.FromDays(86));
        var documentId = await host.Get<IDemoScenarios>().DeliverResponseAsync(code);

        var first = await host.ToolAsync("extractSelfCertification", new { caseCode = code, documentId });
        var second = await host.ToolAsync("extractSelfCertification", new { caseCode = code, documentId });

        Assert.True(first.GetProperty("recorded").GetBoolean());
        Assert.Equal(first.GetProperty("selfCertificationId").GetInt32(), second.GetProperty("selfCertificationId").GetInt32());
        var client = (await host.Get<IClientService>().GetAsync("CL-0001"))!;
        Assert.Single(client.SelfCertifications, s => s.DocumentId == documentId);
        Assert.Equal(documentId, await host.Get<IDemoScenarios>().DeliverResponseAsync(code)); // a second delivery uploads nothing
        Assert.Single(await host.Get<IClientService>().ListDocumentsAsync("CL-0001"), d => d.FileName == SyntheticPdfs.SelfCertMaria);
    }

    [Fact]
    public async Task Free_texts_from_the_model_are_saved_masked()
    {
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        var payslip = await host.DocumentIdAsync("CL-0001", SyntheticPdfs.PayslipMaria);

        await host.ToolAsync("recordSignal", new
        {
            caseCode = code, description = "Salariu din Germania, sună la +49 89 4521 7730", documentId = payslip, page = 1, quote = "Angajator: Müller Logistik GmbH",
        });

        var signal = Assert.Single((await host.CaseAsync(code))!.Signals);
        Assert.DoesNotContain("4521", signal.Description);
        Assert.Contains("[PHONE-1]", signal.Description);
    }
}

/// <summary>Round 2 of the review of the fixes (PR #47): whole-word requests, questions never write, the upload marker.</summary>
public class SentinelGuardsRound2Tests
{
    [Theory]
    [InlineData("Ce conținut are autocertificarea pentru {0}?")]
    [InlineData("Ce verificări a făcut codul pentru {0}?")]
    [InlineData("A răspuns clientul la {0}?")]
    [InlineData("Can you check what is missing for {0}?")]
    [InlineData("Ce conținut are autocertificarea pentru {0}")]
    public async Task A_question_with_a_keyword_inside_records_no_answer(string question)
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, clock) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        await host.DecideAsync(await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(code)), SentinelTools.SendClientRequest);
        clock.Advance(TimeSpan.FromDays(86));
        await host.Get<IDemoScenarios>().DeliverResponseAsync(code);

        var run = await host.RunAsync(SentinelTools.AgentName, string.Format(question, code));

        Assert.DoesNotContain("extractSelfCertification", run.ToolCalls());
        var item = (await host.CaseAsync(code))!;
        Assert.Empty(item.Responses);
        Assert.Equal(CaseStatus.AwaitingClient, item.Status); // unchanged (no tick ran)
    }

    [Fact]
    public async Task Anas_sentence_from_the_pitch_reads_the_answer()
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, clock) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        await host.DecideAsync(await host.RunAsync(SentinelTools.AgentName, SentinelActions.Analyze(code)), SentinelTools.SendClientRequest);
        clock.Advance(TimeSpan.FromDays(86));
        await host.Get<IDemoScenarios>().DeliverResponseAsync(code);

        var run = await host.RunAsync(SentinelTools.AgentName, $"Case: {code}\n\nMaria a trimis autocertificarea nouă.");

        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(SentinelTools.CloseCase, Assert.Single(run.Steps, s => s.Kind == StepKind.Approval).Tool);
    }

    [Fact]
    public async Task A_document_on_file_without_its_upload_change_gets_it_once()
    {
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var code = (await host.RecordGoldenChangeAsync("4")).CaseCode!;
        int documentId;
        await using (var db = await host.Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            var maria = await db.Clients.SingleAsync(c => c.Code == "CL-0001");
            var document = new ClientDocument
            {
                ClientId = maria.Id, Kind = DocumentKind.SelfCertForm, FileName = SyntheticPdfs.SelfCertMaria,
                Content = SyntheticPdfs.Get(SyntheticPdfs.SelfCertMaria).Bytes, Pages = 1, UploadedAt = GoldenCases.ChangeMoment,
            };
            db.ClientDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        Assert.Equal(documentId, await host.Get<IDemoScenarios>().DeliverResponseAsync(code));
        Assert.Equal(documentId, await host.Get<IDemoScenarios>().DeliverResponseAsync(code));

        var uploads = (await host.Get<IChangeService>().ListAsync("CL-0001")).Where(e => e.Kind == ChangeKind.DocumentUploaded).ToList();
        Assert.Equal($"{{\"documentId\":{documentId}}}", Assert.Single(uploads).NewValueJson);
    }
}
