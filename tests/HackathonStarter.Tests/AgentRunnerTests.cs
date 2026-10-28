using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Checks;
using HackathonStarter.Features.Items;
using HackathonStarter.Tests.Crs.Sentinel;
using HackathonStarter.Tests.Support;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Tests;

/// <summary>The agent runtime with a scripted model: no network, deterministic.</summary>
public class AgentRunnerTests
{
    [Fact]
    public async Task Native_tool_call_then_answer_records_steps_and_usage()
    {
        var model = new FakeChatClient().CallTool("getStats", new { }).Reply("You have 12 items.");
        await using var host = await TestHost.CreateAsync(model, ai => { ai.InputPricePerMillion = 3; ai.OutputPricePerMillion = 15; });

        var run = await host.RunAsync("assistant", "Give me an overview");

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.Contains(run.Steps, s => s is { Kind: StepKind.ToolCall, Tool: "getStats" });
        Assert.Contains(run.Steps, s => s.Kind == StepKind.ToolResult && s.Output!.Contains("\"total\""));
        Assert.Equal("You have 12 items.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.True(run.InputTokens > 0 && run.OutputTokens > 0);
        Assert.NotNull(run.EstimatedCost);
        Assert.Equal("fake-model", run.Model);
    }

    [Fact]
    public async Task Falls_back_to_json_mode_when_the_model_rejects_tools()
    {
        var model = new FakeChatClient { RejectTools = true }
            .Json(new { thought = "Look up stats first", tool = "getStats", input = new { }, answer = (string?)null })
            .Json(new { thought = "Done", tool = (string?)null, input = (object?)null, answer = "From JSON mode." });
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("assistant", "overview");

        Assert.Equal(RunSource.Live, run.Source);
        Assert.Contains(run.Steps, s => s is { Kind: StepKind.Reasoning, Text: "Look up stats first" });
        Assert.Contains(run.Steps, s => s is { Kind: StepKind.ToolResult, Tool: "getStats" });
        Assert.Equal("From JSON mode.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Model_failure_falls_back_to_the_scripted_mock()
    {
        var model = new FakeChatClient().Fail("model overloaded").Fail("model overloaded");
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("assistant", "Give me an overview of our items");

        Assert.Equal(RunSource.Mock, run.Source);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Contains("overloaded", run.FallbackReason);
        Assert.Contains(run.Steps, s => s.Kind == StepKind.Text && s.Text.StartsWith("You have"));
    }

    [Fact]
    public async Task Replays_a_recording_for_a_similar_question()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        host.AddRecording("assistant", "Which items are active right now?",
            new AgentStep { Kind = StepKind.Text, Text = "Recorded answer." });

        var run = await host.RunAsync("assistant", "which items are ACTIVE now");

        Assert.Equal(RunSource.Replay, run.Source);
        Assert.Equal("Which items are active right now?", run.ReplayOf);
        Assert.Equal("Recorded answer.", run.Steps.Single(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Does_not_replay_a_recording_for_an_unrelated_question()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        host.AddRecording("assistant", "Which items are active right now?",
            new AgentStep { Kind = StepKind.Text, Text = "Recorded answer." });

        var run = await host.RunAsync("assistant", "Monthly payment for a 20000 loan at 6% over 3 years?");

        Assert.Equal(RunSource.Mock, run.Source);
        Assert.Null(run.ReplayOf);
        Assert.DoesNotContain(run.Steps, s => s.Text == "Recorded answer.");
        Assert.Contains(run.Steps, s => s is { Kind: StepKind.ToolCall, Tool: "calculateLoan" });
    }

    [Fact]
    public async Task Approval_proposes_then_executes_only_when_approved()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        var target = (await host.Get<ItemService>().ListAsync(new ItemFilter("Old prototype"))).Single();

        var run = await host.RunAsync("assistant", "Archive the 'Old prototype' item");
        var approval = run.Steps.Single(s => s.Kind == StepKind.Approval);
        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(ItemStatus.Draft, (await host.Get<ItemService>().GetAsync(target.Id))!.Status); // not executed yet

        var decided = await host.Get<AgentRunner>().DecideAsync(host.Agent("assistant"), run.Id, approval.Id, approved: true);

        Assert.Equal(RunStatus.Completed, decided.Status);
        Assert.Equal(ApprovalState.Approved, decided.Steps.Single(s => s.Kind == StepKind.Approval).State);
        Assert.Equal(ItemStatus.Archived, (await host.Get<ItemService>().GetAsync(target.Id))!.Status);
    }

    [Fact]
    public async Task An_approved_action_that_fails_is_not_shown_as_done()
    {
        using var _ = new UiLanguage("en-GB"); // the runner writes the decision in the UI language
        var model = new FakeChatClient().CallTool("updateItem", new { id = 999, title = "Ghost", status = "Archived" }).Reply("proposed");
        await using var host = await TestHost.CreateAsync(model);
        var run = await host.RunAsync("assistant", "Archive the 'Ghost' item");
        var approval = run.Steps.Single(s => s.Kind == StepKind.Approval);

        var decided = await host.Get<AgentRunner>().DecideAsync(host.Agent("assistant"), run.Id, approval.Id, approved: true);

        Assert.Equal(RunStatus.Error, decided.Status);
        Assert.Equal("Item 999 not found", decided.Error);
        Assert.Equal("Item 999 not found", decided.Steps.Single(s => s.Kind == StepKind.ToolResult).Error);
        var card = decided.Steps.Single(s => s.Kind == StepKind.Approval);
        Assert.Equal((ApprovalState.Approved, "Item 999 not found"), (card.State!.Value, card.Error));
        Assert.Equal("Approved by Ana Admin, but the action failed: Item 999 not found.", decided.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.DoesNotContain(decided.Steps, s => s.Text.Contains("Done:"));
        Assert.Equal(RunStatus.Error, (await host.Get<RunStore>().GetAsync(run.Id))!.Status);
    }

    [Fact]
    public async Task A_live_proposal_gets_one_last_turn_without_tools_then_the_run_ends()
    {
        var model = new FakeChatClient()
            .CallTool("updateItem", new { id = 1, title = "Old prototype", status = "Archived" })
            .Reply("proposed")
            .CallTool("getStats", new { });
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("assistant", "Archive the 'Old prototype' item");

        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.Single(run.Steps, s => s is { Kind: StepKind.Approval, State: ApprovalState.Pending });
        Assert.DoesNotContain(run.Steps, s => s.Kind is StepKind.ToolCall or StepKind.ToolResult); // getStats never ran
        Assert.Equal("proposed", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.Equal(2, model.Calls.Count);
        Assert.IsType<NoneChatToolMode>(model.CallOptions[1]!.ToolMode);
    }

    [Fact]
    public async Task A_tool_called_in_the_turn_after_a_live_proposal_is_not_run()
    {
        var model = new FakeChatClient()
            .CallTool("updateItem", new { id = 1, title = "Old prototype", status = "Archived" })
            .CallTool("getStats", new { })
            .Reply("too late");
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("assistant", "Archive the 'Old prototype' item");

        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.DoesNotContain(run.Steps, s => s.Kind is StepKind.ToolCall or StepKind.ToolResult);
        Assert.Equal(2, model.Calls.Count);
        // The model ended its last turn without text: the run still tells the user what happens next.
        Assert.Equal("The proposed action needs human approval before it runs.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Tool_calls_after_a_proposal_in_the_same_batch_do_not_run()
    {
        var agent = new NotesAgent();
        var model = new FakeChatClient().CallTools(("publish", new { }), ("addNote", new { }), ("publish", new { })).Reply("proposed");
        await using var host = await TestHost.CreateAsync(model, extraAgents: agent);

        var run = await host.RunAsync("notes-agent", "Publish, then note it");

        Assert.Equal(0, agent.Notes);
        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Single(run.Steps, s => s.Kind == StepKind.Approval);
        Assert.DoesNotContain(run.Steps, s => s.Kind is StepKind.ToolCall or StepKind.ToolResult);
        // Every call of the batch gets a result (providers require one), saying why it did not run.
        var results = model.Calls[1].Single(m => m.Role == ChatRole.Tool).Contents.OfType<FunctionResultContent>().ToList();
        Assert.Equal(["call_1", "call_2", "call_3"], results.Select(r => r.CallId));
        Assert.Contains("awaiting_human_approval", results[0].Result?.ToString());
        Assert.All(results.Skip(1), r => Assert.Contains("not_executed", r.Result?.ToString()));
    }

    [Fact]
    public async Task A_json_mode_proposal_gets_one_last_turn_then_the_run_ends()
    {
        var model = new FakeChatClient()
            .Json(new { thought = "Propose it", tool = "updateItem", input = new { id = 1, title = "Old prototype", status = "Archived" }, answer = (string?)null })
            .Json(new { thought = "Tell the user", tool = (string?)null, input = (object?)null, answer = "proposed" })
            .Json(new { thought = "More data", tool = "getStats", input = new { }, answer = (string?)null });
        await using var host = await TestHost.CreateAsync(model, ai => ai.ToolMode = "Json");

        var run = await host.RunAsync("assistant", "Archive the 'Old prototype' item");

        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.DoesNotContain(run.Steps, s => s.Kind is StepKind.ToolCall or StepKind.ToolResult);
        Assert.Equal("proposed", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.Equal(2, model.Calls.Count);
        Assert.Contains(model.Calls[1], m => m.Role == ChatRole.User && m.Text.Contains("No tools are available"));
    }

    [Fact]
    public async Task A_tool_asked_for_after_a_json_mode_proposal_is_not_run()
    {
        var model = new FakeChatClient()
            .Json(new { thought = "Propose it", tool = "updateItem", input = new { id = 1, title = "Old prototype", status = "Archived" }, answer = (string?)null })
            .Json(new { thought = "More data", tool = "getStats", input = new { }, answer = (string?)null })
            .Json(new { thought = "Done", tool = (string?)null, input = (object?)null, answer = "too late" });
        await using var host = await TestHost.CreateAsync(model, ai => ai.ToolMode = "Json");

        var run = await host.RunAsync("assistant", "Archive the 'Old prototype' item");

        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.DoesNotContain(run.Steps, s => s.Kind is StepKind.ToolCall or StepKind.ToolResult);
        Assert.Contains("needs human approval", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.Equal(2, model.Calls.Count);
    }

    [Fact]
    public async Task A_model_failing_after_a_proposal_keeps_the_proposal_and_runs_no_tool_twice()
    {
        var agent = new NotesAgent();
        var model = new FakeChatClient().CallTool("addNote", new { }).CallTool("publish", new { }).Fail("model overloaded");
        await using var host = await TestHost.CreateAsync(model, extraAgents: agent);

        var run = await host.RunAsync("notes-agent", "Note it, then publish");

        Assert.Equal(1, agent.Notes); // the fallback mock would add the note a second time
        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.Null(run.FallbackReason);
        Assert.Single(run.Steps, s => s is { Kind: StepKind.ToolResult, Tool: "addNote" });
        Assert.Single(run.Steps, s => s is { Kind: StepKind.Approval, Tool: "publish", State: ApprovalState.Pending });
        Assert.Equal("The proposed action needs human approval before it runs.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task A_model_failing_after_tools_ran_fails_the_run_instead_of_running_them_again()
    {
        var agent = new NotesAgent();
        var model = new FakeChatClient().CallTool("addNote", new { }).Fail("model overloaded");
        await using var host = await TestHost.CreateAsync(model, extraAgents: agent);

        var run = await host.RunAsync("notes-agent", "Note it, then publish");

        Assert.Equal(1, agent.Notes);
        Assert.Equal(RunStatus.Error, run.Status);
        Assert.Equal("The live model failed after tools had run (model overloaded). The steps above were kept and not run again.", run.Error);
        Assert.Equal(RunSource.Live, run.Source);
        Assert.Single(run.Steps, s => s is { Kind: StepKind.ToolResult, Tool: "addNote" });
        Assert.DoesNotContain(run.Steps, s => s.Kind == StepKind.Approval);
    }

    [Fact]
    public async Task A_live_run_timing_out_after_a_tool_started_says_so_instead_of_falling_back()
    {
        var model = new FakeChatClient().CallTool("slow", new { });
        await using var host = await TestHost.CreateAsync(model, ai => ai.TimeoutSeconds = 1, extraAgents: new SlowAgent());

        var run = await host.RunAsync("slow-agent", "go");

        Assert.Equal(RunStatus.Error, run.Status);
        Assert.Contains("(no answer within 1s)", run.Error);
        Assert.DoesNotContain(run.Steps, s => s.Text == "mock");
    }

    [Fact]
    public async Task Checks_get_the_successful_tool_results_of_the_run()
    {
        var model = new FakeChatClient().CallTool("lookupClient", new { id = 7 }).CallTool("broken", new { }).Reply("Ana has 1200.50 EUR.");
        await using var host = await TestHost.CreateAsync(model, extraChecks: [new ToolResultsEcho()], extraAgents: new LookupAgent());

        var run = await host.RunAsync("lookup-agent", "Balance of client 7?");

        var fact = run.Steps.Single(s => s.Kind == StepKind.Check).Checks!.Single().Reason; // the echo check returns the fact
        using var doc = JsonDocument.Parse(fact);
        var result = Assert.Single(doc.RootElement.EnumerateArray()); // the failed call is left out
        Assert.Equal("lookupClient", result.GetProperty("tool").GetString());
        Assert.Equal(7, result.GetProperty("input").GetProperty("id").GetInt32());
        Assert.Equal("Ana", result.GetProperty("output").GetProperty("name").GetString()); // embedded as JSON, not as a string
    }

    [Fact]
    public async Task Viewer_cannot_approve()
    {
        await using var admin = await TestHost.CreateAsync(client: null);
        var run = await admin.RunAsync("assistant", "Archive the 'Old prototype' item");
        var approvalId = run.Steps.Single(s => s.Kind == StepKind.Approval).Id;

        admin.Get<HackathonStarter.Services.CurrentUser>().Use(SiteConfig.Users.Single(u => u.Role == Roles.Viewer));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            admin.Get<AgentRunner>().DecideAsync(admin.Agent("assistant"), run.Id, approvalId, approved: true));
    }

    [Fact]
    public async Task An_approval_of_a_run_still_running_is_refused_and_nothing_runs()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        var target = (await host.Get<ItemService>().ListAsync(new ItemFilter("Old prototype"))).Single();
        var run = await host.RunAsync("assistant", "Archive the 'Old prototype' item");
        var approval = run.Steps.Single(s => s.Kind == StepKind.Approval);
        run.Status = RunStatus.Running; // as saved while the run still executes
        await host.Get<RunStore>().SaveAsync(run);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Get<AgentRunner>().DecideAsync(host.Agent("assistant"), run.Id, approval.Id, approved: true));

        Assert.Equal(AgentRunner.StillRunning, error.Message);
        Assert.Equal(ItemStatus.Draft, (await host.Get<ItemService>().GetAsync(target.Id))!.Status);
        var saved = (await host.Get<RunStore>().GetAsync(run.Id))!;
        Assert.Equal(ApprovalState.Pending, saved.Steps.Single(s => s.Kind == StepKind.Approval).State);
        Assert.DoesNotContain(saved.Steps, s => s is { Kind: StepKind.ToolCall, Tool: "updateItem" });
    }

    [Fact]
    public async Task A_proposal_shown_while_the_model_still_writes_can_be_decided_only_once_the_run_ends()
    {
        var model = new FakeChatClient();
        await using var host = await TestHost.CreateAsync(model);
        var target = (await host.Get<ItemService>().ListAsync(new ItemFilter("Old prototype"))).Single();
        var writing = new TaskCompletionSource();
        using var release = new ManualResetEventSlim();
        model.CallTool("updateItem", new { id = target.Id, title = "Old prototype", status = "Archived" })
            .ReplyWith(_ =>
            {
                writing.TrySetResult(); // the approval card is on screen, the model writes its last turn
                release.Wait(TimeSpan.FromSeconds(10));
                return "proposed";
            });
        var service = (AgentRunService)host.Root.GetService(typeof(AgentRunService))!;

        var active = await service.StartAsync("assistant", [new ChatTurn("user", "Archive the 'Old prototype' item")], [], SiteConfig.Users[0], "conv-3");
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var approval = active.Run.Snapshot().Single(s => s.Kind == StepKind.Approval);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Get<AgentRunner>().DecideAsync(host.Agent("assistant"), active.Run.Id, approval.Id, approved: true));
        Assert.Equal(AgentRunner.StillRunning, error.Message);
        Assert.Equal(ItemStatus.Draft, (await host.Get<ItemService>().GetAsync(target.Id))!.Status);

        release.Set();
        var run = await active.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        var decided = await host.Get<AgentRunner>().DecideAsync(host.Agent("assistant"), run.Id, approval.Id, approved: true);
        Assert.Equal(RunStatus.Completed, decided.Status);
        Assert.Equal(ItemStatus.Archived, (await host.Get<ItemService>().GetAsync(target.Id))!.Status);
    }

    [Fact]
    public async Task Approving_twice_at_once_runs_the_action_once()
    {
        var agent = new NotesAgent();
        var model = new FakeChatClient().CallTool("publish", new { }).Reply("proposed");
        await using var host = await TestHost.CreateAsync(model, extraAgents: agent);
        var run = await host.RunAsync("notes-agent", "Publish the notes");
        var approval = run.Steps.Single(s => s.Kind == StepKind.Approval);
        var runner = host.Get<AgentRunner>();

        // A double click (or a second tab): the second decision arrives while the first still runs the action.
        var decisions = Enumerable.Range(0, 2).Select(_ => Task.Run(() => runner.DecideAsync(agent, run.Id, approval.Id, approved: true))).ToList();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.WhenAll(decisions));

        Assert.Equal(1, agent.Published);
        Assert.Equal(1, decisions.Count(d => d.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task Cancelling_marks_the_run_cancelled()
    {
        var model = new FakeChatClient { Delay = TimeSpan.FromSeconds(10) }.Reply("too late");
        await using var host = await TestHost.CreateAsync(model);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var run = await host.RunAsync("assistant", "overview", ct: cts.Token);

        Assert.Equal(RunStatus.Cancelled, run.Status);
    }

    [Fact]
    public async Task A_slow_tool_times_out_and_the_agent_continues()
    {
        var model = new FakeChatClient().CallTool("slow", new { }).Reply("The tool was too slow.");
        await using var host = await TestHost.CreateAsync(model, ai => ai.ToolTimeoutSeconds = 1, extraAgents: new SlowAgent());

        var run = await host.RunAsync("slow-agent", "go");

        var result = run.Steps.Single(s => s.Kind == StepKind.ToolResult);
        Assert.Contains("timed out", result.Error);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal("The tool was too slow.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Rate_limit_switches_to_fallback_without_calling_the_model()
    {
        var model = new FakeChatClient().Reply("first").Reply("second");
        await using var host = await TestHost.CreateAsync(model, security: s => s.AgentRunsPerMinute = 1);

        var first = await host.RunAsync("assistant", "hello");
        var second = await host.RunAsync("assistant", "Give me an overview");

        Assert.Equal(RunSource.Live, first.Source);
        Assert.Equal(RunSource.Mock, second.Source);
        Assert.Contains("rate limit", second.FallbackReason);
        Assert.Single(model.Calls);
    }

    [Fact]
    public async Task Personal_data_is_masked_for_the_model_and_restored_for_the_user()
    {
        const string iban = "RO49AAAA1B31007593840000";
        // The "model" echoes what it received, so we can see exactly what left the app.
        var model = new FakeChatClient().ReplyWith(m => "I see: " + m.Last(x => x.Role.Value == "user").Text);
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("assistant", $"Pay {iban} and email ana.pop@example.com");

        Assert.DoesNotContain(iban, model.AllUserText);
        Assert.DoesNotContain("ana.pop@example.com", model.AllUserText);
        Assert.Contains("[IBAN_1]", model.AllUserText);
        Assert.Contains(iban, run.Steps.Last(s => s.Kind == StepKind.Text).Text); // restored in the answer
        Assert.Equal(2, run.MaskedCount);
        Assert.StartsWith("Privacy: 2 personal data item(s)", run.Steps[0].Text);
    }

    [Fact]
    public async Task Tools_receive_the_real_values_behind_placeholders()
    {
        var model = new FakeChatClient().CallTool("validateIban", new { iban = "[IBAN_1]" }).Reply("Checked [IBAN_1].");
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("assistant", "Is RO49AAAA1B31007593840000 valid?");

        var result = run.Steps.Single(s => s.Kind == StepKind.ToolResult);
        Assert.Contains("\"isValid\":true", result.Output!.Replace(" ", "").Replace("\n", ""));
        Assert.Contains("RO49AAAA1B31007593840000", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Images_are_sent_to_the_model_with_the_last_user_message()
    {
        var model = new FakeChatClient().Reply("A receipt for 42 EUR.");
        await using var host = await TestHost.CreateAsync(model);
        var photo = HackathonStarter.Services.DocumentText.FromImage([1, 2, 3], "receipt.jpg");

        var run = await host.RunAsync("assistant", "What is this?", [photo]);

        var user = model.Calls[0].Last(m => m.Role == ChatRole.User);
        var image = Assert.Single(user.Contents.OfType<DataContent>());
        Assert.Equal("image/jpeg", image.MediaType);
        Assert.Contains("What is this?", user.Text);
        Assert.Contains(run.Steps, s => s.Text.StartsWith("Privacy: 1 image(s)"));   // privacy note: images aren't masked
        Assert.Equal("A receipt for 42 EUR.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Without_a_model_an_image_gets_an_honest_mock_answer()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        var photo = HackathonStarter.Services.DocumentText.FromImage([1, 2, 3], "receipt.jpg");

        var run = await host.RunAsync("assistant", "What is this?", [photo]);

        Assert.Contains("vision-capable model", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
    }

    [Fact]
    public async Task Panel_runs_rounds_then_a_judge_verdict()
    {
        var model = new FakeChatClient().Reply("For.").Reply("Against.").Reply("For again.").Reply("Against again.")
            .Json(new { winner = "Skeptic", scores = new[] { new { name = "Advocate", value = 6 }, new { name = "Skeptic", value = 8 } }, rationale = "Better evidence." });
        await using var host = await TestHost.CreateAsync(model);

        var run = await host.RunAsync("debate", "Should we?");

        Assert.Equal(RunSource.Live, run.Source);
        Assert.Equal(4, run.Steps.Count(s => s.Kind == StepKind.Text && s.Agent is not null));
        var verdict = run.Steps.Single(s => s.Kind == StepKind.Verdict);
        Assert.Equal("Skeptic", verdict.Winner);
        Assert.Equal(8, verdict.Scores!["Skeptic"]);
    }

    [Fact]
    public async Task Background_runs_are_saved_and_restorable_by_conversation()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        var service = host.Root.GetService(typeof(AgentRunService)) as AgentRunService;

        var active = service!.Start("assistant", [new ChatTurn("user", "Give me an overview")], [], SiteConfig.Users[0], "conv-1");
        await active.Completion;

        var restored = await host.Get<RunStore>().ListByConversationAsync("conv-1");
        Assert.Single(restored);
        Assert.Equal(RunStatus.Completed, restored[0].Status);
        Assert.Contains(restored[0].Steps, s => s.Kind == StepKind.Text && s.Text.StartsWith("You have"));
    }

    [Fact]
    public async Task A_stuck_subscriber_does_not_stall_a_background_run()
    {
        // e.g. a page whose Wi-Fi dropped: its circuit is kept, and its JS calls hang for up to a minute.
        await using var host = await TestHost.CreateAsync(client: null);
        var service = (AgentRunService)host.Root.GetService(typeof(AgentRunService))!;

        var active = await service.StartAsync("assistant", [new ChatTurn("user", "Give me an overview")], [], SiteConfig.Users[0], "conv-2");
        active.Updated += _ => new TaskCompletionSource().Task; // never completes

        var run = await active.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(RunStatus.Completed, run.Status);
    }

    [Fact]
    public void History_is_trimmed_to_the_most_recent_turns()
    {
        var history = Enumerable.Range(1, 10).Select(i => new ChatTurn(i % 2 == 1 ? "user" : "assistant", new string('x', 100) + i)).ToList();

        var trimmed = AgentRunner.TrimHistory(history, maxChars: 350);

        Assert.Equal(3, trimmed.Count);
        Assert.EndsWith("10", trimmed[^1].Content);
        Assert.Single(AgentRunner.TrimHistory([new ChatTurn("user", new string('y', 5000))], maxChars: 10)); // last one always kept
    }

    private sealed class LookupAgent : ToolAgent
    {
        public override string Name => "lookup-agent";
        public override string Label => "Lookup";
        public override string Description => "Has a tool that returns an object and one that fails.";
        public override string[] Checks => ["echo-tool-results"];
        public override string SystemPrompt(ToolContext ctx) => "Test agent.";
        public override IEnumerable<AgentTool> Tools(ToolContext ctx) =>
        [
            AgentTool.Create("lookupClient", "Returns a client.", (int id) => new { id, name = "Ana", balance = 1200.50m }),
            AgentTool.Create("broken", "Always fails.", string () => throw new InvalidOperationException("down")),
        ];
        public override Task MockAsync(MockContext mock) => mock.SayAsync("mock");
    }

    /// <summary>Test only: passes, with the "tool-results" fact as its reason.</summary>
    private sealed class ToolResultsEcho : Check
    {
        public override string Id => "echo-tool-results";
        public override string Title => "Echo tool results";
        public override string Description => "Returns the tool-results fact.";
        public override CheckResult Evaluate(CheckSubject subject) => Pass(subject.Fact("tool-results") ?? "missing");
    }

    /// <summary>A tool that changes data without approval (counted) and one that needs approval; its mock calls both.</summary>
    private sealed class NotesAgent : ToolAgent
    {
        public int Notes;
        public int Published;
        public override string Name => "notes-agent";
        public override string Label => "Notes";
        public override string Description => "Adds a note, then proposes to publish it.";
        public override string SystemPrompt(ToolContext ctx) => "Test agent.";
        public override IEnumerable<AgentTool> Tools(ToolContext ctx) =>
        [
            AgentTool.Create("addNote", "Adds a note.", () => ++Notes),
            AgentTool.Create("publish", "Publishes the notes.", async () =>
            {
                Interlocked.Increment(ref Published);
                await Task.Delay(100);
                return "published";
            }, needsApproval: true),
        ];
        public override async Task MockAsync(MockContext mock)
        {
            await mock.CallAsync("addNote", new { });
            await mock.CallAsync("publish", new { });
            await mock.SayAsync("mock");
        }
    }

    private sealed class SlowAgent : ToolAgent
    {
        public override string Name => "slow-agent";
        public override string Label => "Slow";
        public override string Description => "Has a tool that never finishes in time.";
        public override string SystemPrompt(ToolContext ctx) => "Test agent.";
        public override IEnumerable<AgentTool> Tools(ToolContext ctx) =>
            [AgentTool.Create("slow", "Takes forever.", async () => { await Task.Delay(TimeSpan.FromSeconds(10)); return "late"; })];
        public override Task MockAsync(MockContext mock) => mock.SayAsync("mock");
    }
}
