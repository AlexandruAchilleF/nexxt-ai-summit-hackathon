namespace HackathonStarter.Tests;

/// <summary>No emoji anywhere (AGENTS.md): the app's sources and the team's own docs hold no pictographs.</summary>
public class NoEmojiTests
{
    private static readonly string[] SourceExtensions = [".cs", ".razor", ".css", ".js", ".html", ".json"];
    private static readonly string[] SkippedSourceFolders = ["wwwroot/lib/", "wwwroot/fonts/", "wwwroot/icons/"];

    // docs/ghid, docs/surse and docs/anaf-banci hold copies of external sources: not scanned.
    private static readonly string[] DocFolders = ["docs/agents", "docs/commands", "docs/roluri", "docs/ui"];
    private static readonly string[] DocExtensions = [".md", ".html"];

    [Fact]
    public void Sources_and_team_docs_hold_no_emoji()
    {
        var root = RepoRoot();
        var files = ScannedFiles(root).Select(f => RelativePath(root, f)).ToList();

        // Guards the file list: a wrong path would make this test pass without reading anything.
        Assert.Contains("src/HackathonStarter/Components/Layout/MainLayout.razor", files);
        Assert.Contains("src/HackathonStarter/wwwroot/app.css", files);
        Assert.Contains("AGENTS.md", files);
        Assert.Contains("docs/CLIENTS.md", files);
        Assert.Contains("docs/agents/tech-lead.md", files);
        Assert.Contains("docs/ui/macheta-caz.html", files);
        Assert.DoesNotContain(files, f => f.StartsWith("docs/ghid/", StringComparison.Ordinal));

        var offenders = files
            .Select(f => (File: f, Text: File.ReadAllText(Path.Combine(root, f))))
            .Where(x => !x.Text.Contains('\0')) // binary
            .SelectMany(x => x.Text.Split('\n').Select((line, i) => (x.File, Line: i + 1, Text: line)))
            .Select(x => (x.File, x.Line, x.Text, Pictograph: FirstPictograph(x.Text)))
            .Where(x => x.Pictograph is not null)
            .Select(x => $"{x.File}:{x.Line}: U+{x.Pictograph:X4} in \"{x.Text.Trim()}\"")
            .ToList();

        Assert.True(offenders.Count == 0, "No emoji or pictographs (AGENTS.md), write plain text instead:\n" + string.Join("\n", offenders));
    }

    // Escapes, so this file holds no pictographs either.
    [Theory]
    [InlineData("<span class=\"dismiss\">\U0001F5D9</span>", 0x1F5D9)]
    [InlineData("console.log(\"\U0001F527\", e.step.tool);", 0x1F527)]
    [InlineData("<span>— \u2922 \u2715</span>", 0x2715)]
    [InlineData("\u26A0\uFE0F A key used from browser code", 0x26A0)]
    [InlineData("/// \u2B50 THE FILE TO EDIT FIRST", 0x2B50)]
    [InlineData("\u2600 sun", 0x2600)]
    [InlineData("check \u27BF", 0x27BF)]
    [InlineData("\u2BFF", 0x2BFF)]
    [InlineData("\U0001F000 tile", 0x1F000)]
    [InlineData("\U0001FAFF", 0x1FAFF)]
    [InlineData("1\uFE0F", 0xFE0F)]
    public void The_scan_finds_emoji_pictographs_and_the_emoji_selector(string line, int expected) =>
        Assert.Equal(expected, FirstPictograph(line));

    [Theory]
    [InlineData("<span class=\"dismiss\">×</span>")] // multiplication sign
    [InlineData("— \u2922 × ▸ → ← ↑ ↓")]
    [InlineData("„fără termen legal în ghid (p. 38)” · … – ≥ ≤")]
    [InlineData("\u25FF \u2800 \u2C00 \U0001EFFF \U0001FB00 \uFE0E")] // just outside the ranges
    public void The_scan_allows_plain_text_symbols(string line) =>
        Assert.Null(FirstPictograph(line));

    /// <summary>The first code point in U+1F000-U+1FAFF, U+2600-U+27BF, U+2B00-U+2BFF or U+FE0F, or null.</summary>
    private static int? FirstPictograph(string line)
    {
        foreach (var rune in line.EnumerateRunes())
        {
            var c = rune.Value;
            if (c is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF or >= 0x2B00 and <= 0x2BFF or 0xFE0F) return c;
        }
        return null;
    }

    private static IEnumerable<string> ScannedFiles(string root)
    {
        var sources = Directory.EnumerateFiles(Path.Combine(root, "src"), "*", SearchOption.AllDirectories)
            .Where(f => SourceExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !IsBuildOutput(f) && !SkippedSourceFolders.Any(RelativePath(root, f).Contains));
        var topDocs = new[] { "README.md", "AGENTS.md" }.Select(f => Path.Combine(root, f))
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.TopDirectoryOnly));
        var docFolders = DocFolders
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*", SearchOption.AllDirectories))
            .Where(f => DocExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        return sources.Concat(topDocs).Concat(docFolders);
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

    private static string RelativePath(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
