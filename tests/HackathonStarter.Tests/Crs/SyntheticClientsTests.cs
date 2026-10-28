using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HackathonStarter.Features.Finance;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

/// <summary>The 50 synthetic CRS clients: distribution, labels set at generation, determinism, synthetic identifiers.</summary>
public class SyntheticClientsTests
{
    private static readonly string[] Reportable = ["DE", "AT", "IT", "ES", "FR"];
    private static readonly string[] StateKeys = ["res", "mail", "phone", "order", "poa", "cit", "doc"];

    // What every client looks like before the change: the "standard RO resident" of GOLDEN-CASES.md.
    private const string Standard = "res=RO mail= phone=RO order= poa= cit=RO doc=";

    // After the change, as the scenario describes it ({J} = the scenario's jurisdiction). Built from the data, not the engine.
    private static readonly Dictionary<string, string> AfterChange = new()
    {
        ["domestic-move"] = "",
        ["domestic-phone-changed"] = "",
        ["domestic-phone-added"] = "phone=RO,RO",
        ["domestic-mailing"] = "mail=RO",
        ["domestic-order"] = "order=Depository>RO",
        ["domestic-poa"] = "poa=RO",
        ["crs-move"] = "res={J}",
        ["crs-move-phone"] = "res={J} phone={J}",
        ["crs-phone"] = "phone={J}",
        ["crs-custodial-order"] = "order=Custodial>{J}",
        ["crs-poa"] = "poa={J}",
        ["crs-mailing"] = "mail={J}",
        ["fatca-phone"] = "phone=RO,{J}",
        ["fatca-order"] = "order=Depository>{J}",
        ["fatca-citizenship"] = "cit=RO,{J} doc=Passport",
        ["trap-depository-order"] = "order=Depository>{J}",
        ["trap-foreign-phone"] = "phone=RO,{J}",
        ["trap-citizenship"] = "cit=RO,{J} doc=Passport",
        ["trap-foreign-salary"] = "doc=Payslip",
    };

    // What each scenario should produce, written again here from its source, not read from the template: RuleId@country
    // ({J} = the scenario's jurisdiction); a rule without "@" is a GEN-* rule, whose jurisdiction is not compared.
    private static readonly Dictionary<string, (ScenarioDecision Decision, string Hits, string Cleared, string Source)> Outcomes = new()
    {
        ["domestic-move"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "golden 15"),
        ["domestic-phone-changed"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "golden 1"),
        ["domestic-phone-added"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "PLAN 8.3 pas 2"),
        ["domestic-mailing"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "PLAN 8.3 pas 2"),
        ["domestic-order"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "PLAN 8.3 pas 2"),
        ["domestic-poa"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "PLAN 8.3 pas 2"),
        ["crs-move"] = (ScenarioDecision.Clarification, "CRS-IND-ADDR@{J}", "", "golden 16"),
        ["crs-move-phone"] = (ScenarioDecision.Clarification, "CRS-IND-ADDR@{J} CRS-IND-PHONE@{J}", "", "golden 4"),
        ["crs-phone"] = (ScenarioDecision.Clarification, "CRS-IND-PHONE@{J}", "", "golden 4, the phone alone"),
        ["crs-custodial-order"] = (ScenarioDecision.Clarification, "CRS-IND-SO@{J}", "", "golden 5b"),
        ["crs-poa"] = (ScenarioDecision.Clarification, "CRS-IND-POA@{J}", "", "golden 11"),
        ["crs-mailing"] = (ScenarioDecision.Clarification, "CRS-IND-MAIL@{J}", "", "PLAN 8.2 CRS-IND-MAIL, p. 29 lit. b"),
        ["fatca-phone"] = (ScenarioDecision.FatcaAlert, "FATCA-IND-PHONE@US", "CRS-IND-PHONE@US", "golden 14"),
        ["fatca-order"] = (ScenarioDecision.FatcaAlert, "FATCA-IND-SO@US", "CRS-IND-SO@US", "golden 5c"),
        ["fatca-citizenship"] = (ScenarioDecision.FatcaAlert, "FATCA-IND-CIT@US", "", "golden 7"),
        ["trap-depository-order"] = (ScenarioDecision.NoCase, "", "CRS-IND-SO@{J}", "golden 5a"),
        ["trap-foreign-phone"] = (ScenarioDecision.NoCase, "", "CRS-IND-PHONE@{J}", "golden 13"),
        ["trap-citizenship"] = (ScenarioDecision.NoCase, "", "", "PLAN 8.2, nationality is not a signal"),
        ["trap-foreign-salary"] = (ScenarioDecision.NoCase, "", "GEN-DOMESTIC", "golden 8"),
    };

