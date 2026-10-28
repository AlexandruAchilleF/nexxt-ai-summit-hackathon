using System.ComponentModel;
using HackathonStarter.Agents;
using HackathonStarter.Features.Cases;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// Phase 0 skeleton of the CRS Sentinel agent (docs/PLAN.md section 13). The model reads, quotes and writes; it never
/// decides an indicium, a deadline or an alert: it calls the code for that. Owner: R4 (tools, prompt, mock RO/EN).
/// </summary>
public class CrsSentinelAgent : ToolAgent
{
    public override string Name => "crs-sentinel";
    public override string Label => "CRS Sentinel";
    public override string Description => "Prepares CRS/FATCA cases: evidence with verified quotes, the legal indicia and the request to the client. A person approves.";
    public override string[] Checks => ["pii-leak"];

    public override string SystemPrompt(ToolContext ctx) => """
        You are CRS Sentinel, the assistant of a bank's CRS/FATCA operations team.
        Never decide whether something is an indicium, a deadline or an alert: the tools (code) decide; you explain.
        Every statement about a client comes from the bank's data or from a verified quote.
        Context signals (income, employer, studies abroad) are signals with no alert, shown separately from indicia.
        Never write "TIN valid"; write "format valid".
        Cite the rule ID and the guide page for every indicium. Answer in the user's language.
        """;

    public override IEnumerable<AgentTool> Tools(ToolContext ctx)
    {
        var cases = ctx.Services.GetRequiredService<ICaseService>();

        yield return AgentTool.Create("getCase",
            "Get a case by code (e.g. CRS-2026-0001): status, deadline and the indicia with rule ID and guide page.",
            async ([Description("Case code, e.g. CRS-2026-0001")] string caseCode) =>
                await cases.GetAsync(caseCode) is { } c
                    ? (object)new
                    {
                        c.Code, Status = c.Status.ToString(), Regime = c.Regime.ToString(), c.DueAt,
                        Indicia = c.Indicia.Take(10).Select(i => new { i.RuleId, i.Jurisdiction, i.GuidePage, i.Explanation }),
                    }
                    : new { error = $"No case {caseCode}" });
    }

    public override Task MockAsync(MockContext m) =>
        m.SayAsync(Services.Lang.Pick(
            "Agentul CRS Sentinel e în construcție: uneltele și scenariile vin în faza 1.",
            "The CRS Sentinel agent is being built: its tools and scripts arrive in phase 1."));
}

public static class SentinelSetup
{
    public static IServiceCollection AddSentinel(this IServiceCollection services) =>
        services.AddSingleton<AgentDefinition, CrsSentinelAgent>();
}
