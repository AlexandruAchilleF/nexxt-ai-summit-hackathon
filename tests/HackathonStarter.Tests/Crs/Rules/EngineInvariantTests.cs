using System.Text.Json;
using HackathonStarter.Features.Rules;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.Rules.Subjects;

namespace HackathonStarter.Tests.Crs.Rules;

/// <summary>Non-discrimination, determinism and the contract with R2 / R4.</summary>
public class EngineInvariantTests
{
    private static readonly CrsRuleEngine Engine = new();

    public static TheoryData<string> Nationalities() => ["DE", "IT", "FR", "AT", "MD", "UA", "GB", "CN", "IN", "TR"];

    [Theory]
    [MemberData(nameof(Nationalities))]
    public void A_non_us_citizenship_never_triggers_anything(string citizenship)
    {
        var o = Engine.Evaluate(RoResident(), RoResident().WithCitizenships("RO", citizenship), new RuleChange("DocumentUploaded"));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Empty(o.Indicia);
    }

    [Theory]
    [MemberData(nameof(Nationalities))]
    public void A_foreign_place_of_birth_other_than_the_us_never_triggers_anything(string country)
    {
        Assert.Equal(Decision.NoCase, Engine.Evaluate(RoResident(), RoResident().BornIn(country), new RuleChange("DocumentUploaded")).Decision);
    }

    [Fact]
    public void The_same_input_always_gives_the_same_outcome()
    {
        var c = EngineCases.Get("4");
        var first = JsonSerializer.Serialize(Engine.Evaluate(c.Before, c.After, c.Change));
        for (var i = 0; i < 5; i++) Assert.Equal(first, JsonSerializer.Serialize(Engine.Evaluate(c.Before, c.After, c.Change)));
    }

    [Fact]
    public void The_engine_never_returns_a_shield_alert()
    {
        foreach (var c in EngineCases.All)
            Assert.NotEqual(Decision.ShieldAlert, Engine.Evaluate(c.Before, c.After, c.Change).Decision);
    }

    [Fact]
    public void The_engine_does_not_mutate_its_input()
    {
        var c = EngineCases.Get("5b");
        var before = JsonSerializer.Serialize(c.After);
        Engine.Evaluate(c.Before, c.After, c.Change);
        Assert.Equal(before, JsonSerializer.Serialize(c.After));
    }

    [Fact]
    public void Outcome_round_trips_through_json_for_storage()
    {
        var c = EngineCases.Get("4");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);
        var json = JsonSerializer.Serialize(outcome);
        var back = JsonSerializer.Deserialize<RuleOutcome>(json)!;

        Assert.Equal(outcome.Decision, back.Decision);
        Assert.Equal(outcome.Indicia, back.Indicia);
        Assert.Equal(outcome.Explanation, back.Explanation);
        Assert.Contains("\"CaseRegime\"", json);
        Assert.DoesNotContain("\"Uncovered\"", json);
    }

    [Fact]
    public void Add_rules_registers_the_engine_and_keeps_r3s_tin_check()
    {
        var provider = new ServiceCollection().AddSingleton<ITinFormatCheck, FakeTinFormat>().AddRules().BuildServiceProvider();
        var engine = provider.GetRequiredService<CrsRuleEngine>();
        Assert.Same(engine, provider.GetRequiredService<CrsRuleEngine>());
        Assert.IsType<FakeTinFormat>(provider.GetRequiredService<ITinFormatCheck>());
        Assert.Equal(RuleCatalog.All, engine.Catalog);
    }

    [Fact]
    public void Rules_code_reads_no_clock_and_no_infrastructure()
    {
        var folder = Path.Combine(RepoRoot(), "src", "HackathonStarter", "Features", "Rules");
        var forbidden = new[] { "DateTime.Now", "DateTime.UtcNow", "DateTime.Today", "TimeProvider", "DbContext", "HttpClient", "IChatClient" };
        foreach (var file in Directory.GetFiles(folder, "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.All(forbidden, word => Assert.DoesNotContain(word, text));
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

public class RuleOutcomeSummaryTests
{
    [Fact]
    public void Summary_splits_open_and_covered_indicia_and_cites_the_law()
    {
        var c = EngineCases.Get("4");
        var summary = new CrsRuleEngine().Evaluate(c.Before, c.After, c.Change).Summarize();

        Assert.Equal(Decision.Clarification, summary.Decision);
        Assert.Equal(2, summary.OpenIndicia.Count);
        Assert.Empty(summary.CoveredIndicia);
        Assert.All(summary.OpenIndicia, f => Assert.StartsWith("CPF Anexa 1", f.LegalBasis));
        Assert.True(summary.RestsOnInterpretation); // Maria's account is new
    }

    [Fact]
    public void Summary_in_english_uses_the_english_texts()
    {
        var c = EngineCases.Get("13");
        var summary = new CrsRuleEngine().Evaluate(c.Before, c.After, c.Change).Summarize(en: true);

        Assert.StartsWith("No case", summary.Explanation);
        Assert.Contains("Romanian phone", Assert.Single(summary.Cleared).Explanation);
        Assert.Equal("Phone in a reportable jurisdiction, no Romanian phone", summary.Cleared[0].Title);
        Assert.False(summary.RestsOnInterpretation);
    }
}
