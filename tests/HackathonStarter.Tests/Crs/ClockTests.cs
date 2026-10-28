using System.Reflection;
using System.Text.RegularExpressions;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// CRS code reads time from <see cref="SimClock"/> only (docs/PLAN.md 9.1, 16.2): the demo moves "+85 days" and every
/// deadline, case transition and audit entry must move with it. The app-wide TimeProvider stays the system clock.
/// </summary>
public class ClockTests
{
    /// <summary>The CRS modules (AGENTS.md, "Time"). Folder name = last namespace segment.</summary>
    private static readonly string[] CrsModules =
        ["Clients", "Changes", "Rules", "Deadlines", "Cases", "Shield", "Tin", "Audit", "Evaluation", "Sentinel"];

    private static readonly Regex SystemClock = new(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b");

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
            "CRS code must inject SimClock, not TimeProvider (docs/PLAN.md 9.1):\n" + string.Join("\n", offenders));
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

        var offenders = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(x => ReadsSystemClock(x.Text))
            .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "CRS code reads time from SimClock (inject it; use clock.UtcNow), never the system clock:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData("var now = DateTime.UtcNow;", true)]
    [InlineData("var now = System.DateTime.Now;", true)]
    [InlineData("var today = DateOnly.FromDateTime(DateTime.Today);", true)]
    [InlineData("var at = DateTimeOffset.UtcNow;", true)]
    [InlineData("var at = DateTimeOffset.Now.UtcDateTime;", true)]
    [InlineData("var now = clock.UtcNow;", false)]
    [InlineData("var now = clock.GetUtcNow();", false)]
    [InlineData("    // never DateTime.Now here", false)]
    [InlineData("    /// <summary>Not DateTime.UtcNow.</summary>", false)]
    [InlineData("var kind = DateTimeKind.Utc;", false)]
    public void The_source_check_flags_system_clock_reads_outside_comments(string line, bool flagged)
    {
        Assert.Equal(flagged, ReadsSystemClock(line));
    }

    private static bool ReadsSystemClock(string line)
    {
        var trimmed = line.TrimStart();
        return !trimmed.StartsWith("//", StringComparison.Ordinal) && SystemClock.IsMatch(line);
    }

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

    private sealed class TakesSimClock(SimClock clock)
    {
        public DateTime Now => clock.UtcNow;
    }
}
