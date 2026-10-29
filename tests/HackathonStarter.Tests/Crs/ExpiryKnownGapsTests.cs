using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Clients;
using static HackathonStarter.Tests.Crs.R2WorkflowHost;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// The no-initial-request expiry gap remains pinned to current behaviour pending separate legal review by R2 and the domain analyst.
/// The insufficient-response freeze is fixed: registration rejects the answer without mutation and expiry still runs.
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

        host.SetNow(Utc("2027-01-26T07:00:01"));
        Assert.Equal(new DeadlineTick(0, 0, 1), await cases.TickAsync(Utc("2027-01-26T07:00:01")));

        var item = (await cases.GetAsync(code))!;
        Assert.DoesNotContain(item.Requests, r => r.Kind == RequestKind.Initial && r.Status == RequestStatus.Sent);
        // Today: Expired and classified, although the client was never asked. Expected: no expiry (or an escalation to the
        // operator) until an initial request has been sent.
        Assert.Equal(CaseStatus.Expired, item.Status);
        Assert.Equal("DL-EXPIRED-NEW", item.OutcomeRuleId);
    }

    [Fact]
    public async Task An_insufficient_answer_is_rejected_without_freezing_expiry()
    {
        await using var host = await CreateAsync();
        await host.SeedClientAsync("CL-0016", NewAccount);
        var code = await host.AwaitingClientAsync("CL-0016", "ES");
        var cases = host.As(Viewer).Cases;

        host.SetNow(Utc("2027-01-10T08:00:00"));
        var auditCount = host.Audit.Entries.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.As(Member).Cases.RegisterResponseAsync(code, Explanation(ExplanationCategory.Other)));

        var waiting = (await cases.GetAsync(code))!;
        Assert.Equal(CaseStatus.AwaitingClient, waiting.Status);
        Assert.Empty(waiting.Responses);
        Assert.Equal(auditCount, host.Audit.Entries.Count);

        host.SetNow(Utc("2027-01-26T07:00:01"));
        Assert.Equal(new DeadlineTick(0, 0, 1), await cases.TickAsync(Utc("2027-01-26T07:00:01")));
        var expired = (await cases.GetAsync(code))!;
        Assert.Equal(CaseStatus.Expired, expired.Status);
        Assert.Equal("DL-EXPIRED-NEW", expired.OutcomeRuleId);
        Assert.Equal(host.Clock.UtcNow, expired.ExpiredAt);
        var expiry = Assert.Single(host.Audit.OfKind(AuditKinds.CaseExpired));
        Assert.Equal(expired.Id, expiry.CaseId);
        Assert.Equal(CaseService.SystemActor, expiry.Actor);
        Assert.Equal(host.Clock.UtcNow, expiry.At);

        auditCount = host.Audit.Entries.Count;
        host.SetNow(Utc("2028-01-26T07:00:01"));
        Assert.Equal(new DeadlineTick(0, 0, 0), await cases.TickAsync(Utc("2028-01-26T07:00:01")));
        var unchanged = (await cases.GetAsync(code))!;
        Assert.Equal((expired.Status, expired.ExpiredAt, expired.OutcomeRuleId),
            (unchanged.Status, unchanged.ExpiredAt, unchanged.OutcomeRuleId));
        Assert.Empty(unchanged.Responses);
        Assert.Equal(auditCount, host.Audit.Entries.Count);
    }
}
