using System.Globalization;
using System.IO.Enumeration;
using System.Text;
using System.Text.RegularExpressions;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Exports;
using HackathonStarter.Features.Items;
using HackathonStarter.Services;

namespace HackathonStarter.Tests;

/// <summary>Two UI languages: every English key written as T("...") has a Romanian translation.</summary>
public class LangTests
{
    [Fact]
    public void Every_literal_key_in_the_code_is_translated()
    {
        var src = Path.Combine(RepoRoot(), "src", "HackathonStarter");
        var files = Directory.EnumerateFiles(src, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !IsRoDictionary(f) && !f.EndsWith("Lang.cs")); // Lang.cs: examples in comments

        var missing = files
            .SelectMany(f => Keys(File.ReadAllText(f)).Select(k => (File: Path.GetFileName(f), Key: k)))
            .Where(x => !Lang.Translations.ContainsKey(x.Key))
            .Select(x => $"{x.File}: {x.Key}")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "Add these to Services/Lang.Ro*.cs (the file of your module):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Romanian_keys_are_not_defined_twice()
    {
        // ["key"] = ... inside BuildRo() (Lang.Ro.cs) and ro["key"] = ...; in Lang.Ro.<Module>.cs. A key may contain \".
        var entry = new Regex(@"^\s*(?:ro)?\[""((?:[^""\\]|\\.)*)""\]\s*=", RegexOptions.Multiline);
        var services = Path.Combine(RepoRoot(), "src", "HackathonStarter", "Services");
        var keys = Directory.EnumerateFiles(services, "*.cs")
            .Where(IsRoDictionary)
            .SelectMany(f => entry.Matches(File.ReadAllText(f))
                .Select(m => (File: Path.GetFileName(f), Key: Regex.Replace(m.Groups[1].Value, @"\\(.)", "$1"))))
            .ToList();

        var twice = keys
            .GroupBy(x => x.Key)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.File))}")
            .ToList();
        Assert.True(twice.Count == 0, "Defined more than once (the last one wins), keep one:\n" + string.Join("\n", twice));

        // Guards the parser: a key written in another form would make this test pass without checking it.
        var unread = Lang.Translations.Keys.Except(keys.Select(x => x.Key)).ToList();
        Assert.True(unread.Count == 0, "Write these as [\"key\"] = or ro[\"key\"] = in Services/Lang.Ro*.cs:\n" + string.Join("\n", unread));
    }

    [Fact]
    public void Keys_that_come_from_data_are_translated()
    {
        var keys = SiteConfig.Nav.Select(n => n.Label)
            .Concat(SiteConfig.Nav.Select(n => n.Group).OfType<string>())
            .Concat(SiteConfig.Users.Select(u => u.Role))
            .Concat(Enum.GetNames<RunStatus>()).Concat(Enum.GetNames<RunSource>()).Concat(Enum.GetNames<ExportStatus>())
            .Concat(Enum.GetNames<ItemStatus>()).Concat(Enum.GetNames<ItemPriority>())
            .Append(RunStore.InterruptedError);

        Assert.All(keys, k => Assert.True(Lang.Translations.ContainsKey(k), $"No Romanian for \"{k}\""));
    }

    [Fact]
    public void T_follows_the_current_ui_culture_and_keeps_unknown_keys()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ro-RO");
            Assert.Equal("Aprobă", Lang.T("Approve"));
            Assert.Equal("Aprobată de Ana", Lang.T("Approved by {0}", "Ana"));
            Assert.Equal("Not a key", Lang.T("Not a key"));
            Assert.Equal("ro", Lang.Pick("ro", "en"));

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
            Assert.Equal("Approve", Lang.T("Approve"));
            Assert.Equal("en", Lang.Pick("ro", "en"));
        }
        finally { CultureInfo.CurrentUICulture = before; }
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        static string Holes(string s) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Order());
        Assert.Empty(Lang.Translations.Where(t => Holes(t.Key) != Holes(t.Value)).Select(t => t.Key));
    }

    /// <summary>The string literals passed to every T(...) call: T("a"), T(x ? "a" : "b"), T("a {0}", arg).</summary>
    private static IEnumerable<string> Keys(string code)
    {
        for (var i = code.IndexOf("T(", StringComparison.Ordinal); i >= 0; i = code.IndexOf("T(", i + 2, StringComparison.Ordinal))
        {
            if (i > 0 && (char.IsLetterOrDigit(code[i - 1]) || code[i - 1] == '_')) continue; // GetT(, ToT(...
            var depth = 0;
            for (var j = i + 1; j < code.Length; j++)
            {
                var c = code[j];
                if (c == '(') depth++;
                else if (c == ')' && --depth == 0) break;
                else if (c == '"' && depth == 1 && code[j - 1] != '$' && code[j - 1] != '@')
                {
                    var text = new StringBuilder();
                    for (j++; j < code.Length && code[j] != '"'; j++)
                    {
                        if (code[j] == '\\') j++;
                        text.Append(code[j]);
                    }
                    if (text.Length > 0) yield return text.ToString(); // "" in a condition, not a key
                }
            }
        }
    }

    /// <summary>Lang.Ro.cs and the module files Lang.Ro.&lt;Module&gt;.cs.</summary>
    private static bool IsRoDictionary(string path) => FileSystemName.MatchesSimpleExpression("Lang.Ro*.cs", Path.GetFileName(path));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
