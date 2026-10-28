using HackathonStarter.Agents;
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
    [InlineData("13", "CL-0013", "CRS-IND-PHONE")]
    [InlineData("5a", "CL-0005", "CRS-IND-SO")]
    [InlineData("15", "CL-0015", "GEN-DOMESTIC")]
    public async Task A_trap_is_explained_from_the_cleared_rules_with_no_case_and_no_approval(string golden, string clientCode, string clearedRule)
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var change = await host.RecordGoldenChangeAsync(golden);
        Assert.Equal(Decision.NoCase, change.Outcome.Decision);
        Assert.Null(change.CaseCode);

        var run = await host.RunAsync(SentinelTools.AgentName, $"De ce nu există caz pentru {clientCode}?");

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
        Assert.Contains(now.Cleared.First(h => h.RuleId == clearedRule).ExplanationRo, answer);
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

    [Fact]
    public async Task The_shield_on_victor_says_what_was_removed_and_the_hidden_text_never_reaches_a_step()
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, _) = await RealWorld.CreateAsync();
        await using var __ = host;
        var change = await host.RecordGoldenChangeAsync("10");
        Assert.Equal(Decision.ShieldAlert, change.Outcome.Decision);

        var run = await host.RunAsync(SentinelTools.AgentName, "Ce documente are CL-0010?");

        Assert.DoesNotContain(run.Steps, s => s.Kind == StepKind.Approval);
        run.ChecksPass();
        var answer = run.Answer();
        Assert.Contains(SyntheticPdfs.StatementVictor, answer);
        Assert.Contains("text alb", answer);
        Assert.Contains("nu a fost citit de model", answer);
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
