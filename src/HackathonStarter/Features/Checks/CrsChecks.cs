using System.Text.Json;
using System.Text.RegularExpressions;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Checks;

/// <summary>
/// Verdicts on the CRS agent's answers (docs/PLAN.md 13.4): a TIN is never called valid (only its format), a
/// context signal is never an alert, every indicium cites its rule and every quotation is verified evidence.
/// Matching ignores case and diacritics, so Romanian and English answers get the same verdict.
/// </summary>
public static partial class CrsChecks
{
    /// <summary>
    /// Code checks the TIN format only: the answer says "format valid" / "valid format", never "TIN valid".
    /// Evaluated on whole words by <see cref="NoTinValidClaimCheck"/>.
    /// </summary>
    public static readonly PhraseRule NoTinValidClaim = new()
    {
        Id = "no-tin-valid-claim",
        Title = "No \"TIN valid\" claim",
        Description = "Only the TIN format is checked: say \"format valid\" or \"valid format\", never that the TIN is valid or correct.",
        Phrases =
        [
            "TIN valid", "TIN-ul valid", "TIN-ul este valid", "TIN-ul e valid", "TIN-ul este corect", "TIN-ul e corect",
            "TINul este valid", "TINul e valid", "TINul este corect", "TINul e corect",
            "codul TIN este valid", "codul TIN e valid", "codul TIN este corect", "codul TIN e corect",
            "cod fiscal valid", "codul fiscal este valid", "codul fiscal e valid", "NIF valid",
            "valid TIN", "TIN is valid", "TIN is correct",
            "TIN-uri valide", "TIN-urile sunt valide", "TINs are valid",
        ],
    };

    /// <summary>A negation this many words before an indicium or alert word cancels it: "un semnal, fără alertă".</summary>
    public const int NegationWindow = 3;

    // Normalized (lower case, no diacritics) word forms, Romanian and English.
    internal static readonly HashSet<string> SignalWords =
        ["semnal", "semnalul", "semnale", "semnalele", "semnalului", "semnalelor", "signal", "signals"];
    internal static readonly HashSet<string> IndiciumWords =
        ["indiciu", "indiciul", "indiciului", "indicii", "indiciile", "indiciilor", "indicium", "indicia"];
    internal static readonly HashSet<string> AlertWords =
        [.. IndiciumWords, "alerta", "alerte", "alertei", "alertele", "alertelor", "alert", "alerts"];
    private static readonly HashSet<string> Negations =
        ["fara", "nu", "nici", "niciun", "nicio", "niciodata", "non", "no", "not", "never", "without", "nor", "neither"];
    // A period after these doesn't end a sentence ("ex. CRS-IND-ADDR"), nor after a lower-case letter or one before a
    // number ("p. 29", "P. 29"); after an upper-case letter it does ("motivul declarat este B. Salariul…").
    private static readonly HashSet<string> Abbreviations = ["ex", "eg", "ie", "pag", "pp", "nr", "art", "alin", "lit", "pct", "cf", "vs"];

