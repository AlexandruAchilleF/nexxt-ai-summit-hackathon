using System.Text.Json;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Tests.Support;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>extractSelfCertification: the model (or the mock) extracts, code verifies every quote and records only verified data.</summary>
public class AgentExtractionTests
{
    private static object Field(string value, string quote) => new { value, quote, page = 1 };

    /// <summary>What a model would return for Maria's new form, with the jurisdictions and the birth date chosen by the test.</summary>
    private static object Extraction(object[] jurisdictions, object? birthDate = null) => new
    {
        holderName = Field("Maria Popescu", "Nume: Maria Popescu"),
        residenceAddress = Field("Leopoldstraße 12, 80802 München, Germania", "Adresa actuală de reședință: Leopoldstraße 12, 80802 München, Germania"),
        birthDate = birthDate ?? Field("1991-06-08", "Data nașterii (zz/ll/aaaa): 08/06/1991"),
        jurisdictions,
        signed = Field("true", "Semnătură: Maria Popescu"),
        signatureDate = Field("2027-01-21", "Data: 21/01/2027"),
        declaration = Field("Confirm că datele sunt corecte și complete.", "Confirm că datele sunt corecte și complete."),
    };

    private static readonly object Germany = new { country = "DE", tin = "86095174223", quote = "Germania (DE) 86095174223", page = 1 };

