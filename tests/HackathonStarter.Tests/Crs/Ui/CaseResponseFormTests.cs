using System.Collections;
using System.Globalization;
using System.Reflection;
using HackathonStarter.Components.Pages.Cases;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using Microsoft.AspNetCore.Components;

namespace HackathonStarter.Tests.Crs.Ui;

/// <summary>
/// Caller preflight and command construction, without rendering. Fakes only capture commands, never validate them.
/// Service tests own sufficiency/permissions; DOM tests own rendering and CaseDetailPage's Documents/@key wiring.
/// </summary>
public class CaseResponseFormTests
{
    private const int ClientId = 41, OwnDocumentId = 73, ForeignDocumentId = 74;
    private const int SavedId = 317, ChangeId = 901, PreviousId = 999;
    private const string ClientCode = "CL-0041", CaseCode = "CRS-2026-0041";
    private const string Channel = "branch", Summary = "Synthetic manual answer";

    [Theory]
    [InlineData("en-GB", null)]
    [InlineData("en-GB", ForeignDocumentId)]
    [InlineData("en-GB", 404)]
    [InlineData("ro-RO", null)]
    [InlineData("ro-RO", ForeignDocumentId)]
    [InlineData("ro-RO", 404)]
    public async Task Documentary_assertion_requires_a_selected_same_client_document_before_writes(string culture, int? documentId)
    {
        using var form = new Form(culture);
        form.Set("_documentary", true);
        form.Set("_documentId", documentId);

        // Documents includes an eligible own document AND a foreign one: neither is an implicit substitute.
        await RefusesBeforeWritesAsync(form, culture == "ro-RO"
            ? "Selectează un document care aparține acestui client înainte de înregistrarea dovezii documentare."
            : "Select a document belonging to this client before registering documentary evidence.");
        Assert.Equal(documentId, form.Get<int?>("_documentId"));
    }

    [Theory]
    [InlineData(Regime.Crs)]
    [InlineData(Regime.Both)]
    public async Task Preexisting_crs_requires_explicit_documentary_assertion_before_writes(Regime regime)
    {
        using var form = new Form(regime: regime, preexisting: true);
        form.Set("_documentId", OwnDocumentId);
        Assert.False(form.Get<bool>("_documentary")); // Selecting a document alone is not an assertion.

        await RefusesBeforeWritesAsync(form,
            "A preexisting CRS account needs a new self-certification and documentary evidence (guide p. 29).");
    }

    [Theory]
    [InlineData(Regime.Crs)]
    [InlineData(Regime.Both)]
    public async Task Preexisting_crs_attaches_explicit_own_evidence_to_response_not_self_certification_source(Regime regime)
    {
        using var form = new Form(regime: regime, preexisting: true);
        form.Set("_documentary", true);
        form.Set("_documentId", OwnDocumentId);

        await form.RegisterAsync();

        AssertChange(form, form.Now);
        AssertResponse(form, documentId: OwnDocumentId);
    }

    [Fact]
    public async Task Preexisting_fatca_does_not_require_documentary_evidence_and_uses_saved_id()
    {
        using var form = new Form(regime: Regime.Fatca, preexisting: true);
        Assert.False(form.Get<bool>("_documentary"));
        Assert.Null(form.Get<int?>("_documentId"));

        await form.RegisterAsync();

        AssertChange(form, form.Now);
        AssertResponse(form);
    }

    [Theory]
    [InlineData(317)]
    [InlineData(523)]
    public async Task Response_uses_exact_returned_id_not_change_or_previous_response_id(int savedId)
    {
        using var form = new Form();
        form.Services.ReturnedId = savedId;

        await form.RegisterAsync();

        AssertChange(form, form.Now);
        var response = AssertResponse(form, savedId);
        Assert.NotEqual<int?>(ChangeId, response.SelfCertificationId);
        Assert.NotEqual<int?>(PreviousId, response.SelfCertificationId); // A higher existing id is not this submission.
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("ro-RO")]
    public async Task Missing_saved_id_stops_after_change_without_latest_lookup_or_response(string culture)
    {
        using var form = new Form(culture);
        form.Services.ReturnedId = null;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(form.RegisterAsync);

        Assert.Equal(culture == "ro-RO"
            ? "Nu a fost returnat identificatorul autocertificării. Răspunsul nu a fost înregistrat."
            : "The self-certification ID was not returned. The answer was not registered.", error.Message);
        AssertChange(form, form.Now);
        Assert.Empty(form.Services.Responses);
        Assert.Equal(new[] { "change" }, form.Services.Order);
    }

