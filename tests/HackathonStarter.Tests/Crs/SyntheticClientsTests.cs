using System.Globalization;
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
    public void Expected_decision_and_rules_come_from_the_scenario_template()
    {
        Assert.Equal(SyntheticClients.Templates.Count, SyntheticClients.Templates.Select(t => t.Id).Distinct().Count());
        Assert.Equal(SyntheticClients.Templates.Count, SyntheticClients.Templates.Select(t => t.Label).Distinct().Count());
        Assert.Contains(SyntheticClients.Templates, t => t.Label == "capcană: ordin din cont de depozit");

        foreach (var c in SyntheticClients.All)
        {
            var (decision, prefix, jurisdictions) = c.Scenario.Group switch
            {
                ScenarioGroup.Nothing => (ScenarioDecision.NoCase, "", new[] { "RO" }),
                ScenarioGroup.Trap => (ScenarioDecision.NoCase, "", Reportable),
                ScenarioGroup.Crs => (ScenarioDecision.Clarification, "CRS-IND-", Reportable),
                _ => (ScenarioDecision.FatcaAlert, "FATCA-IND-", new[] { "US" }),
            };
            Assert.Equal(decision, c.Expected);
            Assert.Equal(c.Scenario.Label, c.Label);
            Assert.Contains(c.Jurisdiction, jurisdictions);
            Assert.Equal(c.Scenario.Hits, c.ExpectedHits.Select(h => h.RuleId));
            Assert.All(c.ExpectedHits.Concat(c.ExpectedCleared), r =>
                Assert.Equal(r.RuleId.StartsWith("GEN-", StringComparison.Ordinal) ? null : c.Jurisdiction, r.Jurisdiction));

            if (decision == ScenarioDecision.NoCase) Assert.Empty(c.ExpectedHits);
            else Assert.All(c.ExpectedHits, h => Assert.StartsWith(prefix, h.RuleId));

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
    public void Every_scenario_has_its_expected_state_in_this_test()
    {
        Assert.Equal(SyntheticClients.Templates.Select(t => t.Id).Order(StringComparer.Ordinal), AfterChange.Keys.Order(StringComparer.Ordinal));
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
