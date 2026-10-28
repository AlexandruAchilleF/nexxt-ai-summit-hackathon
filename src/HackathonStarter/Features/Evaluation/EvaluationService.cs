using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;

namespace HackathonStarter.Features.Evaluation;

/// <summary>Runs the rules on the golden cases and the synthetic clients; the only source of the numbers on /metrics. Owner: R5.</summary>
public interface IEvaluationService
{
    Task<EvaluationReport> RunAsync(CancellationToken ct = default);
}

/// <summary>What the evaluation runs on: the golden cases, the synthetic clients and the generated PDFs. Tests pass their own.</summary>
public sealed record EvaluationInputs(
    Func<IReadOnlyList<GoldenCase>> Golden,
    Func<IReadOnlyList<SyntheticSnapshotCase>> Synthetic,
    Func<IReadOnlyList<SyntheticPdf>> Documents)
{
    /// <summary>Deterministic, so generated once per process; the evaluation only reads them.</summary>
    public static EvaluationInputs Default { get; } =
        new(() => GoldenCases.All, Once(() => SyntheticSnapshots.Create()), Once<IReadOnlyList<SyntheticPdf>>(() => [.. SyntheticPdfs.All()]));

    private static Func<T> Once<T>(Func<T> create)
    {
        var value = new Lazy<T>(create);
        return () => value.Value;
    }
}

