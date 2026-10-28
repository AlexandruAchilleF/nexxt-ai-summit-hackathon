using System.Security;
using System.Xml;
using System.Xml.Linq;
using HackathonStarter.Agents;
using HackathonStarter.Features.Targets;

namespace HackathonStarter.Features.Tin;

public record TinOnEuropaAnswer(bool ValidStructure, bool ValidSyntax);

/// <summary>
/// The European Commission's TIN on Europa check (SOAP), through TargetClient: timeout, record/replay and /privacy.
/// EU member states only, and it checks the format only, not that the TIN exists (its WSDL says so).
/// </summary>
public sealed class TinOnEuropaClient(TargetClient? targets)
{
    public const string Target = "tin-on-europa";
    private const string Ns = "urn:ec.europa.eu:taxud:tin:services:checkTin:types";

    public static IReadOnlySet<string> EuCountries { get; } = new HashSet<string>
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU",
        "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK",
    };

    public static string BuildRequest(string country, string tin) =>
        $"""<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:urn="{Ns}"><soapenv:Header/><soapenv:Body><urn:checkTin><urn:countryCode>{SecurityElement.Escape(country)}</urn:countryCode><urn:tinNumber>{SecurityElement.Escape(tin)}</urn:tinNumber></urn:checkTin></soapenv:Body></soapenv:Envelope>""";

    /// <summary>What TargetClient sends, and the key of its recordings.</summary>
    public static IReadOnlyList<ChatTurn> Conversation(string country, string tin) => [new("user", BuildRequest(country, tin))];

    /// <summary>The answer, or null for anything that is not a checkTinResponse with both flags (a SOAP fault, garbage).</summary>
    public static TinOnEuropaAnswer? Parse(string xml) =>
        Response(xml) is { } r && Flag(r, "validStructure") is { } structure && Flag(r, "validSyntax") is { } syntax
            ? new(structure, syntax)
            : null;

    /// <summary>Never throws for a service failure: no answer comes back as (null, Unavailable, error).</summary>
    public async Task<(TinOnEuropaAnswer? Answer, TargetSource Source, string? Error)> CheckAsync(string country, string tin,
        CancellationToken ct = default)
    {
        if (targets?.Find(Target) is null) return (null, TargetSource.Unavailable, $"Targets:{Target} is not configured");
        var reply = await targets.SendAsync(Target, Conversation(country, tin), ct);
        if (reply.Source == TargetSource.Unavailable) return (null, reply.Source, reply.Error);
        // Recordings are keyed by the masked request: two CNPs (or two card-like TINs) share a key, so replay only an
        // answer that echoes this exact country and TIN.
        var forThisTin = reply.Source == TargetSource.Live || IsAbout(reply.Text, country, tin);
        return forThisTin && Parse(reply.Text) is { } answer
            ? (answer, reply.Source, reply.Error)
            : (null, TargetSource.Unavailable, reply.Error ?? "unexpected answer");
    }

    private static XElement? Response(string xml)
    {
        try { return XDocument.Parse(xml).Descendants(XName.Get("checkTinResponse", Ns)).FirstOrDefault(); }
        catch (XmlException) { return null; }
    }

    private static bool IsAbout(string xml, string country, string tin) =>
        Response(xml) is { } r && Value(r, "countryCode") == country && Value(r, "tinNumber") == tin;

    private static string? Value(XElement response, string name) => response.Element(XName.Get(name, Ns))?.Value;

    private static bool? Flag(XElement response, string name) => bool.TryParse(Value(response, name), out var flag) ? flag : null;
}