    public static TheoryData<string> Scenarios => [.. AfterChange.Keys];

    [Theory]
    [InlineData(SyntheticClients.Seed)]
    [InlineData(1)]
    [InlineData(99)]
    public void Fifty_clients_30_nothing_7_crs_3_fatca_10_traps(int seed)
    {
        var cases = SyntheticClients.Generate(seed);

        Assert.Equal(50, cases.Count);
        Assert.Equal(30, cases.Count(c => c.Scenario.Group == ScenarioGroup.Nothing));
        Assert.Equal(7, cases.Count(c => c.Scenario.Group == ScenarioGroup.Crs));
        Assert.Equal(3, cases.Count(c => c.Scenario.Group == ScenarioGroup.Fatca));
        Assert.Equal(10, cases.Count(c => c.Scenario.Group == ScenarioGroup.Trap));
        Assert.All(SyntheticClients.Templates, t => Assert.Equal(t.Count, cases.Count(c => c.Scenario == t)));
        Assert.Equal(Enumerable.Range(1001, 50).Select(n => $"CL-{n}"), cases.Select(c => c.Code));
        Assert.All(cases, c => Assert.Equal(c.Before.Code, c.After.Code));
    }

    [Fact]
    public void Templates_expect_exactly_the_outcomes_of_this_test()
    {
        Assert.All(Outcomes.Values, o => Assert.False(string.IsNullOrWhiteSpace(o.Source)));
        Assert.Equal(
            Outcomes.OrderBy(o => o.Key, StringComparer.Ordinal).Select(o => Outcome(o.Key, o.Value.Decision, RuleIds(o.Value.Hits), RuleIds(o.Value.Cleared))),
            SyntheticClients.Templates.OrderBy(t => t.Id, StringComparer.Ordinal).Select(t => Outcome(t.Id, t.Expected, t.Hits, t.Cleared)));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Each_client_expects_the_rules_of_its_scenario_in_its_jurisdiction(string scenario)
    {
        var (decision, hits, cleared, _) = Outcomes[scenario];
        var cases = SyntheticClients.All.Where(c => c.Scenario.Id == scenario).ToList();

        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            Assert.Equal(decision, c.Expected);
            Assert.Equal(Rules(hits, c.Jurisdiction), Rules(c.ExpectedHits));
            Assert.Equal(Rules(cleared, c.Jurisdiction), Rules(c.ExpectedCleared));
        }
    }

    [Fact]
    public void Labels_and_jurisdictions_follow_the_scenario_group()
    {
        Assert.Equal(SyntheticClients.Templates.Count, SyntheticClients.Templates.Select(t => t.Id).Distinct().Count());
        Assert.Equal(SyntheticClients.Templates.Count, SyntheticClients.Templates.Select(t => t.Label).Distinct().Count());
        Assert.Contains(SyntheticClients.Templates, t => t.Label == "capcană: ordin din cont de depozit");

        foreach (var c in SyntheticClients.All)
        {
            string[] jurisdictions = c.Scenario.Group switch
            {
                ScenarioGroup.Nothing => ["RO"],
                ScenarioGroup.Fatca => ["US"],
                _ => Reportable,
            };
            Assert.Contains(c.Jurisdiction, jurisdictions);

            if (c.Scenario.Group == ScenarioGroup.Trap)
            {
                Assert.StartsWith("capcană: ", c.Label);
                Assert.StartsWith("trap: ", c.Scenario.LabelEn);
            }
            else Assert.False(string.IsNullOrWhiteSpace(c.Scenario.LabelEn));
        }

        // Every reportable jurisdiction shows up at least once among the CRS and trap clients.
        Assert.All(Reportable, j => Assert.Contains(SyntheticClients.All, c => c.Scenario.Group != ScenarioGroup.Nothing && c.Jurisdiction == j));
    }

