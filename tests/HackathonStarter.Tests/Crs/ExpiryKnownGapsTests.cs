using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Clients;
using static HackathonStarter.Tests.Crs.R2WorkflowHost;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// Two P0 gaps from the legal review of the "client does not answer" route, pinned as they behave today so the demo build
/// stays green. Each test asserts the CURRENT behaviour; when the workflow is fixed, the test fails and is rewritten to the
/// expected behaviour in its comment. Owner: R2, triage with the domain analyst. No fix before the demo.
/// </summary>
public class ExpiryKnownGapsTests
{
    private static readonly DateOnly NewAccount = new(2020, 6, 15);

    private static NewResponse Explanation(ExplanationCategory category) =>
        new(ResponseKind.ReasonableExplanation, category, "online", "Explanation from the client");

    [Fact]
    public async Task KnownGap_a_case_expires_without_an_initial_request_ever_sent()
    {
        await using var host = await CreateAsync();
        await host.SeedClientAsync("CL-0016", NewAccount);
        var code = (await host.MoveAsync("CL-0016", "ES")).CaseCode!; // Open: no request drafted or approved
        var cases = host.As(Viewer).Cases;

        Assert.Equal(new DeadlineTick(0, 0, 1), await cases.TickAsync(Utc("2027-01-26T07:00:01")));

        var item = (await cases.GetAsync(code))!;
        Assert.DoesNotContain(item.Requests, r => r.Kind == RequestKind.Initial && r.Status == RequestStatus.Sent);
        // Today: Expired and classified, although the client was never asked. Expected: no expiry (or an escalation to the
        // operator) until an initial request has been sent.
        Assert.Equal(CaseStatus.Expired, item.Status);
        Assert.Equal("DL-EXPIRED-NEW", item.OutcomeRuleId);
    }

    [Fact]
    public async Task KnownGap_an_insufficient_answer_stops_the_expiry_for_good()
    {
        await using var host = await CreateAsync();
        await host.SeedClientAsync("CL-0016", NewAccount);
        var code = await host.AwaitingClientAsync("CL-0016", "ES");
        var cases = host.As(Viewer).Cases;

        host.SetNow(Utc("2027-01-10T08:00:00"));
        await host.As(Member).Cases.RegisterResponseAsync(code, Explanation(ExplanationCategory.Other)); // not enough to close
        Assert.Equal(CaseStatus.ResponseReceived, (await cases.GetAsync(code))!.Status);

        // Today: ResponseReceived is outside the tick, so the case never expires, even a year after the deadline. Expected:
        // an answer that does not resolve the case keeps the deadline (expiry, or an escalation to the operator).
        Assert.Equal(new DeadlineTick(0, 0, 0), await cases.TickAsync(Utc("2027-01-26T07:00:01")));
        Assert.Equal(new DeadlineTick(0, 0, 0), await cases.TickAsync(Utc("2028-01-26T07:00:01")));
        Assert.Equal(CaseStatus.ResponseReceived, (await cases.GetAsync(code))!.Status);
    }
}