    /// <summary>
    /// The sentences of a text: split at line breaks and after ".", "!" or "?" followed by a space, unless the period
    /// ends an abbreviation ("p. 29") or the next word starts in lower case. Table headers, table separators and
    /// horizontal rules ("---") are skipped. Headings ("## …", a line right above "---" / "===", or a whole bold line
    /// "**Indicii găsite**") and labels ("Indicii găsite:") are sentences too: the last sentence of a heading line, or
    /// one that ends with ':', also gets what it introduces (<see cref="Sentence.Introduces"/>).
    /// </summary>
    internal static List<Sentence> Sentences(string text)
    {
        var lines = Lines(text);
        var sentences = new List<Sentence>();
        for (var i = 0; i < lines.Count; i++)
        {
            var (lineStart, line) = lines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || TableSeparator().IsMatch(trimmed) || IsTableHeader(lines, i)) continue;

            var underlined = Underlined(lines, i);
            var first = sentences.Count;
            var start = 0;
            foreach (Match end in SentenceEnd().Matches(line))
            {
                var next = end.Index + end.Length;
                if (next >= line.Length || char.IsLower(line[next]) || IsAbbreviation(line, end.Index, next)) continue;
                Add(sentences, lineStart + start, line[start..next]);
                start = next;
            }
            Add(sentences, lineStart + start, line[start..]);
            if (sentences.Count > first && (IsHeading(lines, i) || IsLabel(sentences[^1].Text)))
                sentences[^1].Introduces = Introduced(lines, underlined ? i + 2 : i + 1);
        }
        return sentences;
    }

    /// <summary>The physical lines of a text, blank ones included, with the index where each starts.</summary>
    private static List<(int Start, string Text)> Lines(string text)
    {
        var lines = new List<(int Start, string Text)>();
        var start = 0;
        foreach (Match lineBreak in LineBreak().Matches(text))
        {
            lines.Add((start, text[start..lineBreak.Index]));
            start = lineBreak.Index + lineBreak.Length;
        }
        lines.Add((start, text[start..]));
        return lines;
    }

    /// <summary>A table header row: the next line is a separator ("|---|---|", "|-|:-:|") and both lines have a '|'.</summary>
    private static bool IsTableHeader(List<(int Start, string Text)> lines, int i) =>
        i + 1 < lines.Count && lines[i].Text.Contains('|') && lines[i + 1].Text.Contains('|')
        && TableSeparator().IsMatch(lines[i + 1].Text.Trim());

    /// <summary>A heading: "## …", a whole bold line that is not a sentence ("**Indicii găsite**"), or a setext heading.</summary>
    private static bool IsHeading(List<(int Start, string Text)> lines, int i)
    {
        var trimmed = lines[i].Text.Trim();
        return trimmed.StartsWith('#') || BoldTitle().IsMatch(trimmed) || Underlined(lines, i);
    }

    /// <summary>A setext heading: the next line is "---" or "===".</summary>
    private static bool Underlined(List<(int Start, string Text)> lines, int i) =>
        i + 1 < lines.Count && SetextUnderline().IsMatch(lines[i + 1].Text.Trim());

    /// <summary>A label: the text ends with ':', bold markers aside ("**Indicii găsite:**").</summary>
    private static bool IsLabel(string text) => text.Trim().TrimEnd('*', '_', ' ').EndsWith(':');

    /// <summary>
    /// What a heading or label introduces, after any blank lines: each item of the list (its nested lines included,
    /// the parent list's next items not), each row of the table, or else the paragraph that follows, as one text.
    /// When that block is itself a subheading or a label ("Am găsit următoarele:"), what it introduces, each prefixed
    /// with its text (so a citation in it covers every item). Empty when nothing follows.
    /// </summary>
    private static List<string> Introduced(List<(int Start, string Text)> lines, int from)
    {
        var i = from;
        while (i < lines.Count && lines[i].Text.Trim().Length == 0) i++;
        var items = new List<string>();
        if (i == lines.Count) return items;

        if (IsTableHeader(lines, i))
        {
            for (i += 2; i < lines.Count && lines[i].Text.Contains('|'); i++) items.Add(lines[i].Text);
        }
        else if (ListItem().IsMatch(lines[i].Text))
        {
            var indent = Indent(lines[i].Text);
            for (; i < lines.Count; i++)
            {
                var line = lines[i].Text;
                if (line.Trim().Length == 0) continue;
                if (Indent(line) < indent) break; // the parent list goes on
                if (Indent(line) > indent) items[^1] += "\n" + line; // nested items and continuation lines
                else if (ListItem().IsMatch(line)) items.Add(line);
                else break;
            }
        }
        else if (IsHeading(lines, i))
        {
            return Prefixed(lines[i].Text, Introduced(lines, Underlined(lines, i) ? i + 2 : i + 1));
        }
        else
        {
            var paragraph = lines[i].Text;
            for (i++; i < lines.Count && lines[i].Text.Trim().Length > 0 && !lines[i].Text.TrimStart().StartsWith('#')
                      && !(IsLabel(paragraph) && ListItem().IsMatch(lines[i].Text)); i++)
                paragraph += "\n" + lines[i].Text;
            if (IsLabel(paragraph)) return Prefixed(paragraph, Introduced(lines, i));
            items.Add(paragraph);
        }
        return items;
    }

    private static List<string> Prefixed(string label, List<string> introduced) =>
        introduced.Count == 0 ? [label] : [.. introduced.Select(item => label + "\n" + item)];

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    private static void Add(List<Sentence> sentences, int start, string segment)
    {
        var lead = segment.Length - segment.TrimStart().Length;
        var trimmed = segment.Trim();
        if (trimmed.Length > 0) sentences.Add(new Sentence(start + lead, trimmed));
    }

    private static bool IsAbbreviation(string line, int periodIndex, int next)
    {
        if (line[periodIndex] != '.') return false;
        var start = periodIndex;
        while (start > 0 && (char.IsLetter(line[start - 1]) || line[start - 1] == '.')) start--;
        var word = line[start..periodIndex].Replace(".", "");
        if (word.Length == 1) return char.IsLower(word[0]) || char.IsDigit(line[next]);
        return Abbreviations.Contains(TextMatch.Normalize(word));
    }

    /// <summary>Words from <paramref name="set"/> in the sentence that no negation precedes within <see cref="NegationWindow"/> words.</summary>
    internal static IEnumerable<Match> Unnegated(Sentence sentence, HashSet<string> set) =>
        sentence.Words.Where((w, i) => set.Contains(w.Value) && !Negated(sentence.Words, i));

    private static bool Negated(IReadOnlyList<Match> words, int index)
    {
        for (var i = Math.Max(0, index - NegationWindow); i < index; i++)
        {
            var word = words[i].Value;
            if (Negations.Contains(word) || word.EndsWith("n't", StringComparison.Ordinal) || word.EndsWith("n’t", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>A sentence of the answer: where it starts in the text, and its normalized words.</summary>
    internal sealed class Sentence
    {
        public Sentence(int start, string text)
        {
            Start = start;
            Text = text;
            Normalized = TextMatch.Normalize(text);
            Words = Word().Matches(Normalized);
        }

        public int Start { get; }
        public string Text { get; }
        public string Normalized { get; }
        public IReadOnlyList<Match> Words { get; }

        /// <summary>For a heading or a label: the texts it introduces (see <see cref="Introduced"/>); null otherwise.</summary>
        public IReadOnlyList<string>? Introduces { get; set; }

        /// <summary>
        /// Position of a word in the original text. Normalize keeps the length of Latin text with diacritics, so
        /// indexes map back; otherwise the whole sentence is returned.
        /// </summary>
        public (int Index, int Length) Locate(Match word) =>
            Normalized.Length == Text.Length ? (Start + word.Index, word.Length) : (Start, Text.Length);
    }

    [GeneratedRegex(@"\r\n?|\n")]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"[.!?…]+\s+")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^\s*(?:[-*+•]|\d+[.)])\s")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^(?:-+|=+)$")]
    private static partial Regex SetextUnderline();

    /// <summary>A whole line in ** or __, optionally followed by ':', that doesn't end like a sentence.</summary>
    [GeneratedRegex(@"^(?:\*\*[^*]*[^*.!?\s]\*\*|__[^_]*[^_.!?\s]__):?$")]
    private static partial Regex BoldTitle();

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’]\p{L}+)?")]
    private static partial Regex Word();
}

