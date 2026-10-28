using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Targets;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using HackathonStarter.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HackathonStarter.Tests;

public class ConnectionHealthTests
{
    // Invalid check digit on purpose: the probe tests connectivity, not a customer's TIN.
    private const string ProbeTin = "86095174224";
    private const string Soap11Namespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string SoapNamespace = "urn:ec.europa.eu:taxud:tin:services:checkTin:types";
    private const string PrivateMarker = "SYNTHETIC-WIRE-DIAGNOSTIC";
    private const string TinLabel = "Synthetic TIN service";
    private const string BothFalse = "<t:validStructure>false</t:validStructure><t:validSyntax>false</t:validSyntax>";
    private const string Greeting = "Hello, are you there?";

    [Theory]
    [InlineData("en-GB")]
    [InlineData("ro-RO")]
    public async Task Tin_probe_sends_the_exact_SOAP_conversation_and_false_flags_still_prove_connectivity(string culture)
    {
        using var language = new CultureScope(culture);
        using var http = Respond(SoapAnswer());
        await using var host = await CreateAsync(http, user: SiteConfig.Users.Single(u => u.Role == Roles.Member));

        var result = Assert.Single(await host.Get<ConnectionCheck>().RunAsync());

        var sent = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(new Uri("https://tin.example.invalid/check"), sent.Uri);
        Assert.Equal("text/xml; charset=utf-8", sent.ContentType);
        Assert.Equal("\"\"", sent.SoapAction);
        Assert.Equal(TinOnEuropaClient.BuildRequest("DE", ProbeTin), sent.Body);
        Assert.False(new TinFormat().Check("DE", ProbeTin).FormatValid);
        Assert.True(result.Ok);
        Assert.Equal("target", result.Kind);
        Assert.Equal(Text(culture, $"target “{TinLabel}”", $"țintă „{TinLabel}”"), result.Name);
        Assert.Contains(Text(culture, "SOAP checkTin response received (connectivity only)",
            "Răspuns SOAP checkTin primit (doar conectivitate)"), result.Detail);
        AssertPrivate(result);
    }

    public static IEnumerable<object[]> UnexpectedSoapResponses()
    {
        yield return ["empty", "en-GB", ""];
        yield return ["malformed XML", "ro-RO", $"<broken>{ProbeTin}"];
        yield return ["HTML", "en-GB", $"<html><body>{PrivateMarker} {ProbeTin}</body></html>"];
        yield return ["SOAP fault", "ro-RO", $"""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><s:Fault><faultcode>s:Server</faultcode><faultstring>{PrivateMarker} {ProbeTin}</faultstring></s:Fault></s:Body></s:Envelope>
            """];
        yield return ["missing validStructure", "en-GB", SoapAnswer("<t:validSyntax>false</t:validSyntax>")];
        yield return ["missing validSyntax", "ro-RO", SoapAnswer("<t:validStructure>false</t:validStructure>")];
        yield return ["non-Boolean flag", "en-GB", SoapAnswer("<t:validStructure>true</t:validStructure><t:validSyntax>unknown</t:validSyntax>")];
        yield return ["wrong namespace", "ro-RO", SoapAnswer().Replace(SoapNamespace, "urn:synthetic:wrong")];
    }

    [Theory]
    [MemberData(nameof(UnexpectedSoapResponses))]
    public async Task Live_HTTP_without_a_namespaced_response_and_both_Boolean_flags_is_not_healthy(
        string scenario, string culture, string body)
    {
        using var language = new CultureScope(culture);
        using var http = Respond(body);
        await using var host = await CreateAsync(http);

        var result = Assert.Single(await host.Get<ConnectionCheck>().RunAsync());

        Assert.Single(http.Requests);
        Assert.False(result.Ok, $"{scenario}: HTTP 200 alone is not SOAP connectivity evidence.");
        Assert.Contains(Text(culture, "unexpected SOAP response", "răspuns SOAP neașteptat"), result.Detail);
        AssertPrivate(result);
    }

    public static IEnumerable<object[]> InvalidSoapEnvelopes()
    {
        var response = BareTinResponse();
        yield return ["bare response fragment", response];
        yield return ["response inside SOAP Fault.detail", SoapFault(response)];
        yield return ["Header-only response", $"""<s:Envelope xmlns:s="{Soap11Namespace}"><s:Header>{response}</s:Header><s:Body/></s:Envelope>"""];
        yield return ["duplicate Body", $"""<s:Envelope xmlns:s="{Soap11Namespace}"><s:Body>{response}</s:Body><s:Body/></s:Envelope>"""];
        yield return ["stray operation payload", SoapEnvelope($"""{response}<t:unexpected xmlns:t="{SoapNamespace}"/>""")];
        yield return ["multiple response payloads", SoapEnvelope(response + response)];
    }

