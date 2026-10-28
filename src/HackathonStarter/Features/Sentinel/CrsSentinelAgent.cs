using HackathonStarter.Agents;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// The CRS Sentinel agent (docs/PLAN.md section 13). The model reads, quotes and writes; it never decides an indicium, a
/// deadline or an alert: it calls the code for that (<see cref="SentinelTools"/>). Sending a request and closing a case wait
/// for a person. Without a model the mock (<see cref="SentinelMock"/>) runs the same tools on the case in the database.
/// Owner: R4.
/// </summary>
public class CrsSentinelAgent : ToolAgent
{
    public override string Name => "crs-sentinel";
    public override string Label => "CRS Sentinel";

    public override string Description => Services.Lang.Pick(
        "Pregătește cazurile CRS/FATCA: dovezi cu citate verificate, indiciile legale și cererea către client. Un om aprobă.",
        "Prepares CRS/FATCA cases: evidence with verified quotes, the legal indicia and the request to the client. A person approves.");

    public override string[] Suggestions => Services.Lang.Pick<string[]>(
        ["Pregătește cazul", "Clientul a răspuns: verifică autocertificarea", "Rezumă cazul"],
        ["Prepare the case", "The client answered: check the self-certification", "Summarize the case"]);

    /// <summary>The integrator adds the CRS check ids (evidence-quoted, signals-never-alert, no-tin-valid-claim, rule-cited).</summary>
    public override string[] Checks => ["pii-leak"];

    /// <summary>Run 1 is seven tool calls and run 2 seven more; leave room for a re-read.</summary>
    public override int? MaxSteps => 14;

    public override string SystemPrompt(ToolContext ctx) => SentinelPrompt.Text;

    public override IEnumerable<AgentTool> Tools(ToolContext ctx) => new SentinelTools(ctx.Services).All();

    public override Task MockAsync(MockContext m) => SentinelMock.RunAsync(m);
}

public static class SentinelSetup
{
    public static IServiceCollection AddSentinel(this IServiceCollection services) =>
        services.AddSingleton<AgentDefinition, CrsSentinelAgent>();
}