/// <summary>
/// <see cref="CrsChecks.NoTinValidClaim"/>, matched on whole words of the normalized text: "invalid TIN format",
/// "TIN validation" and "valid ținând" contain no forbidden phrase. A plural ending ("valid TINs", "TIN valide")
/// still matches. A phrase right after "format" / "formatul" or right before "format" ("format TIN valid", "valid
/// TIN format") is about the format, so it passes too.
/// </summary>
public class NoTinValidClaimCheck : Check
{
    private readonly List<(string Phrase, Regex Pattern)> _phrases = [.. CrsChecks.NoTinValidClaim.Phrases.Select(p => (p, new Regex(
        @"(?<![\p{L}\p{N}])(?<!\bformat(?:ul|ului)?\s+)" + Regex.Escape(TextMatch.Normalize(p)) + @"(?:s|e)?(?![\p{L}\p{N}])(?!\s+format)")))];

    public PhraseRule Rule => CrsChecks.NoTinValidClaim;
    public override string Id => Rule.Id;
    public override string Version => Rule.Version;
    public override string Title => Rule.Title;
    public override string Description => Rule.Description;

    public override CheckResult Evaluate(CheckSubject subject)
    {
        // Normalize keeps the length for Latin text with diacritics (ă → a), so indexes map back to the original.
        var normalized = TextMatch.Normalize(subject.Text);
        var sameLength = normalized.Length == subject.Text.Length;
        var hits = _phrases
            .Select(p => (p.Phrase, Match: p.Pattern.Match(normalized)))
            .Where(h => h.Match.Success)
            .ToList();

        return hits.Count == 0
            ? Pass("None of the forbidden phrases was found.")
            : Fail($"Contains {hits.Count} forbidden phrase(s).",
                hits.Select(h => new CheckFinding($"“{h.Phrase}”", sameLength ? Excerpt(subject.Text, h.Match.Index, h.Match.Length) : null)));
    }
}

