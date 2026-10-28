using System.Reflection;
using System.Text.RegularExpressions;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// CRS code reads time from <see cref="ISimClock"/> only (docs/PLAN.md 9.1, 16.2): the demo moves "+85 days" and every
/// deadline, case transition and audit entry must move with it. The app-wide TimeProvider stays the system clock.
/// </summary>
public class ClockTests
{
    /// <summary>The CRS modules (AGENTS.md, "Time"). Folder name = last namespace segment.</summary>
    private static readonly string[] CrsModules =
        ["Clients", "Changes", "Rules", "Deadlines", "Cases", "Shield", "Tin", "Audit", "Evaluation", "Sentinel"];

    private static readonly Regex SystemClock = new(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b");

    /// <summary>Taking the app-wide clock without a constructor parameter: resolving it from DI, or the static system clock.</summary>
    private static readonly Regex DirectTimeProvider = new(
        @"\bGet(Required)?Service<\s*(global::)?(System\.)?TimeProvider\s*>" +
        @"|\bGet(Required)?Service\(\s*typeof\(\s*(global::)?(System\.)?TimeProvider\s*\)\s*\)" +
        @"|\bTimeProvider\.System\b");

    /// <summary>The one line allowed to touch TimeProvider: AddDeadlines builds SimClock over the app's clock (fallback: the system clock).</summary>
    private const string SimClockWiringFile = "src/HackathonStarter/Features/Deadlines/DeadlineCalculator.cs";

    [Fact]
    public void Crs_types_never_take_TimeProvider()
    {
        var crsTypes = typeof(SimClock).Assembly.GetTypes()
            .Where(t => CrsModules.Any(m => InNamespace(t, $"HackathonStarter.Features.{m}")))
            .Where(t => t != typeof(SimClock)) // SimClock wraps the real clock: the one place allowed to take it
            .ToList();

        // Guards the filter: an empty list would make this test pass without checking anything.
        Assert.Contains(typeof(CaseService), crsTypes);
        Assert.Contains(typeof(CrsRuleEngine), crsTypes);
        Assert.Contains(typeof(DeadlineCalculator), crsTypes);

        var offenders = TimeProviderParameters(crsTypes);
        Assert.True(offenders.Count == 0,
            "CRS code must inject ISimClock, not TimeProvider (docs/PLAN.md 9.1):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void The_constructor_check_finds_a_TimeProvider_parameter()
    {
        Assert.Equal(new[] { "TakesTimeProvider(System.TimeProvider clock)" },
            TimeProviderParameters([typeof(TakesTimeProvider), typeof(TakesSimClock)]));
    }

    [Fact]
    public void Crs_sources_never_read_the_system_clock()
    {
        var root = RepoRoot();
        var features = Path.Combine(root, "src", "HackathonStarter", "Features");
        var files = new List<string>();
        foreach (var module in CrsModules)
        {
            var dir = Path.Combine(features, module);
            Assert.True(Directory.Exists(dir), $"CRS module folder not found: {Path.GetRelativePath(root, dir)} (update CrsModules)");
            files.AddRange(Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")));
        }
        Assert.NotEmpty(files);
        Assert.Contains(SimClockWiringFile, files.Select(f => RelativePath(root, f))); // keeps the exception pointing at a real file

        var offenders = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: RelativePath(root, f), Line: i + 1, Text: line)))
            .Where(x => ReadsSystemClock(x.File, x.Text))
            .Select(x => $"{x.File}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "CRS code reads time from ISimClock (inject it; use clock.UtcNow), never the system clock or TimeProvider:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData("var now = DateTime.UtcNow;", true)]
    [InlineData("var now = System.DateTime.Now;", true)]
    [InlineData("var today = DateOnly.FromDateTime(DateTime.Today);", true)]
    [InlineData("var at = DateTimeOffset.UtcNow;", true)]
    [InlineData("var at = DateTimeOffset.Now.UtcDateTime;", true)]
    [InlineData("var clock = sp.GetRequiredService<TimeProvider>();", true)]
    [InlineData("var clock = sp.GetService<TimeProvider>() ?? fallback;", true)]
    [InlineData("var clock = sp.GetRequiredService<System.TimeProvider>();", true)]
    [InlineData("var clock = (TimeProvider)sp.GetRequiredService(typeof(TimeProvider));", true)]
    [InlineData("var now = TimeProvider.System.GetUtcNow();", true)]
    [InlineData("var now = clock.UtcNow;", false)]
    [InlineData("var now = clock.GetUtcNow();", false)]
    [InlineData("var clock = sp.GetRequiredService<ISimClock>();", false)]
    [InlineData("    // never DateTime.Now here", false)]
    [InlineData("    /// <summary>Not DateTime.UtcNow.</summary>", false)]
    [InlineData("    /// <summary>Over the app's <see cref=\"TimeProvider\"/>, never GetService<TimeProvider>.</summary>", false)]
    [InlineData("var kind = DateTimeKind.Utc;", false)]
    public void The_source_check_flags_system_clock_reads_outside_comments(string line, bool flagged)
    {
        Assert.Equal(flagged, ReadsSystemClock("src/HackathonStarter/Features/Cases/CaseService.cs", line));
    }

    [Theory]
    [InlineData(SimClockWiringFile, "services.AddSingleton<ISimClock>(sp => new SimClock(sp.GetService<TimeProvider>() ?? TimeProvider.System));", false)]
    [InlineData("src/HackathonStarter/Features/Cases/CaseService.cs", "services.AddSingleton<ISimClock>(sp => new SimClock(sp.GetService<TimeProvider>() ?? TimeProvider.System));", true)]
    [InlineData(SimClockWiringFile, "var real = sp.GetRequiredService<TimeProvider>();", true)]
    [InlineData(SimClockWiringFile, "var now = TimeProvider.System.GetUtcNow();", true)]
    [InlineData(SimClockWiringFile, "var clock = new SimClock(TimeProvider.System); var now = DateTime.UtcNow;", true)] // the exception never covers DateTime
    public void Only_the_SimClock_wiring_in_AddDeadlines_may_touch_TimeProvider(string file, string line, bool flagged)
    {
        Assert.Equal(flagged, ReadsSystemClock(file, line));
    }

    /// <param name="file">Path relative to the repository root, with '/'.</param>
    private static bool ReadsSystemClock(string file, string line)
    {
        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) return false;
        if (SystemClock.IsMatch(line)) return true;
        if (!DirectTimeProvider.IsMatch(line)) return false;
        return !(file == SimClockWiringFile && line.Contains("new SimClock(", StringComparison.Ordinal));
    }

    private static string RelativePath(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static bool InNamespace(Type type, string ns) =>
        type.Namespace is { } n && (n == ns || n.StartsWith(ns + ".", StringComparison.Ordinal));

    private static List<string> TimeProviderParameters(IEnumerable<Type> types) =>
        types
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters().Where(p => p.ParameterType == typeof(TimeProvider))
                    .Select(p => $"{t.Name}({p.ParameterType.FullName} {p.Name})")))
            .Distinct()
            .ToList();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private sealed class TakesTimeProvider(TimeProvider clock)
    {
        public DateTimeOffset Now => clock.GetUtcNow();
    }

    private sealed class TakesSimClock(ISimClock clock)
    {
        public DateTime Now => clock.UtcNow;
    }
}
