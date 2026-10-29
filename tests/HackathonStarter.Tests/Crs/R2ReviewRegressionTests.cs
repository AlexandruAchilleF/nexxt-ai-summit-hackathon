using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using HackathonStarter.Config;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Synthetic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.R2WorkflowHost;

namespace HackathonStarter.Tests.Crs;

/// <summary>R2 review regressions: validate incoming answers before they can stop expiry; human approval still closes them.</summary>
public class R2ReviewRegressionTests
{
    private static readonly DateOnly NewAccount = new(2020, 6, 15);
    private static readonly DateOnly PreexistingAccount = new(2012, 9, 3);
    private const string ClientCode = "CL-0101";

    [Theory]
    [InlineData(ResponseKind.Other, ExplanationCategory.None)]
    [InlineData(ResponseKind.ReasonableExplanation, ExplanationCategory.Other)]
    [InlineData(ResponseKind.ReasonableExplanation, ExplanationCategory.None)]
    [InlineData(ResponseKind.NewSelfCertification, ExplanationCategory.None)]
    public async Task An_inadequate_day_20_answer_cannot_freeze_expiry(ResponseKind kind, ExplanationCategory category)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var before = await StoredAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code,
            new NewResponse(kind, category, "online", "Synthetic insufficient answer")));

        Assert.Equal(before, await StoredAsync(host));
        await ExpiresAsync(host, code);
    }

    [Theory]
    [InlineData(Regime.Crs, true)]
    [InlineData(Regime.Fatca, false)]
    [InlineData(Regime.Fatca, true)]
    [InlineData(Regime.Both, false)]
    [InlineData(Regime.Both, true)]
    public async Task Explanations_do_not_answer_preexisting_crs_or_fatca_cases(Regime regime, bool preexisting)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, preexisting ? PreexistingAccount : NewAccount);
        var code = await AwaitingAsync(host, regime);
        host.AdvanceDays(20);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Explanation()));

        Assert.Contains(regime == Regime.Crs ? "p. 29" : "p. 38", error.Message);
        Assert.Equal(before, await StoredAsync(host));
        if (regime != Regime.Fatca) await ExpiresAsync(host, code);
        else Assert.Null((await CaseAsync(host, code)).DueAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_valid_flag_does_not_make_a_bad_tin_or_an_uncovered_indicium_a_sufficient_answer(bool badTin)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        if (!badTin)
            await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.PhoneChanged, "online",
                Phone: new PhoneSnapshot("+33144550123", "FR")));
        var selfCertId = await SubmitAsync(host, code, badTin ? "BADFORMAT" : "86095174223");
        var snapshot = (await host.As(Viewer).Clients.SnapshotAsync(client.Id))!;
        var engine = host.Root.GetRequiredService<ICrsRuleEngine>();
        // The two checks are independent: a bad CRS TIN is not an indicium; an uncovered phone is not a form check.
        Assert.Equal(badTin ? Decision.Compliant : Decision.Clarification, engine.EvaluateCurrent(snapshot).Decision);
        Assert.Equal(badTin ? Decision.Clarification : Decision.NoCase, engine.EvaluateSelfCertification(snapshot, snapshot.SelfCertification!).Decision);
        await using (var db = await host.DbAsync())
            Assert.Equal(SelfCertStatus.Valid, (await db.SelfCertifications.SingleAsync(s => s.Id == selfCertId)).Status);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId)));

        Assert.Contains(badTin ? "SC-TIN-FORMAT:DE" : "CRS-IND-PHONE:FR", error.Message);
        Assert.Equal(before, await StoredAsync(host));
        await ExpiresAsync(host, code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_sufficient_answer_waits_for_human_approval_and_candidate_explanations_are_not_saved(bool explanation)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var documentId = await host.AddDocumentAsync(client.Id, "Synthetic answer", explanation ? DocumentKind.Letter : DocumentKind.SelfCertForm);
        var input = explanation ? Explanation(documentId) : Form(await SubmitAsync(host, code), documentId, documentary: true);

        var response = await host.As(Member).Cases.RegisterResponseAsync(code, input);

        var waiting = await CaseAsync(host, code);
        Assert.Equal(CaseStatus.ResponseReceived, waiting.Status);
        Assert.Null(waiting.Proof);
        Assert.Null(waiting.ClosedAt);
        Assert.Empty(host.Audit.OfKind(AuditKinds.CaseClosed));
        await using (var db = await host.DbAsync()) Assert.Empty(await db.AcceptedExplanations.ToListAsync());
        var snapshot = (await host.As(Viewer).Clients.SnapshotAsync(client.Id))!;
        Assert.Equal(explanation ? Decision.Clarification : Decision.Compliant, host.Root.GetRequiredService<ICrsRuleEngine>().EvaluateCurrent(snapshot).Decision);
        var before = await StoredAsync(host);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.As(Member).Cases.CloseAsync(code, ConfirmationChannel.Online));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.As(Viewer).Cases.CloseAsync(code, ConfirmationChannel.Online));
        Assert.Equal(before, await StoredAsync(host));

        host.SetNow(waiting.DueAt!.Value.AddDays(31));
        Assert.Equal(new DeadlineTick(0, 0, 0), await host.As(Viewer).Cases.TickAsync(host.Clock.UtcNow));
        Assert.Equal(before, await StoredAsync(host));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.NotNull(closed.Proof);
        Assert.Equal(response.ReceivedAt, closed.Proof.ConfirmedAt);
        Assert.Equal($"DOC-{documentId}", closed.Proof.TransactionOrDocumentId);
        await using var saved = await host.DbAsync();
        var document = await saved.ClientDocuments.SingleAsync(d => d.Id == documentId);
        Assert.Equal(CaseService.Sha256(document.Content), closed.Proof.EvidenceSha256);
        Assert.Equal(explanation ? 1 : 0, await saved.AcceptedExplanations.CountAsync());
        Assert.Equal(explanation ? Array.Empty<string>() : ["DE"], closed.ReportableFor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Response_summaries_are_masked_without_changing_references_or_source_documents(bool includesPii)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        var selfCertId = await SubmitAsync(host, code);
        const string phone = "+49305550151";
        var iban = SyntheticData.IbanWithCheckDigits("DE", "100000000000000001");
        var sourceText = $"SPECIMEN synthetic answer: {phone}; {iban}";
        var sourceBytes = Encoding.UTF8.GetBytes(sourceText);
        var documentId = await host.AddDocumentAsync(client.Id, sourceText, DocumentKind.SelfCertForm);
        await using (var db = await host.DbAsync())
        {
            (await db.ClientDocuments.SingleAsync(d => d.Id == documentId)).Content = sourceBytes;
            await db.SaveChangesAsync();
        }
        var note = includesPii ? $"Synthetic reply: call {phone}; account {iban}." : "Synthetic supporting documents received.";
        var expected = includesPii ? "Synthetic reply: call [PHONE-1]; account [IBAN-1]." : note;

        var response = await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId, documentId) with { Summary = $"  {note}  " });

        Assert.Equal(expected, response.Summary);
        var saved = Assert.Single((await CaseAsync(host, code)).Responses);
        Assert.Equal(expected, saved.Summary);
        Assert.Equal((response.Id, selfCertId, documentId), (saved.Id, saved.SelfCertificationId, saved.DocumentId));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
        Assert.NotNull(closed.Proof);
        Assert.Equal($"DOC-{documentId}", closed.Proof.TransactionOrDocumentId);
        Assert.Equal(CaseService.Sha256(sourceBytes), closed.Proof.EvidenceSha256);
        await using var check = await host.DbAsync();
        Assert.Equal(sourceBytes, (await check.ClientDocuments.SingleAsync(d => d.Id == documentId)).Content);
        Assert.Equal(client.Accounts.Single().Iban, (await check.Accounts.SingleAsync(a => a.ClientId == client.Id)).Iban);
        Assert.Equal("86095174223", (await check.SelfCertifications.SingleAsync(s => s.Id == selfCertId)).Residences.Single().Tin);
        var payload = Assert.Single(host.Audit.OfKind(AuditKinds.ResponseRegistered)).PayloadJson;
        Assert.DoesNotContain(phone, payload);
        Assert.DoesNotContain(iban, payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Request_rejection_reasons_are_masked_without_changing_plain_notes(bool includesPii)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = Assert.IsType<string>((await host.MoveAsync(ClientCode, "DE")).CaseCode);
        var draft = await host.As(Member).Cases.DraftRequestAsync(code, "en", "Synthetic request");
        const string phone = "+49305550151";
        var iban = SyntheticData.IbanWithCheckDigits("DE", "100000000000000001");
        var note = includesPii ? $"Verify {iban}; contact {phone}." : "Use the agreed request template.";
        var expected = includesPii ? "Verify [IBAN-1]; contact [PHONE-1]." : note;

        var rejected = await host.As(Admin).Cases.RejectRequestAsync(code, draft.Id, $"  {note}  ");

        Assert.Equal(expected, rejected.RejectionReason);
        var item = await CaseAsync(host, code);
        var saved = Assert.Single(item.Requests);
        Assert.Equal(expected, saved.RejectionReason);
        Assert.Equal((draft.Id, draft.CaseId, RequestStatus.Rejected, Admin.Name), (saved.Id, saved.CaseId, saved.Status, saved.RejectedBy));
        Assert.Equal(CaseStatus.Open, item.Status);
        var payload = Assert.Single(host.Audit.OfKind(AuditKinds.RequestRejected)).PayloadJson;
        Assert.DoesNotContain(phone, payload);
        Assert.DoesNotContain(iban, payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_empty_placeholder_outcome_cannot_accept_an_answer_or_close_a_case(bool atClose)
    {
        await using var host = await CreateAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var response = Form(await SubmitAsync(host, code));
        if (atClose) await host.As(Member).Cases.RegisterResponseAsync(code, response);
        host.Engine.OnEvaluateCurrent = _ => new RuleOutcome(Decision.NoCase, [], [], [],
            "Rule engine not implemented yet (phase 0 placeholder).", "Motorul nu e implementat.", "0.0-phase0");
        var before = await StoredAsync(host);

        if (atClose)
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, response));

        Assert.Equal(before, await StoredAsync(host));
    }

    [Fact]
    public async Task A_real_no_case_outcome_with_checked_rules_can_close_a_corrected_form_case()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        await SubmitAsync(host, null, "BADFORMAT");
        var code = Assert.Single(await host.As(Viewer).Cases.QueueAsync()).Code;
        await SendInitialAsync(host, code);
        host.AdvanceDays(1);
        var selfCertId = await SubmitAsync(host, code);
        var client = (await host.As(Viewer).Clients.GetAsync(ClientCode))!;
        var outcome = host.Root.GetRequiredService<ICrsRuleEngine>().EvaluateCurrent(ClientSnapshot.From(client));
        Assert.Equal(Decision.NoCase, outcome.Decision);
        Assert.Empty(outcome.Indicia);
        Assert.NotEmpty(outcome.Cleared);

        await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);

        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.Equal("SC-VALID", closed.OutcomeRuleId);
        Assert.Equal(["DE"], closed.ReportableFor);
        Assert.NotNull(closed.Proof);
        Assert.Equal($"SC-{selfCertId}", closed.Proof.TransactionOrDocumentId);
        var selfCert = client.SelfCertifications.Single(s => s.Id == selfCertId);
        Assert.Equal(CaseService.Sha256(Encoding.UTF8.GetBytes(CaseService.CanonicalJson(selfCert))), closed.Proof.EvidenceSha256);
    }

    [Fact]
    public async Task A_noncurrent_form_cannot_answer_the_case_even_if_its_valid_flag_was_restored()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(1);
        var older = await SubmitAsync(host, code);
        host.AdvanceDays(1);
        var current = await SubmitAsync(host, code);
        await using (var db = await host.DbAsync())
        {
            (await db.SelfCertifications.SingleAsync(s => s.Id == older)).Status = SelfCertStatus.Valid;
            await db.SaveChangesAsync();
        }
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Form(older)));

        Assert.Contains("current", error.Message);
        Assert.Equal(before, await StoredAsync(host));
        await host.As(Member).Cases.RegisterResponseAsync(code, Form(current));
        Assert.Equal(CaseStatus.ResponseReceived, (await CaseAsync(host, code)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_not_us_person_answer_is_evaluated_against_the_latest_indicium_date(bool signedAfterChange)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(5);
        await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.PhoneAdded, "online",
            Phone: new PhoneSnapshot("+13125550147", "US")));
        host.AdvanceDays(1);
        var selfCertId = await SubmitAsync(host, code, signedAt: signedAfterChange ? host.Clock.UtcNow.Date : Base.AddDays(2));
        var before = await StoredAsync(host);

        if (!signedAfterChange)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId)));
            Assert.Contains("FATCA-IND-PHONE:US", error.Message);
            Assert.Equal(before, await StoredAsync(host));
            return;
        }
        await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.Equal(["DE"], closed.ReportableFor);
        Assert.NotNull(closed.Proof);
    }

    [Fact]
    public async Task Accepting_an_answer_withdraws_each_outstanding_draft_reminder_once_in_id_order()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(85);
        await host.As(Viewer).Cases.TickAsync(host.Clock.UtcNow);
        var member = host.As(Member).Cases;
        await member.DraftRequestAsync(code, "en", "Second reminder draft", RequestKind.Reminder);
        var rejected = await member.DraftRequestAsync(code, "en", "Rejected draft", RequestKind.Reminder);
        await host.As(Admin).Cases.RejectRequestAsync(code, rejected.Id, "Synthetic operator reason");
        var sent = await member.DraftRequestAsync(code, "en", "Sent reminder", RequestKind.Reminder);
        await host.As(Admin).Cases.ApproveRequestAsync(code, sent.Id, "email");
        var draftIds = (await CaseAsync(host, code)).Requests.Where(r => r.Kind == RequestKind.Reminder && r.Status == RequestStatus.Draft).Select(r => r.Id).ToArray();
        var selfCertId = await SubmitAsync(host, code);
        var auditCount = host.Audit.Entries.Count;

        var response = await member.RegisterResponseAsync(code, Form(selfCertId));

        var item = await CaseAsync(host, code);
        Assert.Equal(CaseStatus.ResponseReceived, item.Status);
        Assert.Equal(2, draftIds.Length);
        foreach (var id in draftIds)
        {
            var reminder = item.Requests.Single(r => r.Id == id);
            Assert.Equal(RequestStatus.Rejected, reminder.Status);
            Assert.Equal(CaseService.SystemActor, reminder.RejectedBy);
            Assert.Equal(response.ReceivedAt, reminder.RejectedAt);
            Assert.Equal("response.received", reminder.RejectionReason);
        }
        Assert.Equal("Synthetic operator reason", item.Requests.Single(r => r.Id == rejected.Id).RejectionReason);
        Assert.Equal(Admin.Name, item.Requests.Single(r => r.Id == rejected.Id).RejectedBy);
        Assert.Equal(RequestStatus.Sent, item.Requests.Single(r => r.Id == sent.Id).Status);
        var entries = host.Audit.Entries.Skip(auditCount).ToArray();
        Assert.Equal([AuditKinds.RequestRejected, AuditKinds.RequestRejected, AuditKinds.ResponseRegistered], entries.Select(e => e.Kind));
        Assert.Equal(draftIds, entries.Take(2).Select(e => JsonSerializer.Deserialize<JsonElement>(e.PayloadJson).GetProperty("requestId").GetInt32()));
        Assert.All(entries.Take(2), e => Assert.Equal(CaseService.SystemActor, e.Actor));
        Assert.Equal("user:" + Member.Id, entries[^1].Actor);
        var beforeRefusals = await StoredAsync(host);
        foreach (var id in draftIds)
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.ApproveRequestAsync(code, id, "email"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => member.RegisterResponseAsync(code, Form(selfCertId)));
        Assert.Equal(beforeRefusals, await StoredAsync(host));
    }

    [Fact]
    public async Task Even_a_new_draft_reminder_cannot_send_a_response_received_case_back_to_awaiting_client()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        await host.As(Member).Cases.RegisterResponseAsync(code, Explanation());
        var draft = await host.As(Member).Cases.DraftRequestAsync(code, "en", "Stale reminder", RequestKind.Reminder);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.ApproveRequestAsync(code, draft.Id, "email"));

        Assert.Contains("ResponseReceived", error.Message);
        Assert.Equal(before, await StoredAsync(host));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejecting_an_inadequate_answer_leaves_draft_reminders_and_audits_untouched(bool badTin)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(85);
        await host.As(Viewer).Cases.TickAsync(host.Clock.UtcNow);
        var response = badTin ? Form(await SubmitAsync(host, code, "BADFORMAT"))
            : new NewResponse(ResponseKind.Other, ExplanationCategory.None, "online", "Insufficient");
        var before = await StoredAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, response));

        Assert.Equal(before, await StoredAsync(host));
        Assert.Equal(RequestStatus.Draft, Assert.Single((await CaseAsync(host, code)).Requests, r => r.Kind == RequestKind.Reminder).Status);
        await ExpiresAsync(host, code);
    }

    [Theory]
    [InlineData(false, "null")]
    [InlineData(false, "unknown")]
    [InlineData(false, "foreign")]
    [InlineData(true, "null")]
    [InlineData(true, "unknown")]
    [InlineData(true, "foreign")]
    public async Task A_documentary_claim_requires_an_existing_own_document_and_cannot_stop_expiry(bool preexisting, string invalid)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, preexisting ? PreexistingAccount : NewAccount);
        var other = await host.SeedClientAsync("CL-0199", NewAccount);
        var foreign = await host.AddDocumentAsync(other.Id, "Other synthetic client's document");
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var selfCertId = await SubmitAsync(host, code);
        int? documentId = invalid == "null" ? null : invalid == "unknown" ? foreign + 1000 : foreign;
        var before = await StoredAsync(host);

        var error = await Record.ExceptionAsync(() => host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId, documentId, documentary: true)));

        if (invalid == "unknown") Assert.IsType<KeyNotFoundException>(error);
        else Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(before, await StoredAsync(host));
        await ExpiresAsync(host, code);
    }

    [Fact]
    public async Task Preexisting_crs_needs_both_the_documentary_flag_and_a_real_own_document()
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, PreexistingAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var selfCertId = await SubmitAsync(host, code);
        var documentId = await host.AddDocumentAsync(client.Id, "Synthetic documentary evidence", DocumentKind.Letter);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId, documentId)));

        Assert.Contains("p. 29", error.Message);
        Assert.Equal(before, await StoredAsync(host));
        await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId, documentId, documentary: true));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.NotNull(closed.Proof);
        Assert.Equal($"DOC-{documentId}", closed.Proof.TransactionOrDocumentId);
        await using var db = await host.DbAsync();
        Assert.Equal(CaseService.Sha256((await db.ClientDocuments.SingleAsync(d => d.Id == documentId)).Content), closed.Proof.EvidenceSha256);
    }

    [Theory]
    [InlineData(false, "null")]
    [InlineData(false, "unknown")]
    [InlineData(false, "foreign")]
    [InlineData(false, "deleted")]
    [InlineData(true, "null")]
    [InlineData(true, "foreign")]
    [InlineData(true, "flag")]
    public async Task Closing_rechecks_documentary_claims_on_tampered_or_stale_rows(bool preexisting, string invalid)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, preexisting ? PreexistingAccount : NewAccount);
        var other = await host.SeedClientAsync("CL-0199", NewAccount);
        var foreign = await host.AddDocumentAsync(other.Id, "Other synthetic client's document");
        var own = await host.AddDocumentAsync(client.Id, "Synthetic documentary evidence");
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var selfCertId = await SubmitAsync(host, code);
        await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId, own, documentary: true));
        await using (var db = await host.DbAsync())
        {
            var item = await db.Cases.Include(c => c.Responses).SingleAsync(c => c.Code == code);
            var response = Assert.Single(item.Responses);
            if (invalid == "deleted") await db.ClientDocuments.Where(d => d.Id == own).ExecuteDeleteAsync();
            else if (invalid == "flag") response.HasDocumentaryEvidence = false;
            else response.DocumentId = invalid == "null" ? null : invalid == "unknown" ? own + 1000 : foreign;
            await db.SaveChangesAsync();
        }
        var before = await StoredAsync(host);

        var error = await Record.ExceptionAsync(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));

        if (invalid is "unknown" or "deleted") Assert.IsType<KeyNotFoundException>(error);
        else Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(before, await StoredAsync(host));
        Assert.Null((await CaseAsync(host, code)).Proof);
    }

    [Fact]
    public async Task Closing_rechecks_the_form_with_the_real_engine_after_a_valid_row_is_tampered()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        var selfCertId = await SubmitAsync(host, code);
        await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId));
        await using (var db = await host.DbAsync())
        {
            var selfCert = await db.SelfCertifications.SingleAsync(s => s.Id == selfCertId);
            selfCert.Residences.Single().Tin = "BADFORMAT";
            await db.SaveChangesAsync();
        }
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));

        Assert.Contains("SC-TIN-FORMAT:DE", error.Message);
        Assert.Equal(before, await StoredAsync(host));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_submission_cannot_supersede_an_accepted_answer_with_or_without_a_case_code(bool explicitCase, bool explanation)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var response = explanation ? Explanation() : Form(await SubmitAsync(host, code));
        await host.As(Member).Cases.RegisterResponseAsync(code, response);
        host.AdvanceDays(1);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host, explicitCase ? code : null));

        Assert.Contains("ResponseReceived", error.Message);
        Assert.Equal(before, await StoredAsync(host));
        Assert.Equal(CaseStatus.Closed, (await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online)).Status);
    }

    [Fact]
    public async Task A_submission_still_cannot_answer_a_closed_case()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        await host.As(Member).Cases.RegisterResponseAsync(code, Explanation());
        await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(host, code));

        Assert.Contains("Closed", error.Message);
        Assert.Equal(before, await StoredAsync(host));
    }

    [Fact]
    public async Task Change_result_exposes_the_exact_saved_self_certification_id_without_changing_deconstruction()
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var result = await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.SelfCertSubmitted, "online",
            SelfCertification: new SelfCertificationSnapshot(host.Clock.UtcNow, [new("DE", "86095174223")], false)));
        var (change, outcome, caseCode) = result; // the existing three-value contract must keep compiling
        Assert.Same(result.Change, change);
        Assert.Same(result.Outcome, outcome);
        Assert.Equal(result.CaseCode, caseCode);
        // Reflection pins the non-positional, nullable, init-only addition and lets this regression run before it exists.
        var property = typeof(ChangeResult).GetProperty("SelfCertificationId");
        Assert.NotNull(property);
        Assert.Equal(typeof(int?), property.PropertyType);
        Assert.Contains(typeof(IsExternalInit), property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers());
        await using var db = await host.DbAsync();
        var saved = await db.SelfCertifications.SingleAsync(s => s.ClientId == client.Id && s.Status == SelfCertStatus.Valid);
        Assert.Equal(saved.Id, Assert.IsType<int>(property.GetValue(result)));
        Assert.Contains($"\"SelfCertificationId\":{saved.Id}", change.NewValueJson);

        var address = await host.MoveAsync(ClientCode, "ES");
        Assert.Null(property.GetValue(address));
        var document = await host.AddDocumentAsync(client.Id, "Synthetic letter");
        var uploaded = await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.DocumentUploaded, "online", DocumentId: document));
        Assert.Null(property.GetValue(uploaded));
        Assert.Null(property.GetValue(new ChangeResult(change, outcome, caseCode)));
    }

    [Theory]
    [InlineData("2027-01-21T23:30:00", "2027-01-22T00:00:00")]
    [InlineData("2027-07-21T21:30:00", "2027-07-22T00:00:00")]
    [InlineData("2027-01-22T07:00:00", "2027-01-22T20:00:00")]
    public async Task Receipt_dates_use_the_Bucharest_calendar_not_exact_instants_and_storage_stays_unchanged(string receipt, string dated)
    {
        var receivedAt = Utc(receipt);
        var datedAt = Utc(dated);
        await using var host = await RealAsync();
        host.SetNow(receivedAt.AddDays(-20));
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.SetNow(receivedAt);
        var selfCertId = await SubmitAsync(host, code, signedAt: datedAt);

        var response = await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId));
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);

        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.NotNull(closed.Proof);
        Assert.Equal(receivedAt, response.ReceivedAt);
        Assert.Equal((datedAt, receivedAt, receivedAt), (closed.Proof.DeclarationDatedAt, closed.Proof.DeclarationReceivedAt, closed.Proof.ConfirmedAt));
        await using var db = await host.DbAsync();
        var stored = await db.SelfCertifications.SingleAsync(s => s.Id == selfCertId);
        Assert.Equal((datedAt, receivedAt), (stored.SignedAt, stored.ReceivedAt));
    }

    [Theory]
    [InlineData("2027-01-21T23:30:00", "2027-01-23T00:00:00")]
    [InlineData("2027-07-21T21:30:00", "2027-07-23T00:00:00")]
    [InlineData("2027-01-22T19:00:00", "2027-01-22T22:30:00")]
    [InlineData("2027-07-22T18:00:00", "2027-07-22T21:30:00")]
    public async Task A_later_Bucharest_calendar_date_is_rejected_at_registration_without_freezing_expiry(string receipt, string dated)
    {
        await using var host = await RealAsync();
        host.SetNow(Utc(receipt).AddDays(-20));
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.SetNow(Utc(receipt));
        var selfCertId = await SubmitAsync(host, code, signedAt: Utc(dated));
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId)));

        Assert.Contains("p. 34", error.Message);
        Assert.Contains("application convention", error.Message);
        Assert.Contains("Europe/Bucharest", error.Message);
        Assert.Equal(before, await StoredAsync(host));
        await ExpiresAsync(host, code);
    }

    [Theory]
    [InlineData("2027-01-21T23:30:00", "2027-01-21T00:00:00", "2027-01-23T00:00:00")]
    [InlineData("2027-07-21T21:30:00", "2027-07-21T00:00:00", "2027-07-23T00:00:00")]
    public async Task Closing_rechecks_the_calendar_date_of_a_tampered_declaration(string receipt, string dated, string tampered)
    {
        await using var host = await RealAsync();
        host.SetNow(Utc(receipt).AddDays(-20));
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.SetNow(Utc(receipt));
        var selfCertId = await SubmitAsync(host, code, signedAt: Utc(dated));
        await host.As(Member).Cases.RegisterResponseAsync(code, Form(selfCertId));
        await using (var db = await host.DbAsync())
        {
            (await db.SelfCertifications.SingleAsync(s => s.Id == selfCertId)).SignedAt = Utc(tampered);
            await db.SaveChangesAsync();
        }
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));

        Assert.Contains("p. 34", error.Message);
        Assert.Equal(before, await StoredAsync(host));
    }

    [Theory]
    [InlineData(Roles.Admin, false)]
    [InlineData(Roles.Member, false)]
    [InlineData(Roles.Viewer, false)]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.Member, true)]
    [InlineData(Roles.Viewer, true)]
    public async Task Future_ticks_refuse_every_role_even_without_cases_and_never_write_audit_reminders_or_follow_ups(string role, bool hasCases)
    {
        await using var host = await RealAsync();
        DateTime[] ticks = [Base.AddTicks(1)];
        if (hasCases)
        {
            await host.SeedClientAsync(ClientCode, NewAccount);
            var code = await AwaitingAsync(host, Regime.Both);
            var item = await CaseAsync(host, code);
            ticks = [item.RemindAt!.Value, item.DueAt!.Value];
        }
        var cases = host.As(SiteConfig.Users.Single(u => u.Role == role)).Cases;
        foreach (var tick in ticks)
        {
            host.SetNow(tick.AddTicks(-1)); // no tolerance: even one future tick is refused
            var before = await StoredAsync(host);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cases.TickAsync(tick));

            Assert.Equal(before, await StoredAsync(host));
        }
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public async Task Ticks_normalize_utc_and_allow_equality_and_historic_instants(DateTimeKind kind)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(85);
        var at = host.Clock.UtcNow;
        var tick = kind == DateTimeKind.Local ? at.ToLocalTime() : DateTime.SpecifyKind(at, kind);

        Assert.Equal(new DeadlineTick(1, 1, 0), await host.As(Viewer).Cases.TickAsync(tick));

        Assert.Equal(at, (await CaseAsync(host, code)).RemindedAt);
        host.AdvanceDays(6);
        Assert.Equal(new DeadlineTick(0, 0, 0), await host.As(Viewer).Cases.TickAsync(at));
        var due = Base.AddDays(90);
        Assert.Equal(new DeadlineTick(0, 0, 1), await host.As(Viewer).Cases.TickAsync(due));
        Assert.Equal(due, (await CaseAsync(host, code)).ExpiredAt);
    }

    [Fact]
    public async Task Tick_checks_again_inside_the_writer_after_the_clock_moves_back_while_it_waits()
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await AwaitingAsync(host, Regime.Both);
        var due = (await CaseAsync(host, code)).DueAt!.Value;
        host.SetNow(due);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Audit.BeforeAppendAsync = async kind =>
        {
            if (kind != AuditKinds.RequestDrafted) return;
            entered.TrySetResult();
            await release.Task;
        };
        var holdingWriter = host.As(Member).Cases.DraftRequestAsync(code, "en", "Synthetic reminder", RequestKind.Reminder);
        Task<DeadlineTick>? ticking = null;
        (string Rows, string Audit) before;
        var auditCount = host.Audit.Entries.Count;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            before = await StoredAsync(host); // the holder saved its request but has not yet appended its audit
            var future = host.As(Member).Cases.TickAsync(due.AddTicks(1));
            Assert.True(future.IsCompleted); // fail fast even while another operation holds Writer
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => future);
            ticking = host.As(Viewer).Cases.TickAsync(due);
            Assert.False(ticking.IsCompleted);
            host.SetNow(due.AddTicks(-1));
        }
        finally { release.TrySetResult(); }
        await holdingWriter;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ticking!);

        Assert.Equal(before.Rows, (await StoredAsync(host)).Rows);
        Assert.Equal(auditCount + 1, host.Audit.Entries.Count); // only the request that held the writer
        Assert.Equal(AuditKinds.RequestDrafted, host.Audit.Entries[^1].Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_clock_audit_keeps_the_clock_and_changed_event_untouched(bool reset)
    {
        await using var host = await CreateAsync();
        host.AdvanceDays(10);
        var before = (host.Clock.UtcNow, host.Clock.Offset);
        var changed = 0;
        host.Clock.Changed += () => changed++;
        host.Audit.FailOn = AuditKinds.ClockChanged;
        var control = host.As(Admin).ClockControl;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => reset ? control.ResetAsync() : control.AdvanceDaysAsync(85));

        Assert.Equal($"Synthetic audit failure on {AuditKinds.ClockChanged}.", error.Message);
        Assert.Equal(before, (host.Clock.UtcNow, host.Clock.Offset));
        Assert.Equal(0, changed);
        Assert.Empty(host.Audit.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_clock_audit_observes_the_old_time_and_is_saved_before_changed_fires(bool reset)
    {
        await using var host = await CreateAsync();
        host.AdvanceDays(10);
        var before = host.Clock.UtcNow;
        var changed = 0;
        host.Audit.BeforeAppendAsync = kind =>
        {
            Assert.Equal(AuditKinds.ClockChanged, kind);
            Assert.Equal(before, host.Clock.UtcNow);
            Assert.Equal(0, changed);
            return Task.CompletedTask;
        };
        host.Clock.Changed += () =>
        {
            changed++;
            Assert.Equal(before, Assert.Single(host.Audit.OfKind(AuditKinds.ClockChanged)).At);
        };
        var control = host.As(Admin).ClockControl;

        var offset = reset ? await control.ResetAsync() : await control.AdvanceDaysAsync(85);

        Assert.Equal(reset ? TimeSpan.Zero : TimeSpan.FromDays(95), offset);
        Assert.Equal(1, changed);
        Assert.Equal(before, Assert.Single(host.Audit.Entries).At);
    }

    // ---- Final review: form validity is independent of the explanation route; receipts cannot be in the future. ----

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task B1_Student_explanation_cannot_register_with_unresolved_form_findings(bool mixedIndicia, bool missingDate)
    {
        await using var host = await RealAsync();
        var (item, _, ruleId) = await MalformedFormCaseAsync(host, mixedIndicia, missingDate);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(item.Code, Explanation()));

        Assert.Contains(ruleId, error.Message);
        await AssertResponseRefusalAsync(host, item.Code, before, atClose: false);
        var waiting = await CaseAsync(host, item.Code);
        Assert.Equal((item.DueAt, item.RemindAt), (waiting.DueAt, waiting.RemindAt));
        if (mixedIndicia) await ExpiresAsync(host, item.Code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task B1_Student_explanation_cannot_close_seeded_responses_with_unresolved_form_findings(bool mixedIndicia, bool missingDate)
    {
        await using var host = await RealAsync();
        var (item, _, ruleId) = await MalformedFormCaseAsync(host, mixedIndicia, missingDate);
        // A response accepted by an older build must not bypass the form check at closing either.
        await SeedPendingResponseAsync(host, item.Code, Explanation(), host.Clock.UtcNow);
        var before = await StoredAsync(host);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(item.Code, ConfirmationChannel.Online));

        Assert.Contains(ruleId, error.Message);
        await AssertResponseRefusalAsync(host, item.Code, before, atClose: true);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task B1_Student_explanation_rechecks_tampered_forms_without_saved_form_findings(bool atClose, bool missingDate)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var originalId = Assert.Single(client.SelfCertifications).Id;
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        if (atClose) await host.As(Member).Cases.RegisterResponseAsync(code, Explanation());
        await using (var db = await host.DbAsync())
        {
            var original = await db.SelfCertifications.SingleAsync(s => s.Id == originalId);
            if (missingDate) original.SignedAt = default;
            else original.Residences = [new TaxResidence { Country = "DE", Tin = "BADFORMAT" }];
            await db.SaveChangesAsync();
        }
        var ruleId = missingDate ? "SC-VALID" : "SC-TIN-FORMAT";
        await AssertCurrentFormFindingAsync(host, originalId, ruleId);
        Assert.Equal(["CRS-IND-ADDR"], (await CaseAsync(host, code)).Indicia.Select(i => i.RuleId));
        var before = await StoredAsync(host);

        var error = atClose
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online))
            : await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Explanation()));

        Assert.Contains(ruleId, error.Message);
        await AssertResponseRefusalAsync(host, code, before, atClose);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B1_Student_explanation_requires_an_original_declaration(bool atClose)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        // Guide p. 35(b) confirms an original declaration. This fixture has none at all; it does not decide whether
        // a retained Doubtful/Superseded declaration can be rehabilitated (those application flags are not in the guide).
        await using (var db = await host.DbAsync())
            await db.SelfCertifications.Where(s => s.ClientId == client.Id).ExecuteDeleteAsync();
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        Assert.Null((await host.As(Viewer).Clients.SnapshotAsync(client.Id))!.SelfCertification);
        if (atClose) await SeedPendingResponseAsync(host, code, Explanation(), host.Clock.UtcNow);
        var before = await StoredAsync(host);

        if (atClose)
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, Explanation()));

        await AssertResponseRefusalAsync(host, code, before, atClose);
    }

    [Fact]
    public async Task B1_Student_explanation_can_cure_only_the_original_residence_discrepancy()
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var originalId = Assert.Single(client.SelfCertifications).Id;
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        host.AdvanceDays(20);
        var snapshot = (await host.As(Viewer).Clients.SnapshotAsync(client.Id))!;
        Assert.Equal(["RO"], snapshot.SelfCertification!.Residences.Select(r => r.Country));
        var engine = Assert.IsType<CrsRuleEngine>(host.Root.GetRequiredService<ICrsRuleEngine>());
        var formCheck = engine.EvaluateSelfCertification(snapshot, snapshot.SelfCertification!);
        var discrepancy = Assert.Single(formCheck.Uncovered);
        Assert.Equal(("SC-CONSISTENT", "DE", "Addresses"), (discrepancy.RuleId, discrepancy.Jurisdiction, discrepancy.SourceField));
        var documentId = await host.AddDocumentAsync(client.Id, "Synthetic student supporting letter", DocumentKind.Letter);

        var response = await host.As(Member).Cases.RegisterResponseAsync(code, Explanation(documentId));

        await using (var db = await host.DbAsync()) Assert.Empty(await db.AcceptedExplanations.ToListAsync());
        Assert.Equal(CaseStatus.ResponseReceived, (await CaseAsync(host, code)).Status);
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.Equal("RESP-EXPLANATION", closed.OutcomeRuleId);
        Assert.Empty(closed.ReportableFor);
        Assert.NotNull(closed.Proof);
        Assert.Equal(response.ReceivedAt, closed.Proof.ConfirmedAt);
        Assert.Equal($"DOC-{documentId}", closed.Proof.TransactionOrDocumentId);
        await using var check = await host.DbAsync();
        Assert.Equal(CaseService.Sha256((await check.ClientDocuments.SingleAsync(d => d.Id == documentId)).Content), closed.Proof.EvidenceSha256);
        var original = await check.SelfCertifications.SingleAsync(s => s.Id == originalId);
        Assert.Equal(SelfCertStatus.Valid, original.Status);
        Assert.Equal(["RO"], original.Residences.Select(r => r.Country));
        Assert.Equal("DE", (await check.AcceptedExplanations.SingleAsync()).Jurisdiction);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task B1_A_corrected_form_can_close_a_case_with_historical_form_findings(bool mixedIndicia, bool missingDate)
    {
        await using var host = await RealAsync();
        var (item, malformedId, ruleId) = await MalformedFormCaseAsync(host, mixedIndicia, missingDate);
        host.AdvanceDays(1);
        var correctedId = await SubmitAsync(host, item.Code);
        Assert.NotEqual(malformedId, correctedId);
        Assert.Contains((await CaseAsync(host, item.Code)).Indicia, i => i.RuleId == ruleId); // History is not a current form verdict.

        var response = await host.As(Member).Cases.RegisterResponseAsync(item.Code, Form(correctedId));
        var closed = await host.As(Admin).Cases.CloseAsync(item.Code, ConfirmationChannel.Online);

        Assert.Equal(correctedId, response.SelfCertificationId);
        Assert.Equal((CaseStatus.Closed, "SC-VALID"), (closed.Status, closed.OutcomeRuleId));
        Assert.Equal(["DE"], closed.ReportableFor);
        Assert.NotNull(closed.Proof);
        Assert.Equal(response.ReceivedAt, closed.Proof.ConfirmedAt);
        Assert.Equal($"SC-{correctedId}", closed.Proof.TransactionOrDocumentId);
        await using var db = await host.DbAsync();
        var corrected = await db.SelfCertifications.SingleAsync(s => s.Id == correctedId);
        Assert.Equal(CaseService.Sha256(Encoding.UTF8.GetBytes(CaseService.CanonicalJson(corrected))), closed.Proof.EvidenceSha256);
        Assert.Equal(SelfCertStatus.Superseded, (await db.SelfCertifications.SingleAsync(s => s.Id == malformedId)).Status);
        Assert.Empty(await db.AcceptedExplanations.ToListAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task B2_Reset_refuses_future_declaration_receipts_at_registration_and_close(bool atClose, bool explanation)
    {
        await using var host = await RealAsync();
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        var clock = host.Root.GetRequiredService<ISimClock>();
        clock.Advance(TimeSpan.FromDays(20));
        var receivedAt = clock.UtcNow;
        // The signature date itself is nonfuture after reset; only the actual declaration receipt is in the future.
        var selfCertId = await SubmitAsync(host, code, signedAt: Base);
        clock.Reset();
        Assert.Equal(Base, clock.UtcNow);
        Assert.True(receivedAt > clock.UtcNow);
        var input = explanation ? Explanation() : Form(selfCertId);
        if (atClose)
            await SeedPendingResponseAsync(host, code, input, clock.UtcNow); // Nonfuture response: isolate the declaration receipt check.
        var before = await StoredAsync(host);

        if (atClose)
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.RegisterResponseAsync(code, input));

        await AssertResponseRefusalAsync(host, code, before, atClose);
        await using var db = await host.DbAsync();
        var declaration = await db.SelfCertifications.SingleAsync(s => s.Id == selfCertId);
        Assert.Equal((Base, receivedAt), (declaration.SignedAt, declaration.ReceivedAt)); // Never clamp stored timestamps.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B2_Reset_refuses_closing_responses_received_in_the_future(bool explanation)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        // The form's receipt stays nonfuture after reset, so checking only selfCert.ReceivedAt would not fix this test.
        var input = explanation ? Explanation() : Form(await SubmitAsync(host, code));
        var clock = host.Root.GetRequiredService<ISimClock>();
        clock.Advance(TimeSpan.FromDays(20));
        var response = await host.As(Member).Cases.RegisterResponseAsync(code, input);
        clock.Reset();
        Assert.Equal(Base, clock.UtcNow);
        Assert.True(response.ReceivedAt > clock.UtcNow);
        await using (var db = await host.DbAsync())
        {
            var declaration = await db.SelfCertifications.SingleAsync(s => s.ClientId == client.Id && s.Status == SelfCertStatus.Valid);
            Assert.True(declaration.ReceivedAt <= clock.UtcNow);
        }
        var before = await StoredAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online));

        await AssertResponseRefusalAsync(host, code, before, atClose: true);
        Assert.Equal(response.ReceivedAt, Assert.Single((await CaseAsync(host, code)).Responses).ReceivedAt);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task B2_Nonfuture_receipts_remain_acceptable_after_reset(bool explanation, bool historical)
    {
        await using var host = await RealAsync();
        var client = await host.SeedClientAsync(ClientCode, NewAccount);
        var code = await host.AwaitingClientAsync(ClientCode, "DE");
        var documentId = await host.AddDocumentAsync(client.Id, "Synthetic response evidence", DocumentKind.Letter);
        var input = explanation ? Explanation(documentId) : Form(await SubmitAsync(host, code), documentId);
        var clock = host.Root.GetRequiredService<ISimClock>();
        clock.Advance(TimeSpan.FromDays(20));
        clock.Reset();
        Assert.Equal(Base, clock.UtcNow);
        if (historical) clock.Advance(TimeSpan.FromDays(1));
        var receivedAt = clock.UtcNow;

        var response = await host.As(Member).Cases.RegisterResponseAsync(code, input);
        if (historical) clock.Advance(TimeSpan.FromDays(1));
        var closeAt = clock.UtcNow;
        var closed = await host.As(Admin).Cases.CloseAsync(code, ConfirmationChannel.Online);

        Assert.Equal(input.SelfCertificationId, response.SelfCertificationId);
        Assert.Equal(receivedAt, response.ReceivedAt);
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.Equal(closeAt, closed.ClosedAt);
        Assert.NotNull(closed.Proof);
        Assert.Equal(receivedAt, closed.Proof.ConfirmedAt);
        Assert.True(closed.Proof.ConfirmedAt <= closeAt);
        Assert.Equal(historical, closed.Proof.ConfirmedAt < closeAt);
        Assert.Equal(explanation ? receivedAt : Base, closed.Proof.DeclarationReceivedAt);
        Assert.Equal($"DOC-{documentId}", closed.Proof.TransactionOrDocumentId);
        await using var db = await host.DbAsync();
        Assert.Equal(CaseService.Sha256((await db.ClientDocuments.SingleAsync(d => d.Id == documentId)).Content), closed.Proof.EvidenceSha256);
    }

    private static async Task<(Case Item, int SelfCertificationId, string RuleId)> MalformedFormCaseAsync(
        R2WorkflowHost host, bool mixedIndicia, bool missingDate)
    {
        await host.SeedClientAsync(ClientCode, NewAccount);
        var code = mixedIndicia ? await host.AwaitingClientAsync(ClientCode, "DE") : null;
        host.AdvanceDays(20);
        var result = await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.SelfCertSubmitted, "online",
            SelfCertification: new SelfCertificationSnapshot(missingDate ? default : host.Clock.UtcNow,
                [new("DE", missingDate ? "86095174223" : "BADFORMAT")], false), CaseCode: code));
        var ruleId = missingDate ? "SC-VALID" : "SC-TIN-FORMAT";
        Assert.Equal(Decision.Clarification, result.Outcome.Decision);
        Assert.Contains(result.Outcome.Uncovered, h => h.RuleId == ruleId);
        code = Assert.IsType<string>(result.CaseCode);
        if (!mixedIndicia) await SendInitialAsync(host, code);
        var item = await CaseAsync(host, code);
        Assert.Equal((Regime.Crs, CaseStatus.AwaitingClient, false), (item.Regime, item.Status, item.IsPreexisting));
        Assert.Contains(item.Indicia, i => i.RuleId == ruleId);
        if (mixedIndicia)
        {
            Assert.Contains(item.Indicia, i => i.RuleId == "CRS-IND-ADDR" && i.Jurisdiction == "DE");
            Assert.Equal((Base.AddDays(90), Base.AddDays(85)), (item.DueAt, item.RemindAt));
        }
        else
        {
            Assert.All(item.Indicia, i => Assert.Equal(ruleId, i.RuleId));
            Assert.Null(item.DueAt);
            Assert.Null(item.RemindAt);
        }
        var selfCertId = Assert.IsType<int>(result.SelfCertificationId);
        await AssertCurrentFormFindingAsync(host, selfCertId, ruleId);
        return (item, selfCertId, ruleId);
    }

    private static async Task AssertCurrentFormFindingAsync(R2WorkflowHost host, int selfCertId, string ruleId)
    {
        var client = (await host.As(Viewer).Clients.GetAsync(ClientCode))!;
        var current = Assert.Single(client.SelfCertifications, s => s.Status == SelfCertStatus.Valid);
        Assert.Equal(selfCertId, current.Id);
        var snapshot = ClientSnapshot.From(client);
        var engine = Assert.IsType<CrsRuleEngine>(host.Root.GetRequiredService<ICrsRuleEngine>());
        Assert.Contains(engine.EvaluateSelfCertification(snapshot, snapshot.SelfCertification!).Uncovered, h => h.RuleId == ruleId);
    }

    private static async Task SeedPendingResponseAsync(R2WorkflowHost host, string code, NewResponse response, DateTime receivedAt)
    {
        await using var db = await host.DbAsync();
        var item = await db.Cases.SingleAsync(c => c.Code == code);
        item.Responses.Add(new ClientResponse
        {
            Kind = response.Kind, Category = response.Category, Channel = response.Channel, Summary = response.Summary,
            ReceivedAt = receivedAt, DocumentId = response.DocumentId, SelfCertificationId = response.SelfCertificationId,
            HasDocumentaryEvidence = response.HasDocumentaryEvidence,
        });
        item.Status = CaseStatus.ResponseReceived;
        await db.SaveChangesAsync();
    }

    private static async Task AssertResponseRefusalAsync(R2WorkflowHost host, string code, (string Rows, string Audit) before, bool atClose)
    {
        Assert.Equal(before, await StoredAsync(host));
        var item = await CaseAsync(host, code);
        Assert.Equal(atClose ? CaseStatus.ResponseReceived : CaseStatus.AwaitingClient, item.Status);
        Assert.Null(item.Proof);
        Assert.Null(item.ClosedAt);
        if (atClose) Assert.Single(item.Responses);
        else
        {
            Assert.Empty(item.Responses);
            Assert.Empty(host.Audit.OfKind(AuditKinds.ResponseRegistered));
        }
        Assert.Empty(host.Audit.OfKind(AuditKinds.CaseClosed));
        await using var db = await host.DbAsync();
        Assert.Empty(await db.AcceptedExplanations.ToListAsync());
    }

    private static Task<R2WorkflowHost> RealAsync() => CreateAsync(s => s.AddRules());

    private static NewResponse Form(int id, int? documentId = null, bool documentary = false) =>
        new(ResponseKind.NewSelfCertification, ExplanationCategory.None, "online", "Synthetic self-certification",
            documentId, documentary, id);

    private static NewResponse Explanation(int? documentId = null) =>
        new(ResponseKind.ReasonableExplanation, ExplanationCategory.Student, "online", "Synthetic student explanation", documentId);

    private static async Task<int> SubmitAsync(R2WorkflowHost host, string? caseCode, string tin = "86095174223", DateTime? signedAt = null)
    {
        await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.SelfCertSubmitted, "online",
            SelfCertification: new SelfCertificationSnapshot(signedAt ?? host.Clock.UtcNow, [new("DE", tin)], false), CaseCode: caseCode));
        await using var db = await host.DbAsync();
        return await db.SelfCertifications.Where(s => s.Status == SelfCertStatus.Valid && db.Clients.Any(c => c.Id == s.ClientId && c.Code == ClientCode))
            .Select(s => s.Id).SingleAsync();
    }

    private static async Task<string> AwaitingAsync(R2WorkflowHost host, Regime regime)
    {
        string? code = null;
        if (regime != Regime.Fatca) code = (await host.MoveAsync(ClientCode, "DE")).CaseCode;
        if (regime != Regime.Crs)
            code = (await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.PersonalDataChanged, "online", Citizenships: ["RO", "US"]))).CaseCode;
        Assert.NotNull(code);
        await SendInitialAsync(host, code);
        return code;
    }

    private static async Task SendInitialAsync(R2WorkflowHost host, string code)
    {
        var draft = await host.As(Member).Cases.DraftRequestAsync(code, "en", "Synthetic request");
        await host.As(Admin).Cases.ApproveRequestAsync(code, draft.Id, "email");
    }

    private static async Task<Case> CaseAsync(R2WorkflowHost host, string code) => Assert.IsType<Case>(await host.As(Viewer).Cases.GetAsync(code));

    private static async Task ExpiresAsync(R2WorkflowHost host, string code)
    {
        var item = await CaseAsync(host, code);
        Assert.NotNull(item.DueAt);
        host.SetNow(item.DueAt.Value);
        Assert.Equal(new DeadlineTick(0, 0, 1), await host.As(Viewer).Cases.TickAsync(host.Clock.UtcNow));
        var expired = await CaseAsync(host, code);
        Assert.Equal(CaseStatus.Expired, expired.Status);
        Assert.Equal(item.IsPreexisting ? "DL-EXPIRED-PRE" : "DL-EXPIRED-NEW", expired.OutcomeRuleId);
        Assert.Empty(expired.Responses);
        Assert.Null(expired.Proof);
        Assert.Single(host.Audit.OfKind(AuditKinds.CaseExpired));
    }

    private static async Task<(string Rows, string Audit)> StoredAsync(R2WorkflowHost host)
    {
        await using var db = await host.DbAsync();
        var rows = new
        {
            Cases = await db.Cases.AsNoTracking().AsSplitQuery().OrderBy(c => c.Id)
                .Include(c => c.Indicia.OrderBy(i => i.Id)).Include(c => c.Requests.OrderBy(r => r.Id))
                .Include(c => c.Responses.OrderBy(r => r.Id)).Include(c => c.Proof).ToListAsync(),
            Changes = await db.ChangeEvents.AsNoTracking().OrderBy(c => c.Id).ToListAsync(),
            Evaluations = await db.RuleEvaluations.AsNoTracking().OrderBy(e => e.Id).ToListAsync(),
            SelfCertifications = await db.SelfCertifications.AsNoTracking().OrderBy(s => s.Id).ToListAsync(),
            AcceptedExplanations = await db.AcceptedExplanations.AsNoTracking().OrderBy(e => e.Id).ToListAsync(),
        };
        return (JsonSerializer.Serialize(rows), JsonSerializer.Serialize(host.Audit.Entries));
    }
}