/// <summary>
/// A context signal (income, employer, studies abroad) is information, never an indicium or an alert: a sentence
/// that mentions a signal fails if it also has an indicium or alert word that no negation ("fără", "nu", "no",
/// "not", "without"…) precedes within <see cref="CrsChecks.NegationWindow"/> words. Keep signals and indicia in
/// separate sentences.
/// </summary>
public class SignalsNeverAlertCheck : Check
{
    public override string Id => "signals-never-alert";
    public override string Title => "Signals are never alerts";
    public override string Description =>
        "A context signal (income, employer, studies abroad) is never presented as an indicium or an alert in the same sentence, unless negated (\"a signal, no alert\").";

    public override CheckResult Evaluate(CheckSubject subject)
    {
        var withSignal = CrsChecks.Sentences(subject.Text)
            .Where(s => s.Words.Any(w => CrsChecks.SignalWords.Contains(w.Value)))
            .ToList();
        if (withSignal.Count == 0) return NotApplicable("The answer mentions no context signal.");

        var findings = new List<CheckFinding>();
        foreach (var sentence in withSignal)
        {
            if (CrsChecks.Unnegated(sentence, CrsChecks.AlertWords).FirstOrDefault() is not { } word) continue;
            var (index, length) = sentence.Locate(word);
            var what = CrsChecks.IndiciumWords.Contains(word.Value) ? "signal presented as an indicium" : "signal presented as an alert";
            findings.Add(new CheckFinding(what, Excerpt(subject.Text, index, length)));
        }

        return findings.Count == 0
            ? Pass($"{withSignal.Count} sentence(s) mention a signal; none presents it as an indicium or an alert.")
            : Fail($"{findings.Count} sentence(s) present a context signal as an indicium or an alert.", findings);
    }
}

/// <summary>
/// Every sentence that mentions an indicium (not negated: "fără indicii" mentions none) names the rule ID
/// ("CRS-IND-ADDR", "FATCA-IND-PHONE", "DL-NEW-90") or the guide page ("p. 29", "pagina 29", "p. 35-36"):
/// the indicium was decided by a rule, and the reader can look it up. A rule ID's first part after the prefix
/// starts with a letter, so a case code ("CRS-2026-0001") is not a citation. A heading or a label that ends with
/// ':' ("Am găsit două indicii:") may instead leave the citations to what it introduces: every item of the list,
/// every row of the table, or else the paragraph that follows must cite (past an intro label or a subheading).
/// </summary>
public partial class RuleCitedCheck : Check
{
    public override string Id => "rule-cited";
    public override string Title => "Every indicium cites its rule";
    public override string Description =>
        "Each sentence that mentions an indicium names the rule ID (e.g. CRS-IND-ADDR) or the guide page (e.g. p. 29); a heading or label may leave it to every item it introduces.";

    public override CheckResult Evaluate(CheckSubject subject)
    {
        var claims = CrsChecks.Sentences(subject.Text)
            .Select(s => (Sentence: s, Word: CrsChecks.Unnegated(s, CrsChecks.IndiciumWords).FirstOrDefault()))
            .Where(c => c.Word is not null)
            .ToList();
        if (claims.Count == 0) return NotApplicable("The answer mentions no indicium.");

        var uncited = claims
            .Where(c => !Cites(c.Sentence.Text) && !(c.Sentence.Introduces is { Count: > 0 } introduced && introduced.All(Cites)))
            .ToList();
        return uncited.Count == 0
            ? Pass($"All {claims.Count} sentence(s) about an indicium cite a rule ID or a guide page.")
            : Fail($"{uncited.Count} sentence(s) mention an indicium without a rule ID or a guide page.",
                uncited.Select(c =>
                {
                    var (index, length) = c.Sentence.Locate(c.Word!);
                    return new CheckFinding("indicium without rule ID or page", Excerpt(subject.Text, index, length));
                }));
    }

    /// <summary>The text names a rule ID or a guide page.</summary>
    private static bool Cites(string text) => RuleId().IsMatch(text) || PageReference().IsMatch(TextMatch.Normalize(text));

    [GeneratedRegex(@"\b(CRS|FATCA|SC|DL|GEN|RESP)-[A-Z][A-Z0-9]*(-[A-Z0-9]+)*\b")]
    public static partial Regex RuleId();

    /// <summary>On normalized text: "p. 29", "p.29", "pp. 35-36", "pag. 29", "pagina 29", "paginile 35-36", "page 29".</summary>
    [GeneratedRegex(@"\b(?:pp?|pag)\.\s?\d+|\b(?:pagina|paginile|page|pages)\s+\d+")]
    private static partial Regex PageReference();
}

