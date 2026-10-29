using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Sentinel;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>The system prompt names the tools that exist, in the order the mock and the video use, with the right approvals.</summary>
public class AgentPromptTests
{
    private static readonly string[] Run1 = ["getCase", "listDocuments", "readDocument", "recordEvidence", "recordSignal", "draftClientRequest", "sendClientRequest"];
    private static readonly string[] Run2 = ["getCase", "listDocuments", "extractSelfCertification", "checkTin", "registerResponse", "evaluateRules", "closeCase"];

    [Fact]
    public async Task The_prompt_names_every_tool_and_the_two_runs_in_order()
    {
        var world = new MariaWorld();
        await using var host = await world.HostAsync();
        var agent = (ToolAgent)host.Agent("crs-sentinel");
        var tools = agent.Tools(new ToolContext(SiteConfig.Users[0], [], host.Services)).ToList();

        Assert.StartsWith(SentinelPrompt.Text, agent.SystemPrompt(new ToolContext(SiteConfig.Users[0], [], host.Services)));
        Assert.All(tools, t => Assert.Contains(t.Name, SentinelPrompt.Text));
        Assert.Contains(string.Join(" -> ", Run1), SentinelPrompt.Text);
        Assert.Contains(string.Join(" -> ", Run2), SentinelPrompt.Text);
        foreach (var tool in tools.Where(t => t.NeedsApproval))
            Assert.Contains($"{tool.Name} waits for a person's approval", SentinelPrompt.Text);
        Assert.Equal(["pii-leak", "evidence-quoted"], agent.Checks);
    }

    // On live Bedrock with the UI in English, the answer came back in Romanian: the documents and tool results are in
    // Romanian, so code names the answer language from the UI language, on the prompt's last line.
    [Theory]
    [InlineData("en-US", "Answer language: English.")]
    [InlineData("ro-RO", "Answer language: the language of the user's message")]
    public async Task The_prompt_ends_with_the_answer_language_of_the_ui(string culture, string line)
    {
        using var _ = new UiLanguage(culture);
        var world = new MariaWorld();
        await using var host = await world.HostAsync();
        var agent = (ToolAgent)host.Agent("crs-sentinel");

        var prompt = agent.SystemPrompt(new ToolContext(SiteConfig.Users[0], [], host.Services));

        Assert.StartsWith(line, prompt.Split('\n')[^1]);
        Assert.DoesNotContain("call it \"telefonul din DE\".", prompt);
    }
}