    [Theory]
    [MemberData(nameof(InvalidSoapEnvelopes))]
    public async Task SOAP_connectivity_rejects_missing_misplaced_or_ambiguous_body_responses(string scenario, string body)
    {
        using var language = new CultureScope("en-GB");
        using var http = Respond(body);
        await using var host = await CreateAsync(http);

        var result = Assert.Single(await host.Get<ConnectionCheck>().RunAsync());

        Assert.Equal(TinOnEuropaClient.BuildRequest("DE", ProbeTin), Assert.Single(http.Requests).Body);
        Assert.False(result.Ok, $"{scenario}: connectivity requires one direct checkTinResponse in one SOAP 1.1 Envelope/Body.");
        Assert.Contains("unexpected SOAP response", result.Detail);
        AssertPrivate(result);
    }

    [Fact]
    public async Task HTTP_500_with_valid_looking_XML_is_not_healthy_and_TargetClient_exposes_only_the_status()
    {
        using var language = new CultureScope("ro-RO");
        using var http = Respond(SoapAnswer(), HttpStatusCode.InternalServerError);
        await using var host = await CreateAsync(http);

        var reply = await host.Get<TargetClient>().SendAsync(TinOnEuropaClient.Target,
            TinOnEuropaClient.Conversation("DE", ProbeTin));
        var result = Assert.Single(await host.Get<ConnectionCheck>().RunAsync());

        Assert.Equal(TargetSource.Unavailable, reply.Source);
        Assert.False(result.Ok);
        Assert.Equal("HTTP 500", reply.Error);
        Assert.Contains("indisponibil: verificarea SOAP live a eșuat", result.Detail);
        AssertPrivate(result);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("ro-RO")]
    public async Task An_exact_probe_recording_is_reported_as_replay_not_live_success(string culture)
    {
        using var language = new CultureScope(culture);
        using var http = FailingHttp();
        await using var host = await CreateAsync(http, ai: o => o.Record = true);
        var conversation = TinOnEuropaClient.Conversation("DE", ProbeTin);
        var recordings = host.Get<TargetRecordings>();
        recordings.Save(TinOnEuropaClient.Target, conversation, SoapAnswer());
        Assert.NotNull(recordings.Find(TinOnEuropaClient.Target, conversation));
        Assert.Null(recordings.Find(TinOnEuropaClient.Target, [new("user", Greeting)]));

        var result = Assert.Single(await host.Get<ConnectionCheck>().RunAsync());

        Assert.False(result.Ok);
        Assert.Contains(Text(culture, "down (live SOAP check failed), but a recorded answer exists",
            "indisponibil (verificarea SOAP live a eșuat), dar există un răspuns înregistrat"), result.Detail);
        Assert.Equal(TinOnEuropaClient.BuildRequest("DE", ProbeTin), Assert.Single(http.Requests).Body);
        AssertPrivate(result);
    }

    [Fact]
    public async Task SOAP_transport_errors_never_put_the_probe_or_wire_diagnostic_in_Settings_results()
    {
        using var language = new CultureScope("en-GB");
        using var http = FailingHttp();
        await using var host = await CreateAsync(http);

        var result = Assert.Single(await host.Get<ConnectionCheck>().RunAsync());

        Assert.False(result.Ok);
        AssertPrivate(result);
        Assert.Contains("down: live SOAP check failed", result.Detail);
    }

    [Theory]
    [InlineData("transport without status", null)]
    [InlineData("transport with HTTP 502", "HTTP 502")]
    [InlineData("timeout", "no answer within 60s")]
    [InlineData("HTTP 500 SOAP fault", "HTTP 500")]
    public async Task SOAP_failure_diagnostics_are_safe_in_reply_rendered_logs_and_structured_state(string failure, string? expectedError)
    {
        var logger = new CapturingTargetLogger();
        using var http = failure switch
        {
            "transport without status" => FailingHttp(),
            "transport with HTTP 502" => new StubHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(
                new HttpRequestException(WireDiagnostic, null, HttpStatusCode.BadGateway))),
            "timeout" => new StubHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(new OperationCanceledException(WireDiagnostic))),
            "HTTP 500 SOAP fault" => Respond(SoapFault(WireDiagnostic), HttpStatusCode.InternalServerError),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
        await using var host = await CreateAsync(http, targetLogger: logger);