    // p. 25 lit. b, p. 38: a new depository account is reviewed for FATCA only once its balance exceeds 50.000 $ at a year
    // end, so the account each FATCA scenario plays on holds more than that, without an exchange rate, by 31.12.2025.
    [Fact]
    public void Every_fatca_client_has_a_depository_account_over_the_threshold_by_the_end_of_2025()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            foreach (var c in SyntheticClients.Generate(seed).Where(c => c.Scenario.Group == ScenarioGroup.Fatca))
            {
                Assert.True(c.Before.Accounts.Any(OverFatcaThreshold), $"seed {seed} {c.Code} {c.Scenario.Id}");
                Assert.True(c.After.Accounts.Any(OverFatcaThreshold), $"seed {seed} {c.Code} {c.Scenario.Id}");
                if (c.Scenario.Id != "fatca-order") continue;

                var source = c.After.Accounts.Single(a => a.StandingOrders.Any(o => o.BeneficiaryCountry == "US"));
                Assert.Equal("Cont curent EUR", source.Product);
                Assert.True(OverFatcaThreshold(source), $"seed {seed} {c.Code} {source.OpenedOn} {source.Balance} {source.Currency}");
            }
        }
    }

    // The /metrics figures and the video come from these 50 clients: anything that reshuffles or changes them must fail here
    // and be announced, then the pins are updated.
    [Fact]
    public void Seed_2026_gives_exactly_the_clients_behind_the_metrics()
    {
        Assert.Equal(2026, SyntheticClients.Seed);
        Assert.Equal("CL-1001 Ion Tudor 5040810440768 domestic-phone-changed RO", Pin(SyntheticClients.All[0]));
        Assert.Equal("CL-1050 Cristian Gheorghe 1590621068722 domestic-poa RO", Pin(SyntheticClients.All[^1]));
        Assert.Equal("CL-1037 fatca-citizenship, CL-1038 fatca-order, CL-1044 fatca-phone",
            string.Join(", ", SyntheticClients.All.Where(c => c.Scenario.Group == ScenarioGroup.Fatca).Select(c => $"{c.Code} {c.Scenario.Id}")));
        Assert.Equal("231d7102661c70b6f5cd5be1b51f285995a905d17e3117a8f055667e5263c202", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(SyntheticClients.All)))));
    }

    [Fact]
    public void Same_seed_same_clients_in_any_culture_other_seed_other_clients()
    {
        var reference = InCulture(CultureInfo.InvariantCulture, () => Json(SyntheticClients.Generate()));

        Assert.Equal(reference, Json(SyntheticClients.All));
        Assert.Equal(reference, InCulture(CultureInfo.GetCultureInfo("ro-RO"), () => Json(SyntheticClients.Generate())));
        Assert.Equal(reference, InCulture(CultureInfo.GetCultureInfo("en-US"), () => Json(SyntheticClients.Generate())));
        Assert.NotEqual(reference, Json(SyntheticClients.Generate(SyntheticClients.Seed + 1)));
    }

    [Fact]
    public void Every_scenario_has_its_expected_state_and_outcome_in_this_test()
    {
        var ids = SyntheticClients.Templates.Select(t => t.Id).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(ids, AfterChange.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(ids, Outcomes.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Each_client_carries_the_facts_of_its_scenario(string scenario)
    {
        var cases = SyntheticClients.All.Where(c => c.Scenario.Id == scenario).ToList();

        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            Assert.Equal(Standard, State(c.Before));
            Assert.Equal(Expected(AfterChange[scenario], c.Jurisdiction), State(c.After));
            Assert.NotEqual(Json(c.Before), Json(c.After));
            if (scenario == "domestic-move")
                Assert.NotEqual(c.Before.Addresses.Single(a => a.IsCurrent).City, c.After.Addresses.Single(a => a.IsCurrent).City);
        }
    }

    [Fact]
    public void Identifiers_are_synthetic_with_a_valid_format_and_accounts_are_new()
    {
        foreach (var c in SyntheticClients.All)
        {
            var client = c.After;
            Assert.True(PiiVault.IsValidCnp(client.Cnp), client.Cnp);
            Assert.Equal(client.Cnp, client.SelfCertifications.Single().Residences.Single(r => r.Country == "RO").Tin);
            Assert.All(client.Phones, p => Assert.True(ForeignFormats.IsValidPhone(p.Country, p.Number), $"{c.Code} {p.Number}"));
            Assert.All(client.Phones.Where(p => p.Country == "US"), p => Assert.Matches(@"^\+1\d{3}55501\d{2}$", p.Number));

            foreach (var account in client.Accounts)
            {
                Assert.True(Iban.Validate(account.Iban).IsValid, account.Iban);
                Assert.True(account.OpenedOn > new DateOnly(2015, 12, 31), $"{c.Code} {account.OpenedOn}"); // new for CRS and FATCA (p. 20)
                Assert.True(account.OpenedOn >= client.BirthDate.AddYears(18), $"{c.Code} {account.OpenedOn}");
                foreach (var order in account.StandingOrders)
                {
                    if (order.RoutingNumber is { } aba)
                    {
                        Assert.Equal("US", order.BeneficiaryCountry);
                        Assert.True(ForeignFormats.IsValidAba(aba), aba);
                    }
                    else
                    {
                        var iban = Iban.Validate(order.BeneficiaryAccount);
                        Assert.True(iban.IsValid, order.BeneficiaryAccount);
                        Assert.Equal(order.BeneficiaryCountry, iban.Country);
                        Assert.True(ForeignFormats.HasValidNationalCheck(order.BeneficiaryAccount), order.BeneficiaryAccount);
                    }
                }
            }

            Assert.Equal(DateTimeKind.Utc, c.Change.ReceivedAt.Kind);
            Assert.InRange(c.Change.ReceivedAt, SyntheticClients.ReferenceTime.AddDays(-21), SyntheticClients.ReferenceTime);
        }
    }

    private static string State(SyntheticClient c)
    {
        var current = c.Addresses.Where(a => a.IsCurrent).ToList();
        return string.Join(' ',
            "res=" + List(current.Where(a => a.Kind == SyntheticAddressKind.Residence).Select(a => a.Country)),
            "mail=" + List(current.Where(a => a.Kind == SyntheticAddressKind.Mailing).Select(a => a.Country)),
            "phone=" + List(c.Phones.Where(p => p.IsCurrent).Select(p => p.Country)),
            "order=" + List(c.Accounts.SelectMany(a => a.StandingOrders.Where(o => o.IsActive).Select(o => $"{a.Type}>{o.BeneficiaryCountry}"))),
            "poa=" + List(c.PowersOfAttorney.Where(p => p.IsActive).Select(p => p.HolderCountry)),
            "cit=" + List(c.Citizenships),
            "doc=" + List(c.Documents.Select(d => d.Kind.ToString())));
    }

    private static string Expected(string changes, string jurisdiction)
    {
        var state = Standard.Split(' ').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        foreach (var part in changes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (key, value) = (part[..part.IndexOf('=')], part[(part.IndexOf('=') + 1)..]);
            state[key] = List(value.Replace("{J}", jurisdiction).Split(','));
        }
        return string.Join(' ', StateKeys.Select(k => $"{k}={state[k]}"));
    }

    private static string List(IEnumerable<string> items) => string.Join(',', items.Order(StringComparer.Ordinal));

    private static string Outcome(string id, ScenarioDecision decision, IEnumerable<string> hits, IEnumerable<string> cleared) =>
        $"{id}: {decision} hits={List(hits)} cleared={List(cleared)}";

    private static IEnumerable<string> RuleIds(string rules) => rules.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Split('@')[0]);

    private static string Rules(string rules, string jurisdiction) => List(rules.Replace("{J}", jurisdiction).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Rules(IEnumerable<ExpectedRule> rules) => List(rules.Select(r => r.Jurisdiction is null ? r.RuleId : $"{r.RuleId}@{r.Jurisdiction}"));

    private static bool OverFatcaThreshold(SyntheticAccount a) =>
        a.Type == SyntheticAccountType.Depository && a.OpenedOn <= new DateOnly(2025, 12, 31)
        && (a.Currency, a.Balance) is ("USD", >= 60_000m) or ("EUR", >= 75_000m);

    private static string Pin(SyntheticClientCase c) => $"{c.Code} {c.After.FullName} {c.After.Cnp} {c.Scenario.Id} {c.Jurisdiction}";

    private static string Json(IReadOnlyList<SyntheticClientCase> cases) => JsonSerializer.Serialize(cases);

    private static string Json(SyntheticClient client) => JsonSerializer.Serialize(client);

    private static T InCulture<T>(CultureInfo culture, Func<T> action)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        try { return action(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi); }
    }
}
