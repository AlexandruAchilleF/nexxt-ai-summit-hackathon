using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HackathonStarter.Agents;
using HackathonStarter.Features.Targets;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using HackathonStarter.Tests.Support;

namespace HackathonStarter.Tests.Crs;

/// <summary>TIN on Europa through TargetClient, with a local HttpListener: the Commission is never called from tests.</summary>
public class TinOnEuropaTests
{
    // Real answers captured on 28.10.2026.
    private const string ValidAnswer =
        """<env:Envelope xmlns:env="http://schemas.xmlsoap.org/soap/envelope/"><env:Header/><env:Body><ns2:checkTinResponse xmlns:ns2="urn:ec.europa.eu:taxud:tin:services:checkTin:types"><ns2:countryCode>DE</ns2:countryCode><ns2:tinNumber>86095174223</ns2:tinNumber><ns2:requestDate>2026-10-28+01:00</ns2:requestDate><ns2:validStructure>true</ns2:validStructure><ns2:validSyntax>true</ns2:validSyntax></ns2:checkTinResponse></env:Body></env:Envelope>""";

    private const string BadSyntaxAnswer =
        """<env:Envelope xmlns:env="http://schemas.xmlsoap.org/soap/envelope/"><env:Header/><env:Body><ns2:checkTinResponse xmlns:ns2="urn:ec.europa.eu:taxud:tin:services:checkTin:types"><ns2:countryCode>DE</ns2:countryCode><ns2:tinNumber>86095174224</ns2:tinNumber><ns2:requestDate>2026-10-28+01:00</ns2:requestDate><ns2:validStructure>true</ns2:validStructure><ns2:validSyntax>false</ns2:validSyntax></ns2:checkTinResponse></env:Body></env:Envelope>""";

    private const string FaultAnswer =
        """<env:Envelope xmlns:env="http://schemas.xmlsoap.org/soap/envelope/"><env:Header/><env:Body><env:Fault><faultcode>env:Server</faultcode><faultstring>INVALID_INPUT</faultstring></env:Fault></env:Body></env:Envelope>""";

    private const string Down = "http://127.0.0.1:9";

    private static TargetOptions Target(string endpoint, int timeoutSeconds = 5) => new()
    {
        Kind = "Http",
        Label = "TIN on Europa (European Commission)",
        Endpoint = endpoint,
        ContentType = "text/xml; charset=utf-8",
        Headers = new() { ["SOAPAction"] = "\"\"" },
        RequestTemplate = "{{text}}",
        ResponsePath = "",
        TimeoutSeconds = timeoutSeconds,
    };

    private static async Task<(TestHost Host, TinValidator Validator)> CreateAsync(TargetOptions? target, bool record = false)
    {
        var host = await TestHost.CreateAsync(client: null, ai: o => o.Record = record,
            targets: t => { if (target is not null) t[TinOnEuropaClient.Target] = target; });
        return (host, new TinValidator(new TinFormat(), new TinOnEuropaClient(host.Get<TargetClient>())));
    }

    [Fact]
    public void Empty_response_path_returns_the_whole_body() => Assert.Equal("<a/>", TargetClient.ReadPath("<a/>", ""));

    [Fact]
    public async Task Json_targets_still_send_utf8()
    {
        await using var service = new FakeService(200, """{"answer":"ok"}""");
        await using var host = await TestHost.CreateAsync(client: null, targets: t => t["json"] = new TargetOptions { Kind = "Http", Endpoint = service.Url });

        await host.Get<TargetClient>().SendAsync("json", [new ChatTurn("user", "hi")]);

        Assert.Equal("application/json; charset=utf-8", service.ContentType);
    }

    [Fact]
    public void Request_envelope_escapes_values()
    {
        Assert.Contains("<urn:tinNumber>86095174223</urn:tinNumber>", TinOnEuropaClient.BuildRequest("DE", "86095174223"));
        Assert.Contains("&lt;x&gt;&amp;", TinOnEuropaClient.BuildRequest("DE", "<x>&"));
    }

    [Fact]
    public void Parses_the_real_answers()
    {
        Assert.Equal(new TinOnEuropaAnswer(true, true), TinOnEuropaClient.Parse(ValidAnswer));
        Assert.Equal(new TinOnEuropaAnswer(true, false), TinOnEuropaClient.Parse(BadSyntaxAnswer));
        Assert.Null(TinOnEuropaClient.Parse(FaultAnswer));
        Assert.Null(TinOnEuropaClient.Parse("not xml"));
    }

    [Fact]
    public async Task Live_call_goes_through_TargetClient_as_soap()
    {
        await using var service = new FakeService(200, ValidAnswer);
        var (host, validator) = await CreateAsync(Target(service.Url));
        await using var _ = host;

        var check = await validator.CheckAsync("DE", "86095174223");

        Assert.StartsWith("text/xml", service.ContentType);
        Assert.Contains("<urn:tinNumber>86095174223", service.Body);
        Assert.Equal(TinFormatStatus.Valid, check.Status);
        Assert.Equal(TinCheckSource.TinOnEuropaLive, check.Source);
    }

    [Fact]
    public async Task Commission_answer_wins_and_local_disagreement_is_noted()
    {
        await using var service = new FakeService(200, BadSyntaxAnswer);
        var (host, validator) = await CreateAsync(Target(service.Url));
        await using var _ = host;

        var check = await validator.CheckAsync("DE", "86095174223");

        Assert.Equal(TinFormatStatus.Invalid, check.Status);
        Assert.Contains("Local rules say format valid", check.Note);
    }

