using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Targets;
using HackathonStarter.Features.Tin;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Services;

/// <summary>
/// "Does everything answer?" before going on stage: sends a tiny request to the main model, every model profile
/// and every target, and reports which ones work and how fast. Admins/members only (it spends a few tokens).
/// </summary>
public class ConnectionCheck(ChatClientProvider ai, TargetClient targets, CurrentUser currentUser)
{
    public record Result(string Name, string Kind, bool Ok, long Milliseconds, string Detail);

    public async Task<List<Result>> RunAsync(CancellationToken ct = default)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        var results = new List<Result>();

        // One after the other: easier to read in order, and a slow target doesn't distort the others' timings.
        if (ai.IsConfigured) results.Add(await ModelAsync(Lang.T("main model"), null, ct));
        foreach (var profile in ai.Options.Profiles.Keys)
        {
            results.Add(ai.ClientFor(profile) == ai.Client
                ? new Result(Lang.T("profile “{0}”", profile), "model", false, 0, Lang.T("not usable: missing or invalid settings, the main model is used instead"))
                : await ModelAsync(Lang.T("profile “{0}”", profile), profile, ct));
        }
        foreach (var target in targets.List()) results.Add(await TargetAsync(target, ct));

        return results;
    }

    private async Task<Result> ModelAsync(string name, string? profile, CancellationToken ct)
    {
        var options = ai.OptionsFor(profile);
        var watch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var response = await ai.ClientFor(profile)!.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with the single word OK.")], cancellationToken: timeout.Token);
            return new Result(name, "model", true, watch.ElapsedMilliseconds, $"{options.Provider} · {options.Model}: “{Short(response.Text)}”");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return new Result(name, "model", false, watch.ElapsedMilliseconds, $"{options.Provider} · {options.Model}: {Short(ex.Message)}");
        }
    }

    private async Task<Result> TargetAsync(TargetInfo target, CancellationToken ct)
    {
        var isTin = target.Name.Equals(TinOnEuropaClient.Target, StringComparison.OrdinalIgnoreCase);
        // An intentionally incorrect check digit from the synthetic TIN fixtures. This checks connectivity, not validity.
        IReadOnlyList<ChatTurn> conversation = isTin
            ? TinOnEuropaClient.Conversation("DE", "86095174224")
            : [new ChatTurn("user", "Hello, are you there?")];
        var reply = await targets.SendAsync(target.Name, conversation, ct);
        var ok = reply.Source == TargetSource.Live && (!isTin || IsTinSoapReply(reply.Text));
        // Never display the probe's TIN or raw SOAP response/error in Settings.
        var error = isTin
            ? Lang.T(reply.Source == TargetSource.Live ? "unexpected SOAP response" : "live SOAP check failed")
            : Short(reply.Error);
        var host = target.Kind.Equals("Agent", StringComparison.OrdinalIgnoreCase) ? Lang.T("this app") : target.Host;
        var detail = reply.Source switch
        {
            TargetSource.Live when ok => isTin
                ? $"{target.Kind} · {host}: {Lang.T("SOAP checkTin response received (connectivity only)")}"
                : $"{target.Kind} · {host}: “{Short(reply.Text)}”",
            TargetSource.Replay => Lang.T("down ({0}), but a recorded answer exists", error),
            _ => Lang.T("down: {0}", error),
        };
        return new Result(Lang.T("target “{0}”", target.Label), "target", ok, reply.ElapsedMs, detail);
    }

    private static bool IsTinSoapReply(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000,
            });
            var root = XDocument.Load(reader).Root;
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace tin = "urn:ec.europa.eu:taxud:tin:services:checkTin:types";
            if (root is null || root.Name != soap + "Envelope") return false;
            var children = root.Elements().ToArray();
            if (children.Length is < 1 or > 2 || children[^1].Name != soap + "Body"
                || (children.Length == 2 && children[0].Name != soap + "Header")) return false;
            var body = children[^1];
            var payloads = body.Elements().ToArray();
            if (payloads.Length != 1 || payloads[0].Name != tin + "checkTinResponse"
                || root.Descendants(soap + "Fault").Any()) return false;
            if (root.Nodes().OfType<XText>().Concat(body.Nodes().OfType<XText>()).Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                return false;
            // Parse only the direct operation payload, not a matching element inside a header or fault.
            return TinOnEuropaClient.Parse(payloads[0].ToString(SaveOptions.DisableFormatting)) is not null;
        }
        catch (XmlException) { return false; }
    }

    private static string Short(string? text) =>
        string.IsNullOrWhiteSpace(text) ? Lang.T("(empty)") : text.Length <= 120 ? text.Trim() : text[..120].Trim() + "…";
}