        var reply = await host.Get<TargetClient>().SendAsync(TinOnEuropaClient.Target, TinOnEuropaClient.Conversation("DE", ProbeTin));

        Assert.Equal(TargetSource.Unavailable, reply.Source);
        Assert.Empty(reply.Text);
        Assert.Equal(TinOnEuropaClient.BuildRequest("DE", ProbeTin), Assert.Single(http.Requests).Body);
        Assert.False(string.IsNullOrWhiteSpace(reply.Error));
        Assert.NotEmpty(logger.Messages);
        Assert.NotEmpty(logger.State);
        Assert.All(new (string Channel, string Text)[]
        {
            ("TargetReply.Error", reply.Error ?? ""),
            ("ILogger rendered messages", string.Join("\n", logger.Messages)),
            ("ILogger structured state", string.Join("\n", logger.State.Select(value => $"{value.Key}={value.Value}"))),
            ("ILogger exceptions", string.Join("\n", logger.Exceptions)),
        }, value => AssertNoWireDiagnostic(value.Channel, value.Text));
        if (expectedError is not null)
        {
            Assert.Equal(expectedError, reply.Error);
            Assert.Contains(logger.Messages, message => message.Contains(expectedError, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Generic_JSON_transport_diagnostics_remain_unchanged_in_reply_and_logs()
    {
        const string diagnostic = "Synthetic gateway diagnostic <details>unchanged</details>";
        var logger = new CapturingTargetLogger();
        using var http = new StubHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(
            new HttpRequestException(diagnostic, null, HttpStatusCode.BadGateway)));
        await using var host = await CreateAsync(http, targetLogger: logger,
            targets: t => t["json"] = new TargetOptions { Kind = "Http", Endpoint = "https://generic.example.invalid/json" });

        var reply = await host.Get<TargetClient>().SendAsync("json", [new("user", Greeting)]);

        Assert.Equal(TargetSource.Unavailable, reply.Source);
        Assert.Equal(diagnostic, reply.Error);
        Assert.Equal("application/json; charset=utf-8", Assert.Single(http.Requests).ContentType);
        Assert.Contains(logger.Messages, message => message.Contains(diagnostic, StringComparison.Ordinal));
        Assert.Contains(logger.State, value => Equals(value.Value, diagnostic));
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("ro-RO")]
    public async Task Model_profile_and_local_agent_display_strings_are_localized_but_kinds_and_diagnostics_are_not(string culture)
    {
        using var language = new CultureScope(culture);
        const string diagnostic = "Provider diagnostic stays verbatim";
        var main = new FakeChatClient().Reply("   ").Reply("agent-ready");
        var review = new FakeChatClient().Fail(diagnostic);
        using var http = FailingHttp();
        await using var host = await CreateAsync(http, main,
            ai: o =>
            {
                o.Profiles["review"] = new AiProfile { Model = "review-model" };
                o.Profiles["missing"] = new AiProfile { Model = "missing-model" };
            },
            profiles: new Dictionary<string, IChatClient> { ["review"] = review },
            targets: t => t["assistant"] = new TargetOptions { Kind = "Agent", Agent = "assistant", Label = "Synthetic assistant" });

        var results = await host.Get<ConnectionCheck>().RunAsync();

        Assert.Equal(4, results.Count);
        var model = results[0];
        Assert.True(model.Ok);
        Assert.Equal("model", model.Kind);
        Assert.Equal(Text(culture, "main model", "model principal"), model.Name);
        Assert.Equal($"Fake · fake-model: “{Text(culture, "(empty)", "(gol)")}”", model.Detail);

        var profile = results.Single(r => r.Name.Contains("review", StringComparison.Ordinal));
        Assert.False(profile.Ok);
        Assert.Equal("model", profile.Kind);
        Assert.Equal(Text(culture, "profile “review”", "profil „review”"), profile.Name);
        Assert.Equal($"Fake · review-model: {diagnostic}", profile.Detail);

        var missing = results.Single(r => r.Name.Contains("missing", StringComparison.Ordinal));
        Assert.False(missing.Ok);
        Assert.Equal("model", missing.Kind);
        Assert.Equal(Text(culture, "profile “missing”", "profil „missing”"), missing.Name);
        Assert.Equal(Text(culture, "not usable: missing or invalid settings, the main model is used instead",
            "inutilizabil: setări lipsă sau incorecte; se folosește modelul principal"), missing.Detail);

        var agent = Assert.Single(results, r => r.Kind == "target");
        Assert.True(agent.Ok);
        Assert.Equal(Text(culture, "target “Synthetic assistant”", "țintă „Synthetic assistant”"), agent.Name);
        Assert.Equal($"Agent · {Text(culture, "this app", "această aplicație")}: “agent-ready”", agent.Detail);
        Assert.Equal(2, main.Calls.Count);
        Assert.Single(review.Calls);
        Assert.Contains(Greeting, main.AllUserText);
        Assert.Empty(http.Requests);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("ro-RO")]
    public async Task Generic_JSON_targets_keep_the_greeting_live_reply_and_localized_failure_or_replay_details(string culture)
    {
        using var language = new CultureScope(culture);
        const string diagnostic = "Synthetic gateway unreachable";
        const string errorBody = """{"error":"synthetic JSON failure"}""";
        using var http = new StubHttpHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/json" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"answer":"generic-ready"}""", Encoding.UTF8, "application/json"),
            }),
            "/http-error" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            }),
            _ => Task.FromException<HttpResponseMessage>(new HttpRequestException(diagnostic)),
        });
        await using var host = await CreateAsync(http, ai: o => o.Record = true, targets: t =>
        {
            foreach (var name in new[] { "json", "down", "cached", "http-error" })
                t[name] = new TargetOptions { Kind = "Http", Endpoint = $"https://generic.example.invalid/{name}", Label = name };
        });
        host.Get<TargetRecordings>().Save("cached", [new("user", Greeting)], "recorded generic answer");

        var results = await host.Get<ConnectionCheck>().RunAsync();

        Assert.Equal(4, results.Count);
        Assert.Equal(4, http.Requests.Count);
        Assert.All(http.Requests, sent =>
        {
            Assert.Equal("application/json; charset=utf-8", sent.ContentType);
            Assert.Null(sent.SoapAction);
            using var body = JsonDocument.Parse(sent.Body);
            var turn = Assert.Single(body.RootElement.GetProperty("messages").EnumerateArray());
            Assert.Equal("user", turn.GetProperty("role").GetString());
            Assert.Equal(Greeting, turn.GetProperty("content").GetString());
        });
        Assert.All(results, r => Assert.Equal("target", r.Kind));
        var live = results.Single(r => r.Name.Contains("json", StringComparison.Ordinal));
        Assert.True(live.Ok);
        Assert.Equal(Text(culture, "target “json”", "țintă „json”"), live.Name);
        Assert.Equal("Http · generic.example.invalid: “generic-ready”", live.Detail);
        var down = results.Single(r => r.Name.Contains("down", StringComparison.Ordinal));
        Assert.False(down.Ok);
        Assert.Equal(Text(culture, $"down: {diagnostic}", $"indisponibil: {diagnostic}"), down.Detail);
        var replay = results.Single(r => r.Name.Contains("cached", StringComparison.Ordinal));
        Assert.False(replay.Ok);
        Assert.Equal(Text(culture, $"down ({diagnostic}), but a recorded answer exists",
            $"indisponibil ({diagnostic}), dar există un răspuns înregistrat"), replay.Detail);
        var error = results.Single(r => r.Name.Contains("http-error", StringComparison.Ordinal));
        Assert.False(error.Ok);
        Assert.Equal(Text(culture, $"down: HTTP 500: {errorBody}", $"indisponibil: HTTP 500: {errorBody}"), error.Detail);
    }

    [Fact]
    public async Task Viewer_is_refused_before_any_model_or_target_call()
    {
        var main = new FakeChatClient().Reply("must not be called");
        using var http = Respond(SoapAnswer());
        await using var host = await CreateAsync(http, main, user: SiteConfig.Users.Single(u => u.Role == Roles.Viewer));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host.Get<ConnectionCheck>().RunAsync());

        Assert.Empty(main.Calls);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_for_SOAP_and_models_instead_of_returning_a_failure_or_replay()
    {
        using var cancelled = new CancellationTokenSource();
        using var http = new StubHttpHandler((_, ct) =>
        {
            cancelled.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(ct);
        });
        await using (var host = await CreateAsync(http, ai: o => o.Record = true))
        {
            host.Get<TargetRecordings>().Save(TinOnEuropaClient.Target, TinOnEuropaClient.Conversation("DE", ProbeTin), SoapAnswer());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Get<ConnectionCheck>().RunAsync(cancelled.Token));
            Assert.Single(http.Requests);
        }

        using var modelCancelled = new CancellationTokenSource();
        var main = new FakeChatClient().ReplyWith(_ =>
        {
            modelCancelled.Cancel();
            throw new OperationCanceledException(modelCancelled.Token);
        });
        using var unusedHttp = FailingHttp();
        await using var modelHost = await CreateAsync(unusedHttp, main, targets: _ => { });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => modelHost.Get<ConnectionCheck>().RunAsync(modelCancelled.Token));
        Assert.Single(main.Calls);
        Assert.Empty(unusedHttp.Requests);
    }

    private static string BareTinResponse(string flags = BothFalse) => $"""
        <t:checkTinResponse xmlns:t="{SoapNamespace}"><t:countryCode>DE</t:countryCode><t:tinNumber>{ProbeTin}</t:tinNumber>{flags}</t:checkTinResponse>
        """;

    private static string SoapEnvelope(string body) => $"""<s:Envelope xmlns:s="{Soap11Namespace}"><s:Body>{body}</s:Body></s:Envelope>""";

    private static string SoapAnswer(string flags = BothFalse) => SoapEnvelope(BareTinResponse(flags));

    private static string SoapFault(string detail) => SoapEnvelope($"""
        <s:Fault><faultcode>s:Server</faultcode><faultstring>Synthetic service failure</faultstring><detail>{detail}</detail></s:Fault>
        """);

    private static TargetOptions SoapTarget() => new()
    {
        Kind = "Http", Label = TinLabel, Endpoint = "https://tin.example.invalid/check",
        RequestTemplate = "{{text}}", ResponsePath = "", ContentType = "text/xml; charset=utf-8",
        Headers = new() { ["SOAPAction"] = "\"\"" },
    };

    private static Task<TestHost> CreateAsync(StubHttpHandler http, IChatClient? client = null,
        Action<AiOptions>? ai = null, DemoUser? user = null, IReadOnlyDictionary<string, IChatClient>? profiles = null,
        Action<Dictionary<string, TargetOptions>>? targets = null, ILogger<TargetClient>? targetLogger = null) =>
        TestHost.CreateAsync(client, ai, user: user, profiles: profiles ?? new Dictionary<string, IChatClient>(),
            targets: t =>
            {
                t.Clear();
                if (targets is null) t[TinOnEuropaClient.Target] = SoapTarget();
                else targets(t);
            },
            configure: services =>
            {
                services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new MemoryDatabase());
                services.AddHttpClient("targets").ConfigurePrimaryHttpMessageHandler(() => http);
                if (targetLogger is not null) services.AddSingleton(targetLogger);
            });

    private static StubHttpHandler Respond(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        }));

    private static string WireDiagnostic => $"{PrivateMarker} {ProbeTin} <Fault>synthetic private error body</Fault>";

    private static StubHttpHandler FailingHttp() => new((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException(WireDiagnostic)));

    private static string Text(string culture, string english, string romanian) => culture == "ro-RO" ? romanian : english;

    private static void AssertNoWireDiagnostic(string channel, string text)
    {
        string[] fragments = [ProbeTin, PrivateMarker, "<Fault>", "</Fault>", "<s:Envelope", "<s:Fault", "synthetic private error body"];
        var leaks = fragments.Where(fragment => text.Contains(fragment, StringComparison.Ordinal)).ToArray();
        Assert.True(leaks.Length == 0, $"{channel} leaked wire data ({string.Join(", ", leaks)}): {text}");
    }

    private static void AssertPrivate(ConnectionCheck.Result result)
    {
        var display = result.Name + "\n" + result.Detail;
        Assert.DoesNotContain(ProbeTin, display);
        Assert.DoesNotContain(PrivateMarker, display);
        Assert.DoesNotContain("<", display);
        Assert.DoesNotContain("checkTinResponse", display);
        Assert.DoesNotContain("validStructure", display);
        Assert.DoesNotContain("validSyntax", display);
        Assert.DoesNotContain("tin valid", TextMatch.Normalize(display));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture) => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }

    private sealed class MemoryDatabase : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public MemoryDatabase()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        }

        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _connection.Dispose();
    }

    private sealed class CapturingTargetLogger : ILogger<TargetClient>
    {
        public List<string> Messages { get; } = [];
        public List<KeyValuePair<string, object?>> State { get; } = [];
        public List<string> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> values) State.AddRange(values);
            if (exception is not null) Exceptions.Add(exception.ToString());
        }
    }

    private sealed record SentRequest(HttpMethod Method, Uri? Uri, string? ContentType, string? SoapAction, string Body);

    private sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new SentRequest(request.Method, request.RequestUri, request.Content?.Headers.ContentType?.ToString(),
                request.Headers.TryGetValues("SOAPAction", out var values) ? Assert.Single(values) : null,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return await reply(request, ct);
        }
    }
}