    [Fact]
    public async Task Service_down_falls_back_to_local_rules_quickly()
    {
        var (host, validator) = await CreateAsync(Target(Down, timeoutSeconds: 3));
        await using var _ = host;
        var watch = Stopwatch.StartNew();

        var check = await validator.CheckAsync("DE", "86095174223");

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Equal(TinCheckSource.LocalRules, check.Source);
        Assert.Equal(TinFormatStatus.Valid, check.Status);
        Assert.Contains("TIN on Europa", check.Note);
    }

    [Fact]
    public async Task Recorded_answer_is_replayed_when_the_service_is_down()
    {
        var (host, validator) = await CreateAsync(Target(Down), record: true);
        await using var _ = host;
        host.Get<TargetRecordings>().Save(TinOnEuropaClient.Target, TinOnEuropaClient.Conversation("DE", "86095174223"), ValidAnswer);

        var check = await validator.CheckAsync("DE", "86095174223");

        Assert.Equal(TinCheckSource.TinOnEuropaReplay, check.Source);
        Assert.Equal(TinFormatStatus.Valid, check.Status);
    }

    [Fact]
    public async Task Recording_of_another_tin_is_never_replayed()
    {
        // Both pass the card checksum, so PiiVault masks them to the same placeholder and the replay keys collide.
        const string recorded = "1023217608406", other = "1023217608018";
        Assert.Equal(PiiVault.ForStorage().Mask(recorded), PiiVault.ForStorage().Mask(other));
        Assert.True(new TinFormat().Check("FR", recorded).FormatValid);
        Assert.False(new TinFormat().Check("FR", other).FormatValid);
        var (host, validator) = await CreateAsync(Target(Down), record: true);
        await using var _ = host;
        host.Get<TargetRecordings>().Save(TinOnEuropaClient.Target, TinOnEuropaClient.Conversation("FR", recorded),
            ValidAnswer.Replace(">DE<", ">FR<").Replace("86095174223", recorded));

        var check = await validator.CheckAsync("FR", other);

        Assert.Equal(TinCheckSource.LocalRules, check.Source);
        Assert.Equal(TinFormatStatus.Invalid, check.Status);
    }

    [Fact]
    public void Non_boolean_flags_are_not_an_answer() =>
        Assert.Null(TinOnEuropaClient.Parse(ValidAnswer.Replace("<ns2:validSyntax>true</ns2:validSyntax>", "<ns2:validSyntax/>")));

    [Fact]
    public async Task Non_eu_country_never_calls_the_service()
    {
        await using var service = new FakeService(200, ValidAnswer);
        var (host, validator) = await CreateAsync(Target(service.Url));
        await using var _ = host;

        var check = await validator.CheckAsync("US", "521-48-3907");

        Assert.Equal(0, service.Calls);
        Assert.Equal(TinCheckSource.LocalRules, check.Source);
        Assert.Equal(TinFormatStatus.Valid, check.Status);
    }

    [Fact]
    public async Task Soap_fault_is_not_a_crash()
    {
        await using var service = new FakeService(500, FaultAnswer);
        var (host, validator) = await CreateAsync(Target(service.Url));
        await using var _ = host;

        var check = await validator.CheckAsync("DE", "86095174223");

        Assert.Equal(TinCheckSource.LocalRules, check.Source);
        Assert.Contains("unavailable", check.Note);
    }

    [Fact]
    public async Task Missing_target_configuration_falls_back_to_local_rules()
    {
        var (host, validator) = await CreateAsync(target: null);
        await using var _ = host;

        var check = await validator.CheckAsync("IT", "DBRCST90A41Z129T");

        Assert.Equal(TinCheckSource.LocalRules, check.Source);
        Assert.Equal(TinFormatStatus.Valid, check.Status);
    }

    [Fact]
    public async Task Labels_say_format_valid_never_tin_valid()
    {
        foreach (var culture in new[] { "ro", "en" })
            await InCulture(culture, () =>
            {
                foreach (var source in Enum.GetValues<TinCheckSource>())
                foreach (var formatValid in new[] { true, false })
                foreach (var normalized in new[] { "86095174223", "" })
                {
                    var check = new TinCheck("DE", normalized, formatValid, source, "rule");
                    Assert.DoesNotContain("tin valid", TextMatch.Normalize(check.Label));
                    if (check.Status == TinFormatStatus.Valid) Assert.Contains("format valid", check.Label);
                }
                return Task.FromResult(0);
            });
    }

    private static async Task<T> InCulture<T>(string culture, Func<Task<T>> action)
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>A local stand-in for the Commission's service: records what it receives, answers with a fixed body.</summary>
    private sealed class FakeService : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _loop;
        private int _calls;

        public FakeService(int status, string answer)
        {
            var port = FreePort();
            Url = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _loop = Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); }
                    catch (Exception) { return; }
                    Interlocked.Increment(ref _calls);
                    ContentType = context.Request.ContentType;
                    using (var reader = new StreamReader(context.Request.InputStream)) Body = await reader.ReadToEndAsync();
                    context.Response.StatusCode = status;
                    context.Response.ContentType = "text/xml; charset=utf-8";
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(answer));
                    context.Response.Close();
                }
            });
        }

        public string Url { get; }
        public string? ContentType { get; private set; }
        public string Body { get; private set; } = "";
        public int Calls => _calls;

        public async ValueTask DisposeAsync()
        {
            _listener.Close();
            await _loop;
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