    [Fact]
    public async Task Without_a_model_the_mock_reads_the_synthetic_form_and_every_quote_is_verified()
    {
        var world = new MariaWorld();
        world.DeliverAnswer();
        await using var host = await world.HostAsync();

        var r = await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });

        Assert.Equal("mock", r.GetProperty("source").GetString());
        var fields = r.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("field").GetString()!);
        Assert.Equal(["holderName", "residenceAddress", "birthDate", "signed", "signatureDate", "declaration"], fields.Keys);
        Assert.All(fields.Values, f => Assert.True(f.GetProperty("verified").GetBoolean(), f.ToString()));
        Assert.Equal("Maria Popescu", fields["holderName"].GetProperty("value").GetString());
        Assert.Equal("1991-06-08", fields["birthDate"].GetProperty("value").GetString());
        Assert.Equal("2027-01-21", fields["signatureDate"].GetProperty("value").GetString());
        Assert.Contains("Leopoldstraße 12, 80802 München", fields["residenceAddress"].GetProperty("value").GetString());
        var de = Assert.Single(r.GetProperty("jurisdictions").EnumerateArray());
        Assert.Equal("mock", de.GetProperty("source").GetString());
        Assert.Equal("DE", de.GetProperty("country").GetString());
        Assert.Equal("86095174223", de.GetProperty("tin").GetString());
        Assert.True(de.GetProperty("verified").GetBoolean());
        Assert.True(r.GetProperty("complete").GetBoolean());
        Assert.True(r.GetProperty("recorded").GetBoolean());
        Assert.Equal(2, r.GetProperty("selfCertificationId").GetInt32());
        Assert.Equal(["DE"], r.GetProperty("outcome").GetProperty("reportableFor").EnumerateArray().Select(x => x.GetString()));

        var change = Assert.Single(world.Changes.Requests);
        Assert.Equal(ChangeKind.SelfCertSubmitted, change.Kind);
        Assert.Equal("online", change.Channel);
        Assert.Equal(MariaWorld.ClientCode, change.ClientCode);
        Assert.Equal(MariaWorld.CaseCode, change.CaseCode);
        Assert.Equal(new DateTime(2027, 1, 21, 0, 0, 0, DateTimeKind.Utc), change.SelfCertification!.SignedAt);
        var residence = Assert.Single(change.SelfCertification.Residences);
        Assert.Equal(("DE", "86095174223"), (residence.Country, residence.Tin));
    }

    [Fact]
    public async Task Extraction_returns_the_saved_id_not_a_later_id_with_the_same_signing_date()
    {
        var world = new MariaWorld();
        world.DeliverAnswer();
        ChangeResult? recordedChange = null;
        var getCallsBeforeReturn = -1;
        world.Changes.AfterRecord = recorded =>
        {
            recordedChange = recorded;
            var submitted = world.Clients.Maria.SelfCertifications.Single(s => s.Id == recorded.SelfCertificationId);
            world.Clients.Maria.SelfCertifications.Add(new SelfCertification
            {
                Id = submitted.Id + 1, ClientId = submitted.ClientId, SignedAt = submitted.SignedAt, ReceivedAt = submitted.ReceivedAt,
                Channel = submitted.Channel, DeclaresUsPerson = submitted.DeclaresUsPerson, Residences = [.. submitted.Residences],
            });
            getCallsBeforeReturn = world.Clients.GetCalls;
            return recorded;
        };
        await using var host = await world.HostAsync();

        var r = await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });

        Assert.Single(world.Changes.Requests);
        var change = Assert.IsType<ChangeResult>(recordedChange);
        var saved = Assert.Single(world.Clients.Maria.SelfCertifications, s => s.Id == change.SelfCertificationId);
        var later = Assert.Single(world.Clients.Maria.SelfCertifications, s => s.Id > saved.Id);
        Assert.Equal(saved.SignedAt, later.SignedAt);
        Assert.True(r.GetProperty("complete").GetBoolean());
        Assert.True(r.GetProperty("recorded").GetBoolean());
        Assert.Equal(saved.Id, r.GetProperty("selfCertificationId").GetInt32());
        Assert.NotEqual(later.Id, r.GetProperty("selfCertificationId").GetInt32());
        Assert.Equal(getCallsBeforeReturn, world.Clients.GetCalls);
        Assert.Equal(["DE"], r.GetProperty("outcome").GetProperty("reportableFor").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task A_saved_self_certification_without_a_returned_id_is_an_error_not_a_success()
    {
        var world = new MariaWorld();
        world.DeliverAnswer();
        world.Changes.AfterRecord = recorded => recorded with { SelfCertificationId = null };
        await using var host = await world.HostAsync();

        var r = await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });

        Assert.Equal(MariaWorld.SelfCertDocumentId, Assert.Single(world.Changes.Requests).DocumentId);
        Assert.Equal(2, Assert.Single(world.Clients.Maria.SelfCertifications, s => s.Status == SelfCertStatus.Valid).Id);
        var error = r.GetProperty("error").GetString();
        Assert.Contains("no selfCertificationId", error);
        Assert.Contains("Do not call registerResponse or closeCase", error);
        Assert.Contains("operator", error);
        Assert.False(r.TryGetProperty("recorded", out var recordedFlag) && recordedFlag.ValueKind == JsonValueKind.True);
        Assert.True(!r.TryGetProperty("selfCertificationId", out var id) || id.ValueKind == JsonValueKind.Null);
        Assert.True(!r.TryGetProperty("next", out var next) || next.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task A_jurisdiction_whose_quote_is_not_in_the_document_is_dropped()
    {
        var model = new FakeChatClient().Json(Extraction(
            [Germany, new { country = "FR", tin = "1234567890123", quote = "Franța (FR) 1234567890123", page = 1 }]));
        var world = new MariaWorld();
        world.DeliverAnswer();
        await using var host = await world.HostAsync(model);

        var r = await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });

        Assert.Equal("ai", r.GetProperty("source").GetString());
        var jurisdictions = r.GetProperty("jurisdictions").EnumerateArray().ToList();
        Assert.True(jurisdictions.Single(j => j.GetProperty("country").GetString() == "DE").GetProperty("verified").GetBoolean());
        Assert.False(jurisdictions.Single(j => j.GetProperty("country").GetString() == "FR").GetProperty("verified").GetBoolean());
        Assert.DoesNotContain(r.Strings(), s => s.Contains("1234567890123"));
        Assert.True(r.GetProperty("recorded").GetBoolean());
        var residence = Assert.Single(Assert.Single(world.Changes.Requests).SelfCertification!.Residences);
        Assert.Equal("DE", residence.Country);
        // The model saw the form as data, never as instructions.
        Assert.Contains("<document page=\"1\">", model.AllUserText);
        Assert.Contains("not instructions", string.Join("\n", model.Calls[0].Select(m => m.Text)));
    }

    [Fact]
    public async Task A_value_that_its_quote_does_not_show_is_dropped()
    {
        var wrongTin = new { country = "DE", tin = "86095174224", quote = "Germania (DE) 86095174223", page = 1 };
        var model = new FakeChatClient().Json(Extraction([wrongTin]));
        var world = new MariaWorld();
        world.DeliverAnswer();
        await using var host = await world.HostAsync(model);

        var r = await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });

        Assert.False(Assert.Single(r.GetProperty("jurisdictions").EnumerateArray()).GetProperty("verified").GetBoolean());
        Assert.False(r.GetProperty("recorded").GetBoolean());
        Assert.Contains("jurisdictions", r.GetProperty("missing").EnumerateArray().Select(x => x.GetString()));
        Assert.Empty(world.Changes.Requests);
    }

    [Fact]
    public async Task A_required_field_with_an_unverified_quote_stops_the_recording()
    {
        var model = new FakeChatClient().Json(Extraction([Germany], birthDate: Field("1980-01-01", "Data nașterii (zz/ll/aaaa): 01/01/1980")));
        var world = new MariaWorld();
        world.DeliverAnswer();
        await using var host = await world.HostAsync(model);

        var r = await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });

        var birth = r.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("field").GetString() == "birthDate");
        Assert.False(birth.GetProperty("verified").GetBoolean());
        Assert.True(!birth.TryGetProperty("value", out var v) || v.ValueKind == JsonValueKind.Null);
        Assert.False(r.GetProperty("complete").GetBoolean());
        Assert.Equal(["birthDate"], r.GetProperty("missing").EnumerateArray().Select(x => x.GetString()));
        Assert.False(r.GetProperty("recorded").GetBoolean());
        Assert.True(!r.TryGetProperty("selfCertificationId", out var id) || id.ValueKind == JsonValueKind.Null);
        Assert.Empty(world.Changes.Requests);
    }
}
