using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Checks;

/// <summary>
/// Deterministic verdicts: plain C# rules that decide pass / fail on a text (an agent's answer, a document,
/// a generated report). No model is involved, so the same input always gets the same verdict, and every
/// verdict says why, with the evidence (personal data in evidence is masked).
///
/// Use them where "an LLM judged it" isn't good enough: regression tests, compliance rules, numbers that must
/// match a calculator. A model may still explain a verdict to a human; it never decides it.
///
/// Add a check: a class deriving from <see cref="Check"/> registered with <c>services.AddSingleton&lt;ICheck, MyCheck&gt;()</c>,
/// or a phrase rule in appsettings (Checks:PhraseRules), see <see cref="CheckSetup"/>.
/// </summary>
public interface ICheck
{
    /// <summary>Stable, URL-safe id ("pii-leak"). Tests refer to checks by id.</summary>
    string Id { get; }
    /// <summary>Bump when the rule changes, so an old verdict is never confused with a new one.</summary>
    string Version { get; }
    string Title { get; }
    string Description { get; }
    CheckResult Evaluate(CheckSubject subject);
}

[JsonConverter(typeof(JsonStringEnumConverter<CheckOutcome>))]
public enum CheckOutcome { Pass, Fail, NotApplicable }

/// <summary>What is checked.</summary>
/// <param name="Text">The text under test, e.g. the agent's answer.</param>
/// <param name="Prompt">What the agent was asked: data the user gave can be repeated back, other data can't.</param>
/// <param name="Facts">Expected values for data-driven checks, e.g. "expected-number" = "1234.56".</param>
public record CheckSubject(string Text, string? Prompt = null, IReadOnlyDictionary<string, string>? Facts = null)
{
    public string? Fact(string key) =>
        Facts?.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}

/// <summary>Evidence for a verdict: what was found and where (excerpt with personal data masked).</summary>
public record CheckFinding(string What, string? Excerpt = null);

public record CheckResult(string CheckId, string Version, string Title, CheckOutcome Outcome, string Reason, List<CheckFinding> Findings);

public record CheckReport(List<CheckResult> Results, string Fingerprint)
{
    /// <summary>True when no check failed (not-applicable checks don't fail a report).</summary>
    public bool Passed => Results.All(r => r.Outcome != CheckOutcome.Fail);
    public int Failed => Results.Count(r => r.Outcome == CheckOutcome.Fail);
}

/// <summary>Base class with helpers for building results.</summary>
public abstract class Check : ICheck
{
    public abstract string Id { get; }
    public virtual string Version => "1";
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract CheckResult Evaluate(CheckSubject subject);

    protected CheckResult Pass(string reason) => new(Id, Version, Title, CheckOutcome.Pass, reason, []);
    protected CheckResult Fail(string reason, IEnumerable<CheckFinding> findings) => new(Id, Version, Title, CheckOutcome.Fail, reason, [.. findings]);
    protected CheckResult NotApplicable(string reason) => new(Id, Version, Title, CheckOutcome.NotApplicable, reason, []);

    /// <summary>The text around a match (about 40 characters each side), with personal data masked.</summary>
    protected static string Excerpt(string text, int index, int length)
    {
        var start = Math.Max(0, index - 40);
        var end = Math.Min(text.Length, index + length + 40);
        var excerpt = (start > 0 ? "…" : "") + text[start..end].ReplaceLineEndings(" ") + (end < text.Length ? "…" : "");
        return PiiVault.ForStorage().Mask(excerpt);
    }
}

/// <summary>Runs checks by id. Registered as a singleton; checks must be stateless.</summary>
public class CheckEngine(IEnumerable<ICheck> checks)
{
    private readonly List<ICheck> _checks = checks
        .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.Last()) // a later registration (e.g. from appsettings) replaces a built-in check with the same id
        .OrderBy(c => c.Id, StringComparer.Ordinal)
        .ToList();

    public IReadOnlyList<ICheck> All => _checks;

    public ICheck? Find(string id) => _checks.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <param name="ids">Checks to run, in this order; null = all of them. An unknown id throws <see cref="KeyNotFoundException"/>.</param>
    public CheckReport Evaluate(CheckSubject subject, IEnumerable<string>? ids = null)
    {
        var selected = ids is null
            ? _checks
            : ids.Distinct(StringComparer.OrdinalIgnoreCase).Select(id => Find(id) ?? throw new KeyNotFoundException($"Unknown check '{id}'")).ToList();
        var results = selected.Select(c => c.Evaluate(subject)).ToList();
        return new CheckReport(results, Fingerprint(subject, results));
    }

    /// <summary>
    /// Identifies the verdict: same text, prompt, facts and check versions give the same fingerprint and outcomes.
    /// Shown in the UI and exports, so a re-run can be compared with the original at a glance.
    /// </summary>
    public static string Fingerprint(CheckSubject subject, IEnumerable<CheckResult> results)
    {
        var sb = new StringBuilder();
        sb.Append(subject.Text).Append('\u0001').Append(subject.Prompt).Append('\u0001');
        foreach (var (key, value) in (subject.Facts ?? new Dictionary<string, string>()).OrderBy(f => f.Key, StringComparer.Ordinal))
            sb.Append(key).Append('=').Append(value).Append('\u0001');
        foreach (var r in results) sb.Append(r.CheckId).Append('@').Append(r.Version).Append(':').Append(r.Outcome).Append('\u0001');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }
}