    [Theory]
    [InlineData(1, 21, 15)]
    [InlineData(1, 22, 16)]
    [InlineData(7, 20, 15)]
    [InlineData(7, 21, 16)]
    public async Task Signing_after_bucharest_receipt_day_refuses_without_writes_or_field_clamping(int month, int utcHour, int signingDay)
    {
        // Receipts straddle Bucharest midnight (23:30 / 00:30), with the following local signing day in each row.
        var receipt = new DateTime(2026, month, 14, utcHour, 30, 0, DateTimeKind.Utc);
        var supplied = new DateTime(2026, month, signingDay, 0, 0, 0, DateTimeKind.Unspecified);
        using var form = new Form(now: receipt);
        form.Set("_signedOn", supplied);

        await RefusesBeforeWritesAsync(form, "The signing date cannot be after the receipt date (guide p. 34).");

        Assert.Equal(supplied, form.Get<DateTime>("_signedOn"));
        Assert.Equal(DateTimeKind.Unspecified, form.Get<DateTime>("_signedOn").Kind);
    }

    [Theory]
    [InlineData(1, 22)]
    [InlineData(7, 21)]
    public async Task Same_bucharest_day_preserves_calendar_midnight_utc_even_after_receipt_instant(int month, int utcHour)
    {
        var receipt = new DateTime(2026, month, 14, utcHour, 30, 0, DateTimeKind.Utc);
        var supplied = new DateTime(2026, month, 15, 0, 0, 0, DateTimeKind.Unspecified);
        using var form = new Form(now: receipt);
        form.Set("_signedOn", supplied);

        await form.RegisterAsync();

        var snapshot = AssertChange(form, new DateTime(2026, month, 15, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(snapshot.SignedAt > receipt, "A same-local-day calendar date must not be clamped to the earlier UTC receipt instant.");
        Assert.Equal(supplied, form.Get<DateTime>("_signedOn"));
        Assert.Equal(DateTimeKind.Unspecified, form.Get<DateTime>("_signedOn").Kind);
        AssertResponse(form);
    }

    [Theory]
    [InlineData(Regime.Crs, false)]
    [InlineData(Regime.Crs, true)]
    [InlineData(Regime.Both, true)]
    [InlineData(Regime.Fatca, true)]
    public async Task Explanation_without_documentary_assertion_is_forwarded_for_service_validation_only(Regime regime, bool preexisting)
    {
        using var form = new Form(regime: regime, preexisting: preexisting);
        form.Set("_kind", ResponseKind.ReasonableExplanation);
        form.Set("_category", ExplanationCategory.Student);
        Assert.False(form.Get<bool>("_documentary"));

        await form.RegisterAsync();

        // Forwarding is not legal sufficiency: the real service can refuse this answer for its regime/evidence.
        Assert.Empty(form.Services.Changes);
        var (code, response) = Assert.Single(form.Services.Responses);
        Assert.Equal(CaseCode, code);
        Assert.Equal(new NewResponse(ResponseKind.ReasonableExplanation, ExplanationCategory.Student, Channel, Summary), response);
        Assert.Equal(new[] { "response" }, form.Services.Order);
    }

    private static async Task RefusesBeforeWritesAsync(Form form, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(form.RegisterAsync);
        Assert.Equal(expectedMessage, error.Message);
        Assert.Empty(form.Services.Changes);
        Assert.Empty(form.Services.Responses);
        Assert.Empty(form.Services.Order);
    }

    private static SelfCertificationSnapshot AssertChange(Form form, DateTime signedAt)
    {
        var change = Assert.Single(form.Services.Changes);
        var snapshot = Assert.IsType<SelfCertificationSnapshot>(change.SelfCertification);
        Assert.Equal(new ChangeRequest(ClientCode, ChangeKind.SelfCertSubmitted, Channel,
            SelfCertification: snapshot, CaseCode: CaseCode), change);
        Assert.Null(change.DocumentId); // Supporting evidence must not become the self-certification's source document.
        Assert.Equal(signedAt, snapshot.SignedAt);
        Assert.Equal(DateTimeKind.Utc, snapshot.SignedAt.Kind);
        Assert.Equal(new TaxResidenceSnapshot("DE", "86095174223"), Assert.Single(snapshot.Residences));
        Assert.True(snapshot.DeclaresUsPerson);
        return snapshot;
    }

    private static NewResponse AssertResponse(Form form, int id = SavedId, int? documentId = null)
    {
        var (code, response) = Assert.Single(form.Services.Responses);
        Assert.Equal(CaseCode, code);
        Assert.Equal(new NewResponse(ResponseKind.NewSelfCertification, ExplanationCategory.None, Channel, Summary,
            DocumentId: documentId, HasDocumentaryEvidence: documentId.HasValue, SelfCertificationId: id), response);
        Assert.Equal(new[] { "change", "response" }, form.Services.Order);
        return response;
    }

    private sealed class Form : IDisposable
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentUICulture;
        private readonly CaseNextStep _component;
        public Recorder Services { get; } = new();
        public DateTime Now { get; }

        public Form(string culture = "en-GB", Regime regime = Regime.Crs, bool preexisting = false, DateTime? now = null)
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Now = now ?? new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
            _component = new CaseNextStep();
            var item = new Case
            {
                Id = 17, Code = CaseCode, ClientId = ClientId, ClientCode = ClientCode,
                Regime = regime, IsPreexisting = preexisting, Status = CaseStatus.AwaitingClient,
                Responses = [new() { Id = 1, SelfCertificationId = PreviousId, Kind = ResponseKind.NewSelfCertification }],
            };
            IReadOnlyCollection<DocumentInfo> documents = [Document(OwnDocumentId, ClientId), Document(ForeignDocumentId, ClientId + 1)];
            ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CaseNextStep.Case)] = item, [nameof(CaseNextStep.Now)] = Now,
                [nameof(CaseNextStep.Documents)] = documents, [nameof(CaseNextStep.IsEditor)] = true,
            }).SetParameterProperties(_component);
            Property(_component, "Changes", Services);
            Property(_component, "Cases", Services);
            Set("_answerChannel", Channel);
            Set("_summary", Summary);
            Set("_declaresUs", true);
            var row = Assert.Single(Get<IEnumerable>("_residences").Cast<object>());
            Property(row, "Country", " de ");
            Property(row, "Tin", " 86095174223 ");
        }

        // Only private injected services/form state/RegisterAsync need reflection. No renderer or FillSample invocation.
        public void Set(string name, object? value) => Field(name).SetValue(_component, value);
        public T Get<T>(string name) => (T)Field(name).GetValue(_component)!;
        public Task RegisterAsync() => (Task)(typeof(CaseNextStep).GetMethod("RegisterAsync", Private)
            ?? throw new MissingMethodException(nameof(CaseNextStep), "RegisterAsync")).Invoke(_component, null)!;
        private static FieldInfo Field(string name) => typeof(CaseNextStep).GetField(name, Private)
            ?? throw new MissingFieldException(nameof(CaseNextStep), name);
        private static void Property(object target, string name, object value) =>
            (target.GetType().GetProperty(name, Private | BindingFlags.Public)
                ?? throw new MissingMemberException(target.GetType().Name, name)).SetValue(target, value);
        private DocumentInfo Document(int id, int clientId) => new(id, clientId, DocumentKind.UtilityBill,
            $"SPECIMEN-{id}.txt", 1, ShieldVerdict.Clean, Now.AddDays(-1));
        public void Dispose() => CultureInfo.CurrentUICulture = _previousCulture;
    }

    private sealed class Recorder : IChangeService, ICaseService
    {
        public int? ReturnedId { get; set; } = SavedId;
        public List<ChangeRequest> Changes { get; } = [];
        public List<(string Code, NewResponse Response)> Responses { get; } = [];
        public List<string> Order { get; } = [];

        public Task<ChangeResult> RecordAsync(ChangeRequest request)
        {
            Changes.Add(request);
            Order.Add("change");
            return Task.FromResult(new ChangeResult(new ChangeEvent { Id = ChangeId },
                new RuleOutcome(Decision.NoCase, [], [], [], "", "", "test-double"), null) { SelfCertificationId = ReturnedId });
        }

        public Task<ClientResponse> RegisterResponseAsync(string caseCode, NewResponse response)
        {
            Responses.Add((caseCode, response));
            Order.Add("response");
            return Task.FromResult(new ClientResponse());
        }

        // Any lookup (including a "latest" fallback) or unrelated workflow operation must fail, not validate/repair inputs.
        public Task<List<ChangeEvent>> ListAsync(string? clientCode = null, int take = 50) => throw Unexpected();
        public Task<List<Case>> QueueAsync(CaseQueueFilter? filter = null) => throw Unexpected();
        public Task<Case?> GetAsync(string code) => throw Unexpected();
        public Task<Case> OpenAsync(ChangeEvent change, RuleOutcome outcome, Func<int, Task>? logChange = null) => throw Unexpected();
        public Task<Evidence> AddEvidenceAsync(NewEvidence evidence) => throw Unexpected();
        public Task<ContextSignal> AddSignalAsync(NewSignal signal) => throw Unexpected();
        public Task<List<Evidence>> ListEvidenceAsync(string clientCode) => throw Unexpected();
        public Task<List<ContextSignal>> ListSignalsAsync(string clientCode) => throw Unexpected();
        public Task<IReadOnlyList<string>> RequiredDocumentsAsync(string caseCode) => throw Unexpected();
        public Task<ClientRequest> DraftRequestAsync(string caseCode, string language, string body, RequestKind kind = RequestKind.Initial) => throw Unexpected();
        public Task<ClientRequest> ApproveRequestAsync(string caseCode, int requestId, string channel) => throw Unexpected();
        public Task<ClientRequest> RejectRequestAsync(string caseCode, int requestId, string reason) => throw Unexpected();
        public Task<Case> CloseAsync(string caseCode, ConfirmationChannel channel,
            ConfirmationEvidenceKind evidenceKind = ConfirmationEvidenceKind.SignedDocument, string? collectionSteps = null) => throw Unexpected();
        public Task<Case> EscalateAsync(string caseCode, string reason) => throw Unexpected();
        public Task<DeadlineTick> TickAsync(DateTime now) => throw Unexpected();
        private static Exception Unexpected() => new NotSupportedException("This fake only records submissions; unexpected service call.");
    }
}