/// <summary>
/// Measures the registered engine, shield and verifier on data whose truth comes from generation, never from the engine.
/// Pure code, no model and no network: safe to run when /metrics loads. <paramref name="configuration"/> is optional so the
/// service resolves without it (Phase0ContractTests).
/// </summary>
public sealed class EvaluationService(
    ICrsRuleEngine engine, IDeadlineCalculator deadlines, IHiddenTextShield shield, IEvidenceVerifier verifier, ISimClock clock,
    IConfiguration? configuration = null, EvaluationInputs? inputs = null) : IEvaluationService
{
    /// <summary>CrsRuleEngine.Version of the Phase 0 placeholder. A literal: R1's engine changes the constant.</summary>
    public const string Phase0EngineVersion = "0.0-phase0";

    public const string ManualMinutesKey = "Evaluation:ManualMinutesPerCase";

    /// <summary>Quotes a model might invent; skipped on a document whose generated truth contains one.</summary>
    public static IReadOnlyList<string> InventedQuotes { get; } =
    [
        "Rezidență fiscală: Statele Unite ale Americii",
        "Telefon: +1 312 555 0147",
        "Declar că sunt rezident fiscal în Germania din 01.09.2026.",
    ];

    private static readonly Concealment[] Concealments = [Concealment.White, Concealment.Tiny, Concealment.OffPage, Concealment.Invisible];

    private readonly EvaluationInputs _inputs = inputs ?? EvaluationInputs.Default;

    public Task<EvaluationReport> RunAsync(CancellationToken ct = default) => Task.Run(() => Run(ct), ct);

    private EvaluationReport Run(CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        var ranAt = clock.UtcNow;
        var golden = _inputs.Golden();
        var synthetic = _inputs.Synthetic();
        var spec = Spec(golden, synthetic, ct);
        var (shieldBench, quotes) = Documents(_inputs.Documents(), ct);
        var uncertainty = new UncertaintyReport(shieldBench, quotes, Extraction(), Signals(golden, synthetic));
        return new EvaluationReport(ranAt, Stopwatch.GetElapsedTime(start), spec, uncertainty, Efficiency());
    }

    // ---- Part 1: the specification -------------------------------------------------------------------------------------

    private SpecCoverage Spec(IReadOnlyList<GoldenCase> golden, IReadOnlyList<SyntheticSnapshotCase> synthetic, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        var versions = new SortedSet<string>(StringComparer.Ordinal);
        var goldenRows = golden.Select(c => { ct.ThrowIfCancellationRequested(); return Golden(c, versions) with
        {
            ExpectedIndicia = c.ExpectedIndicia.Count > 0 ? [.. c.ExpectedIndicia.Select(h => Key(h.RuleId, h.Jurisdiction, h.CoveredBy))] : c.ExpectedHits,
            ExpectedCleared = c.ExpectedClearedRules.Count > 0 ? [.. c.ExpectedClearedRules.Select(h => Key(h.RuleId, h.Jurisdiction, Coverage.None))] : c.ExpectedCleared,
        }; }).ToList();
        var syntheticRows = synthetic.Select(s => { ct.ThrowIfCancellationRequested(); return Synthetic(s, versions) with
        {
            ExpectedIndicia = [.. s.ExpectedHits.Select(h => Key(h.RuleId, h.Jurisdiction, Coverage.None))],
            ExpectedCleared = [.. s.ExpectedCleared.Select(h => Key(h.RuleId, h.Jurisdiction, Coverage.None))],
        }; }).ToList();
        var version = string.Join(", ", versions);
        var (goldenTotals, syntheticTotals) = (CoverageTotals.Of(goldenRows), CoverageTotals.Of(syntheticRows));
        return new SpecCoverage(SpecSource(versions, version, goldenTotals, syntheticTotals), version, goldenRows, goldenTotals,
            syntheticRows, syntheticTotals, Stopwatch.GetElapsedTime(start));
    }

    private SourceNote SpecSource(ISet<string> versions, string version, CoverageTotals golden, CoverageTotals synthetic)
    {
        var evaluated = golden.Evaluated + synthetic.Evaluated;
        if (versions.Count == 0)
            return evaluated == 0
                ? new(EvaluationSource.Unavailable, "Nothing was evaluated: no golden case has input and there is no synthetic client.",
                    "Nu s-a evaluat nimic: niciun caz-etalon nu are date de intrare și nu există clienți sintetici.")
                : new(EvaluationSource.Unavailable,
                    $"The registered rule engine ({engine.GetType().Name}) threw an exception on every evaluation ({CountEn(evaluated, "evaluation", "evaluations")}, see the failed rows), so there is no result.",
                    $"Motorul de reguli înregistrat ({engine.GetType().Name}) a aruncat o excepție la fiecare evaluare ({CountRo(evaluated, "evaluare", "evaluări")}, vezi rândurile picate), deci nu există un rezultat.");
        if (versions.Contains(Phase0EngineVersion))
            return new(EvaluationSource.Phase0Placeholder,
                $"The rule engine is not on main yet: the registered one is the phase 0 placeholder ({Phase0EngineVersion}), so these numbers are not a result.",
                $"Motorul de reguli nu e încă pe main: cel înregistrat e înlocuitorul din Faza 0 ({Phase0EngineVersion}), deci cifrele nu sunt un rezultat.");

        var scope = $"the registered engine ({version}) on {CountEn(golden.Evaluated, "golden case", "golden cases")} with input and {CountEn(synthetic.Evaluated, "synthetic client", "synthetic clients")}";
        var scopeRo = $"motorul înregistrat ({version}) pe {CountRo(golden.Evaluated, "caz-etalon", "cazuri-etalon")} cu date de intrare și {CountRo(synthetic.Evaluated, "client sintetic", "clienți sintetici")}";
        // The claim of PLAN 16.3 holds only when every evaluated row passes; the source stays Real either way.
        if (golden.Failed == 0 && synthetic.Failed == 0)
            return new(EvaluationSource.Real, $"The guide's rules are implemented and tested: {scope}.", $"Regulile din ghid sunt implementate și testate: {scopeRo}.");

        var failed = new List<(string En, string Ro)>();
        if (golden.Failed > 0)
            failed.Add((CountEn(golden.Failed, "golden case", "golden cases"), CountRo(golden.Failed, "caz-etalon", "cazuri-etalon")));
        if (synthetic.Failed > 0)
            failed.Add((CountEn(synthetic.Failed, "synthetic client", "synthetic clients"), CountRo(synthetic.Failed, "client sintetic", "clienți sintetici")));
        var one = golden.Failed + synthetic.Failed == 1;
        return new(EvaluationSource.Real,
            $"{string.Join(" and ", failed.Select(f => f.En))} {(one ? "does" : "do")} not match the specification yet (see the failed rows): {scope}.",
            $"{string.Join(" și ", failed.Select(f => f.Ro))} nu se {(one ? "potrivește" : "potrivesc")} încă cu specificația (vezi rândurile picate): {scopeRo}.");
    }

    /// <summary>"1 golden case", "2 golden cases".</summary>
    public static string CountEn(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

    /// <summary>
    /// Romanian numerals: singular at 1; "de" when the last two digits are 00 or 20-99 ("20 de cazuri", "100 de cazuri"),
    /// none at 0, 2-19 and 101-119 ("12 cazuri", "101 cazuri").
    /// </summary>
    public static string CountRo(int n, string one, string many) =>
        n == 1 ? $"1 {one}" : n >= 20 && n % 100 is 0 or >= 20 ? $"{n} de {many}" : $"{n} {many}";

    private CoverageRow Golden(GoldenCase c, ISet<string> versions)
    {
        var workflow = c.Owner == GoldenOwner.R2;
        if (c.ExpectedDecision == Decision.ShieldAlert) return NotRun(c, workflow, CoverageStatus.Shield);
        if (!c.HasInput) return NotRun(c, workflow, CoverageStatus.NoInput);
        var selfCertification = c.Change!.Kind == ChangeKind.SelfCertSubmitted;
        if (selfCertification && c.Change.SelfCertification is null) return NotRun(c, workflow, CoverageStatus.NoInput);

        var start = Stopwatch.GetTimestamp();
        RuleOutcome outcome;
        try
        {
            outcome = selfCertification
                ? engine.EvaluateSelfCertification(c.After!, c.Change.SelfCertification!)
                : engine.Evaluate(c.Before!, c.After!, c.Change.Kind);
        }
        catch (Exception e)
        {
            return Threw(c.Id, c.ClientCode, null, null, workflow, c.ExpectedDecision, e, Stopwatch.GetElapsedTime(start));
        }
        versions.Add(outcome.EngineVersion);

        var deadline = c.ExpectedDeadline is { } expected ? Deadline(expected, outcome, c.After!, c.ReceivedAt!.Value) : null;
        if (workflow)
        {
            var passed = outcome.Decision == c.ExpectedDecision && deadline?.Matches != false;
            return new(c.Id, c.ClientCode, null, null, true, passed ? CoverageStatus.Passed : CoverageStatus.Failed,
                c.ExpectedDecision, outcome.Decision, null, null, null, deadline, [], [], [], null, Stopwatch.GetElapsedTime(start));
        }

        var (missing, unexpected) = CompareIndicia(c.ExpectedIndicia.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)), outcome.Indicia);
        var missingCleared = MissingCleared(c.ExpectedClearedRules.Select(h => (h.RuleId, h.Jurisdiction)), outcome.Cleared);
        bool? reportable = c.ExpectedReportableFor is { } r ? r.ToHashSet(StringComparer.Ordinal).SetEquals(outcome.ReportableFor) : null;
        return Compared(c.Id, c.ClientCode, null, null, c.ExpectedDecision, outcome, missing, unexpected, missingCleared, reportable,
            deadline, Stopwatch.GetElapsedTime(start));
    }

    /// <summary>The label and the expected decision come from the scenario chosen at generation, never from the engine.</summary>
    private CoverageRow Synthetic(SyntheticSnapshotCase s, ISet<string> versions)
    {
        var start = Stopwatch.GetTimestamp();
        RuleOutcome outcome;
        try
        {
            outcome = engine.Evaluate(s.Before, s.After, s.Change.Kind);
        }
        catch (Exception e)
        {
            return Threw(s.Code, s.Code, s.Label, s.LabelEn, false, s.Expected, e, Stopwatch.GetElapsedTime(start));
        }
        versions.Add(outcome.EngineVersion);

        var (missing, unexpected) = CompareIndicia(s.ExpectedHits.Select(h => (h.RuleId, h.Jurisdiction, Coverage.None)), outcome.Indicia);
        var missingCleared = MissingCleared(s.ExpectedCleared.Select(h => (h.RuleId, h.Jurisdiction)), outcome.Cleared);
        return Compared(s.Code, s.Code, s.Label, s.LabelEn, s.Expected, outcome, missing, unexpected, missingCleared, null, null,
            Stopwatch.GetElapsedTime(start));
    }

    private static CoverageRow Compared(string id, string client, string? label, string? labelEn, Decision expected, RuleOutcome outcome,
        List<string> missing, List<string> unexpected, List<string> missingCleared, bool? reportable, DeadlineCheck? deadline, TimeSpan took)
    {
        var passed = outcome.Decision == expected && missing.Count == 0 && unexpected.Count == 0 && missingCleared.Count == 0
                     && reportable != false && deadline?.Matches != false;
        return new(id, client, label, labelEn, false, passed ? CoverageStatus.Passed : CoverageStatus.Failed, expected, outcome.Decision,
            missing.Count == 0 && unexpected.Count == 0, missingCleared.Count == 0, reportable, deadline,
            missing, unexpected, missingCleared, null, took);
    }

    private static CoverageRow NotRun(GoldenCase c, bool workflow, CoverageStatus status) =>
        new(c.Id, c.ClientCode, null, null, workflow, status, c.ExpectedDecision, null, null, null, null, null, [], [], [], null, TimeSpan.Zero);

    private static CoverageRow Threw(string id, string client, string? label, string? labelEn, bool workflow, Decision expected, Exception e, TimeSpan took) =>
        new(id, client, label, labelEn, workflow, CoverageStatus.Failed, expected, null, null, null, null, null, [], [], [], e.GetType().Name, took);

    /// <summary>Exact on (RuleId, Jurisdiction, CoveredBy); a null expected jurisdiction (GEN-*) matches any.</summary>
    private static (List<string> Missing, List<string> Unexpected) CompareIndicia(
        IEnumerable<(string RuleId, string? Jurisdiction, Coverage CoveredBy)> expected, IReadOnlyList<RuleHit> actual)
    {
        var left = actual.ToList();
        var missing = new List<string>();
        foreach (var e in expected.OrderBy(e => e.Jurisdiction is null))
        {
            var i = left.FindIndex(a => a.RuleId == e.RuleId && (e.Jurisdiction is null || a.Jurisdiction == e.Jurisdiction) && a.CoveredBy == e.CoveredBy);
            if (i < 0) missing.Add(Key(e.RuleId, e.Jurisdiction, e.CoveredBy));
            else left.RemoveAt(i);
        }
        return (missing, [.. left.Select(a => Key(a.RuleId, a.Jurisdiction, a.CoveredBy))]);
    }

    private static List<string> MissingCleared(IEnumerable<(string RuleId, string? Jurisdiction)> expected, IReadOnlyList<RuleHit> cleared) =>
        [.. expected.Where(e => !cleared.Any(c => c.RuleId == e.RuleId && (e.Jurisdiction is null || c.Jurisdiction == e.Jurisdiction)))
            .Select(e => Key(e.RuleId, e.Jurisdiction, Coverage.None))];

    private static string Key(string ruleId, string? jurisdiction, Coverage coveredBy) =>
        (string.IsNullOrEmpty(jurisdiction) ? ruleId : $"{ruleId}:{jurisdiction}") + (coveredBy == Coverage.None ? "" : $"/{coveredBy}");

    private DeadlineCheck Deadline(GoldenDeadline expected, RuleOutcome outcome, ClientSnapshot after, DateTime knownAt)
    {
        // NEVERIFICAT: which account sets the deadline is not in the guide. Ours: the most recently opened one, which gives the
        // earliest deadline (a new account's 90 days never end after a preexisting one's). R2 to make it a shared function.
        var account = after.Accounts.MaxBy(a => a.OpenedOn);
        if (outcome.Regime is not { } regime || account is null) return new(expected, null, false);

        var actual = deadlines.Compute(new DeadlineInput(knownAt, regime, account.OpenedOn));
        // No rule in the golden data = the guide gives no deadline (FATCA, Î1); the calculator answers FATCA-SC without a date.
        var matches = actual.Preexisting == expected.Preexisting && actual.DueAt == expected.DueAt && actual.RemindAt == expected.RemindAt
                      && (expected.RuleId is null || actual.RuleId == expected.RuleId);
        return new(expected, actual, matches);
    }

    // ---- Part 2: the shield and the evidence verifier ------------------------------------------------------------------

    private (ShieldBench Shield, VerifierBench Quotes) Documents(IReadOnlyList<SyntheticPdf> pdfs, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        var rows = new List<ShieldRow>();
        var scans = new List<(SyntheticPdf Pdf, ShieldResult Result)>();
        var hidden = Concealments.ToDictionary(c => c, _ => 0);
        var detected = Concealments.ToDictionary(c => c, _ => 0);

        for (var i = 0; i < pdfs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var pdf = pdfs[i];
            var how = pdf.Hidden.Aggregate(Concealment.None, (all, h) => all | h.How);
            ShieldResult result;
            try
            {
                result = shield.Scan(pdf.Bytes);
            }
            catch (Exception e)
            {
                rows.Add(new(i, pdf.ClientCode, how, ShieldVerdict.NotScanned, HiddenReason.None, e.GetType().Name));
                foreach (var c in Concealments) hidden[c] += pdf.Hidden.Count(h => h.How.HasFlag(c));
                continue;
            }
            scans.Add((pdf, result));
            rows.Add(new(i, pdf.ClientCode, how, result.Verdict, result.Hidden.Aggregate(HiddenReason.None, (all, f) => all | Reason(f)), null));

            foreach (var truth in pdf.Hidden)
            {
                var found = result.Hidden.Where(f => f.Page == truth.Page).Aggregate(HiddenReason.None, (all, f) => all | Reason(f));
                foreach (var c in Concealments.Where(c => truth.How.HasFlag(c)))
                {
                    hidden[c]++;
                    if (found.HasFlag(ReasonFor(c))) detected[c]++;
                }
            }
        }

        var clean = rows.Count(r => r.Hidden == Concealment.None);
        var shieldBench = new ShieldBench(
            new SourceNote(EvaluationSource.Real,
                $"The registered shield ({shield.GetType().Name}) on {CountEn(pdfs.Count, "generated PDF", "generated PDFs")}, as it is; the truth is how each was generated.",
                $"Scutul înregistrat ({shield.GetType().Name}) pe {CountRo(pdfs.Count, "PDF generat", "PDF-uri generate")}, așa cum e; adevărul e felul în care a fost generat fiecare."),
            rows, [.. Concealments.Select(c => new ConcealmentTally(c, hidden[c], detected[c]))], clean, rows.Count(r => r.FalseAlarm),
            Stopwatch.GetElapsedTime(start));

        var quotesStart = Stopwatch.GetTimestamp();
        var quotes = new VerifierBench(
            new SourceNote(EvaluationSource.Real,
                $"{VerifierBench.Name} ({verifier.GetType().Name}), not the model's: fixed quotes on the PDFs the shield read.",
                $"{VerifierBench.NameRo} ({verifier.GetType().Name}), nu a modelului: citate fixe pe PDF-urile citite de scut."),
            Tally(scans.SelectMany(s => s.Pdf.MockQuotes.Select(q => (q.Text, (int?)q.Page, s.Result)))),
            Tally(scans.SelectMany(s => s.Pdf.Hidden.Select(h => (h.Text, (int?)h.Page, s.Result)))),
            Tally(scans.SelectMany(s => InventedFor(s.Pdf).Select(q => (q, (int?)null, s.Result)))),
            Stopwatch.GetElapsedTime(quotesStart));
        return (shieldBench, quotes);
    }

    private QuoteTally Tally(IEnumerable<(string Quote, int? Page, ShieldResult Document)> quotes)
    {
        var checks = quotes.Select(q => verifier.Verify(q.Quote, q.Document, q.Page)).ToList();
        return new(checks.Count, checks.Count(c => c.Verified), checks.Count(c => !c.Verified && c.FoundOnlyInHiddenText));
    }

    /// <summary>
    /// The fixed invented quotes, plus each true quote with its last digit changed (a near miss), minus any the document's
    /// generated truth contains: such a quote is true, and rejecting it would be the verifier's error.
    /// </summary>
    public static IEnumerable<string> InventedFor(SyntheticPdf pdf)
    {
        var truth = pdf.MockQuotes.Select(q => q.Text).Concat(pdf.ExpectedText).Concat(pdf.Hidden.Select(h => h.Text))
            .Select(EvidenceVerifier.Normalize).ToList();
        return InventedQuotes.Concat(pdf.MockQuotes.Select(q => NearMiss(q.Text)).OfType<string>())
            .Where(q => !truth.Any(t => t.Contains(EvidenceVerifier.Normalize(q), StringComparison.Ordinal)));
    }

    private static string? NearMiss(string quote)
    {
        var i = quote.AsSpan().LastIndexOfAnyInRange('0', '9');
        return i < 0 ? null : string.Concat(quote.AsSpan(0, i), ((quote[i] - '0' + 1) % 10).ToString(CultureInfo.InvariantCulture), quote.AsSpan(i + 1));
    }

    private static HiddenReason Reason(HiddenFragment fragment) =>
        Enum.TryParse<HiddenReason>(fragment.Reason, out var reason) ? reason : HiddenReason.None;

    private static HiddenReason ReasonFor(Concealment how) => how switch
    {
        Concealment.White => HiddenReason.WhiteText,
        Concealment.Tiny => HiddenReason.TinyFont,
        Concealment.OffPage => HiddenReason.OffPage,
        Concealment.Invisible => HiddenReason.Invisible,
        _ => HiddenReason.None,
    };

    // ---- Part 2: extraction and context signals ------------------------------------------------------------------------

    private static ExtractionReport Extraction() => new(
        new SourceNote(EvaluationSource.Unavailable,
            "There is no live run of R4's extraction yet. It is measured only from a real, live run, never from the mock.",
            "Nu există încă o rulare live a extragerii lui R4. Se măsoară doar dintr-o rulare reală, live, niciodată din mock."),
        null, null);

    private SignalReport Signals(IReadOnlyList<GoldenCase> golden, IReadOnlyList<SyntheticSnapshotCase> synthetic)
    {
        var takesSignals = CanCarry(engine.GetType(), typeof(ContextSignal));
        // A payslip is generated only for the foreign-salary trap: the employer is the signal, in the document only.
        IReadOnlyList<string> cases =
        [
            .. golden.Where(c => c.Documents.Any(d => d.Quotes.Any(q => q.IsSignal))).Select(c => c.Id),
            .. synthetic.Where(s => s.Source.After.Documents.Any(d => d.Kind == SyntheticDocumentKind.Payslip)).Select(s => s.Code),
        ];
        return takesSignals
            ? new(new SourceNote(EvaluationSource.Unavailable,
                    "The rule engine can receive a context signal, so \"no signal changed a decision\" no longer holds by construction.",
                    "Motorul de reguli poate primi un semnal contextual, deci „niciun semnal nu a schimbat o decizie” nu mai e adevărat prin construcție."),
                true, cases, null)
            : new(new SourceNote(EvaluationSource.Real,
                    "0 by construction, checked on this run: no input of the rule engine can carry a context signal.",
                    "0 prin construcție, verificat la această rulare: nicio intrare a motorului de reguli nu poate purta un semnal contextual."),
                false, cases, 0);
    }

    /// <summary>Whether the engine's methods or constructor take <paramref name="signal"/>, directly or inside an app type.</summary>
    private static bool CanCarry(Type engineType, Type signal)
    {
        var app = typeof(EvaluationService).Assembly;
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>(typeof(ICrsRuleEngine).GetMethods().Concat<MethodBase>(engineType.GetConstructors())
            .SelectMany(m => m.GetParameters()).Select(p => p.ParameterType));
        while (pending.TryDequeue(out var type))
        {
            if (!seen.Add(type)) continue;
            if (signal.IsAssignableFrom(type)) return true;
            if (type.HasElementType) pending.Enqueue(type.GetElementType()!);
            foreach (var argument in type.GenericTypeArguments) pending.Enqueue(argument);
            if (type.Assembly == app)
                foreach (var property in type.GetProperties()) pending.Enqueue(property.PropertyType);
        }
        return false;
    }

    // ---- Part 3: operational efficiency --------------------------------------------------------------------------------

    private EfficiencyReport Efficiency()
    {
        var agent = new SourceNote(EvaluationSource.Unavailable,
            "No way to measure the agent's time per case yet: agent runs are not linked to cases.",
            "Încă nu există o cale de a măsura timpul agentului pe caz: rulările agentului nu sunt legate de cazuri.");

        var raw = configuration?[ManualMinutesKey];
        if (string.IsNullOrWhiteSpace(raw))
            return new(agent, null, new SourceNote(EvaluationSource.Unavailable,
                $"Awaiting the Raiffeisen mentor's answer ({ManualMinutesKey} is empty).",
                $"În așteptarea răspunsului mentorului Raiffeisen ({ManualMinutesKey} e gol)."), null);
        // A decimal point only: "22,5" is not read as 225.
        if (!decimal.TryParse(raw.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var minutes) || minutes <= 0)
            return new(agent, null, new SourceNote(EvaluationSource.Unavailable,
                $"{ManualMinutesKey} is not a positive number of minutes.",
                $"{ManualMinutesKey} nu e un număr pozitiv de minute."), null);
        return new(agent, null, new SourceNote(EvaluationSource.Real,
            $"The Raiffeisen mentor's estimate of the manual time per case ({ManualMinutesKey}).",
            $"Estimarea mentorului Raiffeisen pentru timpul manual pe caz ({ManualMinutesKey})."), minutes);
    }
}

public static class EvaluationSetup
{
    public static IServiceCollection AddEvaluation(this IServiceCollection services) =>
        services.AddScoped<IEvaluationService, EvaluationService>();
}