/// <summary>
/// Every quotation in the answer is evidence that code verified: the model may only put in quotation marks what
/// the document says (docs/PLAN.md, EvidenceVerifier).
///
/// Fact <see cref="ToolResultsFact"/>: a JSON array of <c>{ "tool", "input", "output" }</c> objects, one per tool
/// call; "output" is any JSON (or a string holding JSON). Convention for the CRS tools: an object anywhere inside
/// an output, at any depth (arrays of evidence included), with a string property "quote" and a sibling
/// "verified": true is a verified quote. Property names are case-insensitive; "verified": false or missing, and
/// quotes in the input, don't count. Example output:
/// <code>{ "evidence": [ { "page": 2, "quote": "Hauptstraße 5, 10115 Berlin", "verified": true } ] }</code>
///
/// Quotations in the answer: text between „ or “ and ”, “ or a straight quote, between ” and ”, or between straight
/// double quotes, at least
/// <see cref="MinLength"/> characters. One passes when, ignoring case, diacritics and extra whitespace, it is part
/// of a verified quote; a quotation that adds words to a verified quote fails, because the document never says them.
/// Both sides must be in the same form (both real or both masked).
/// </summary>
public partial class EvidenceQuotedCheck : Check
{
    public const string ToolResultsFact = "tool-results";
    public const int MinLength = 8;

    public override string Id => "evidence-quoted";
    public override string Title => "Quotes are verified evidence";
    public override string Description =>
        $"Every quotation in the answer ({MinLength}+ characters) is one of the run's verified quotes (fact \"{ToolResultsFact}\").";

    public override CheckResult Evaluate(CheckSubject subject)
    {
        if (subject.Fact(ToolResultsFact) is not { } toolResults) return NotApplicable($"No \"{ToolResultsFact}\" fact given.");
        var quotations = Quotation().Matches(subject.Text)
            .Where(m => m.Groups["q"].Value.Trim().Length >= MinLength)
            .ToList();
        if (quotations.Count == 0) return NotApplicable("The answer contains no quotation.");

        List<string> verified;
        try
        {
            verified = [.. VerifiedQuotes(toolResults).Select(Canonical).Where(q => q.Length >= MinLength).Distinct()];
        }
        catch (JsonException)
        {
            return Fail($"The \"{ToolResultsFact}\" fact is not valid JSON, so no quotation can be verified.", quotations.Select(Finding));
        }

        var unverified = quotations.Where(m => !IsVerified(Canonical(m.Groups["q"].Value))).ToList();
        return unverified.Count == 0
            ? Pass($"All {quotations.Count} quotation(s) are verified evidence.")
            : Fail($"{unverified.Count} of {quotations.Count} quotation(s) are not among the {verified.Count} verified quote(s).", unverified.Select(Finding));

        bool IsVerified(string quotation) =>
            verified.Any(v => v.Contains(quotation, StringComparison.Ordinal));
        CheckFinding Finding(Match m) => new("unverified quotation", Excerpt(subject.Text, m.Index, m.Length));
    }

    /// <summary>The "quote" strings with a sibling "verified": true found in the outputs of <paramref name="toolResultsJson"/>.</summary>
    public static List<string> VerifiedQuotes(string toolResultsJson)
    {
        using var document = JsonDocument.Parse(toolResultsJson);
        var root = document.RootElement;
        var quotes = new List<string>();
        foreach (var result in root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToArray() : [root])
        {
            if (result.ValueKind != JsonValueKind.Object || Property(result, "output") is not { } output) continue;
            if (output.ValueKind == JsonValueKind.String)
            {
                // Tool steps keep their output as a JSON string; plain text holds no quotes.
                var raw = output.GetString()!.Trim();
                if (!raw.StartsWith('{') && !raw.StartsWith('[')) continue;
                try
                {
                    using var inner = JsonDocument.Parse(raw);
                    Collect(inner.RootElement, quotes);
                }
                catch (JsonException) { }
            }
            else Collect(output, quotes);
        }
        return quotes;
    }

    private static void Collect(JsonElement element, List<string> quotes)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) Collect(item, quotes);
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (Property(element, "quote") is { ValueKind: JsonValueKind.String } quote
                && Property(element, "verified") is { ValueKind: JsonValueKind.True })
                quotes.Add(quote.GetString()!);
            foreach (var property in element.EnumerateObject()) Collect(property.Value, quotes);
        }
    }

    private static JsonElement? Property(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }

    private static string Canonical(string text) => Whitespace().Replace(TextMatch.Normalize(text), " ").Trim();

    [GeneratedRegex(@"„(?<q>[^„“”""]+)[”“""]|“(?<q>[^“”""]+)[”""]|”(?<q>[^„“”""\r\n]+)”|""(?<q>[^""\r\n]+)""")]
    private static partial Regex Quotation();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
