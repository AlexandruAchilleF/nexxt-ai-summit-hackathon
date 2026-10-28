using HackathonStarter.Agents;

namespace HackathonStarter.Features.Arena;

/// <summary>
/// Example MULTI-AGENT PANEL: two agents argue, a judge scores them.
/// Swap the personas for your use case: proposer/critic, customer/advisor, analyst/risk officer…
/// </summary>
public class DebatePanel : PanelAgent
{
    public override string Name => "debate";
    public override string Label => "Agent Arena";
    public override string Description => "Two agents debate a question, then a judge scores their reasoning.";

    public override string[] Suggestions =>
    [
        "Should banks let AI agents approve small loans automatically?",
        "Should a 25-year-old invest in index funds or pay off debt first?",
    ];

    public override PanelParticipant[] Participants =>
    [
        new("Advocate", "Argues FOR the proposal with concrete benefits",
            "You are the Advocate. Argue FOR the proposal. Use concrete, specific arguments and answer the Skeptic's objections directly. Be persuasive but honest."),
        new("Skeptic", "Challenges assumptions and finds risks",
            "You are the Skeptic. Challenge the proposal: find weak assumptions, risks and missing evidence. Attack the reasoning, not the person. Concede strong points."),
    ];

    public override PanelParticipant Judge => new("Judge", "Scores the participants",
        "You are an impartial judge. Score each participant on the quality of reasoning, evidence and how well they answered the other side. Ignore style.");

    public override PanelScript Mock(string topic) => new(
        [
            ("Advocate", $"On \"{topic}\": yes. It removes repetitive work, gives answers in seconds instead of days, and frees experts for the hard cases. Start with a narrow, measurable scope and the benefits show up in the first month."),
            ("Skeptic", "Speed is not the goal; correct decisions are. Who is accountable when it is wrong? Without an audit trail and a human checkpoint, one bad decision can cost more than a year of savings."),
            ("Advocate", "Agreed on accountability, which is why the design keeps a human approval step and logs every action. Low-risk cases are automated; anything unusual is escalated. That keeps most of the speed."),
            ("Skeptic", "Better. But 'low-risk' must be defined with data, not intuition, and monitored for drift. Show error rates from a pilot before scaling, and I'd support it."),
        ],
        Winner: "Advocate",
        Scores: new() { ["Advocate"] = 8, ["Skeptic"] = 7 },
        Rationale: "The Advocate adapted the proposal to the Skeptic's strongest objection (accountability) with a concrete mechanism. The Skeptic raised the key risks but conceded without new evidence.");
}
