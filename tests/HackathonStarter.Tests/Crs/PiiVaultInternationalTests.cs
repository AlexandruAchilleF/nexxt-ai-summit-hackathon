using System.Text.Json;
using System.Text.RegularExpressions;
using HackathonStarter.Agents;
using HackathonStarter.Features.Checks;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;
using HackathonStarter.Tests.Support;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Tests.Crs;

/// <summary>Foreign phone numbers ("+49 …", "0049 …") are masked like Romanian ones (PLAN 13.1: the model sees [PHONE_1]).</summary>
public class PiiVaultInternationalTests
{
    [Theory]
    [InlineData("+49 89 4521 7730")]
    [InlineData("+498945217730")]
    [InlineData("0049 89 4521 7730")]
    [InlineData("+1 (212) 555-0147")]
    [InlineData("+43 1 5134567")]
    [InlineData("+4915144201187")]
    [InlineData("+44 (0)20 7946 0958")]
    [InlineData("+33 6 12 34 56 78")]
    [InlineData("+1.212.555.0147")]
    [InlineData("+40 21 312 3456")] // Romanian landline the Romanian pattern doesn't cover
    [InlineData("+380 44 123 45 67")]
    [InlineData("+43 664 123 45 67")]
    [InlineData("+49 (0)151 1234 5678")]
    [InlineData("+49 89 4521 7730-1234")] // with an extension
    [InlineData("+49-89-4521-7730-1234")]
    public void International_numbers_are_masked_and_restored_exactly(string phone)
    {
        var vault = new PiiVault();
        var text = $"Tel: {phone}.";

        var masked = vault.Mask(text);

        Assert.Equal("Tel: [PHONE_1].", masked);
        Assert.Equal(text, vault.Unmask(masked));
        Assert.Equal(new PiiItem("PHONE", phone, "[PHONE_1]"), Assert.Single(vault.Items));
        Assert.Equal("Tel: [PHONE-1].", PiiVault.ForStorage().Mask(text));
    }

    public static TheoryData<string> PhoneCountries => [.. SyntheticData.PhoneCountries];

    [Theory]
    [MemberData(nameof(PhoneCountries))]
    public void Synthetic_e164_numbers_are_phones_not_cards_or_cnps(string country)
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var phone = new SyntheticData(seed).PhoneE164(country);
            var item = Assert.Single(PiiVault.Detect($"Tel {phone}, {country}"));
            Assert.Equal(new PiiItem("PHONE", phone, "[PHONE_1]"), item);
        }
    }

    [Theory]
    [InlineData("+40721384516")]   // golden cases (GOLDEN-CASES.md)
    [InlineData("+498945217730")]
    [InlineData("+4915144201187")] // its digits pass the CNP checksum
    [InlineData("+13125550147")]
    public void Golden_phones_are_masked_as_phones(string phone) =>
        Assert.Equal("Telefon: [PHONE-1]", PiiVault.Redact($"Telefon: {phone}"));

    [Fact]
    public void Json_with_foreign_phones_round_trips()
    {
        const string json = """{"home":"+49 89 4521 7730","mobile":"+498945217730","office":"0049 89 4521 7730","us":"+1 (212) 555-0147","at":"+43 1 5134567"}""";
        var vault = new PiiVault();

        var masked = vault.Mask(json);

        Assert.Equal("""{"home":"[PHONE_1]","mobile":"[PHONE_2]","office":"[PHONE_3]","us":"[PHONE_4]","at":"[PHONE_5]"}""", masked);
        Assert.Equal(json, vault.Unmask(masked));
    }

    [Fact]
    public void Json_escaped_plus_is_masked_with_the_same_placeholder_as_the_plain_number()
    {
        // RunStore.Json writes "+" as its JSON escape: that is how tool-call arguments are saved.
        var args = JsonSerializer.Serialize(new { phone = "+49 89 4521 7730", fax = "+1 (212) 555-0147" }, RunStore.Json);
        Assert.DoesNotContain("+", args);

        var storage = PiiVault.ForStorage();
        Assert.Equal("Call [PHONE-1]", storage.Mask("Call +49 89 4521 7730"));
        Assert.Equal("""{"phone":"[PHONE-1]","fax":"[PHONE-2]"}""", storage.Mask(args));

        var live = new PiiVault();
        var restored = JsonSerializer.Deserialize<Dictionary<string, string>>(live.Unmask(live.Mask(args)), RunStore.Json)!;
        Assert.Equal("+49 89 4521 7730", restored["phone"]);
        Assert.Equal("+1 (212) 555-0147", restored["fax"]);
    }

    [Theory]
    [InlineData("86095174223")]              // German TIN
    [InlineData("2910608400319")]            // CNP
    [InlineData("RO19RZBR0000060021458391")] // IBAN
    [InlineData("RO19 RZBR 0000 0600 2145 8391")]
    [InlineData("DE89 3704 0044 0532 0130 01")] // IBAN with a wrong checksum: not a phone either
    [InlineData("4111 1111 1111 1111")]      // card
    [InlineData("2026-10-28")]
    [InlineData("28.10.2026")]
    [InlineData("1.234,56")]
    [InlineData("+1.234.567")]               // signed amounts
    [InlineData("+12.345.678,90")]
    [InlineData("+1234567,89")]
    [InlineData("+1234567.89")]
    [InlineData("+12345.67")]
    [InlineData("10:30")]
    [InlineData("2026-10-28T10:00:00+02:00")]
    [InlineData("UTC+02:00")]
    [InlineData("0.0049894521773")]          // decimal
    [InlineData("0,0049894521773")]
    [InlineData("+49 89")]                   // too short
    [InlineData("+4989452177301234567")]     // too long
    [InlineData("REF+498945217730")]         // part of a longer token
    [InlineData("+498945217730X")]
    [InlineData("ID-0049894521773")]
    [InlineData("M\\u00fcller")]             // JSON escape of a letter
    public void Other_numbers_are_not_phones(string text) =>
        Assert.DoesNotContain(PiiVault.Detect($"Value {text} here"), i => i.Type == "PHONE");

    [Fact]
    public void Iban_and_card_win_over_a_phone_on_the_same_digits()
    {
        var vault = new PiiVault();

        var masked = vault.Mask("Pay DE89 3704 0044 0532 0130 00 by card 0049 8945 2177 3001, or call 0044 20 7946 0958.");

        Assert.Equal("Pay [IBAN_1] by card [CARD_1], or call [PHONE_1].", masked);
    }

    [Fact]
    public void A_number_after_the_phone_is_not_swallowed()
    {
        var vault = new PiiVault();

        Assert.Equal("Sunat la [PHONE_1] 2026", vault.Mask("Sunat la +49 89 4521 7730 2026"));
        Assert.Equal("[PHONE_2] 2026-10-28", vault.Mask("+43 1 5134567 2026-10-28"));
        Assert.Equal("+43 1 5134567", vault.Unmask("[PHONE_2]"));
    }

    [Theory]
    [InlineData("Call +1 212 555 0147 9am-5pm EST", "Call [PHONE_1] 9am-5pm EST")]
    [InlineData("Tel: +44 20 7946 0958 2nd floor reception", "Tel: [PHONE_1] 2nd floor reception")]
    [InlineData("Tel +49 89 4521 7730 1x pe zi", "Tel [PHONE_1] 1x pe zi")]
    [InlineData("1,Maria Ionescu,+498945217730,1985", "1,Maria Ionescu,[PHONE_1],1985")] // CSV rows
    [InlineData("Maria,+49 89 4521 7730,1985", "Maria,[PHONE_1],1985")]
    [InlineData("1,Maria Ionescu,+498945217730,35,DE", "1,Maria Ionescu,[PHONE_1],35,DE")] // not "+1234567,89"
    [InlineData("Maria,+49 89 4521 7730,35", "Maria,[PHONE_1],35")]
    [InlineData("1,+498945217730,35", "1,[PHONE_1],35")]
    [InlineData("Sună la +49 89 4521 7730 2 ori", "Sună la [PHONE_1] 2 ori")]
    [InlineData("+43 1 5134567 10 ani", "[PHONE_1] 10 ani")]
    [InlineData("Suna la +43 1 5134567 2026", "Suna la [PHONE_1] 2026")]
    [InlineData("Tel: +49 89 4521 7730 0043 1 5134567", "Tel: [PHONE_1] [PHONE_2]")] // two numbers side by side
    [InlineData("+43 1 5134567 0049 89 4521 7730", "[PHONE_1] [PHONE_2]")]
    [InlineData("1001,0049 89 4521 7730", "1001,[PHONE_1]")] // a "00" number after a numeric column
    [InlineData("1,Maria,35,0049 89 4521 7730", "1,Maria,35,[PHONE_1]")]
    [InlineData("Maria,+1 (212) 555-0147,35", "Maria,[PHONE_1],35")]
    [InlineData("Ref +12 0049 89 4521 7730", "Ref +12 [PHONE_1]")] // a "00" number after a short one
    [InlineData("+1 0049 89 4521 7730", "+1 [PHONE_1]")]
    public void Only_the_phone_is_masked_not_what_follows(string text, string expected)
    {
        var vault = new PiiVault();

        var masked = vault.Mask(text);

        Assert.Equal(expected, masked);
        Assert.Equal(text, vault.Unmask(masked));
        Assert.Equal(expected.Replace('_', '-'), PiiVault.ForStorage().Mask(text));
    }

    [Theory]
    [InlineData("Card +4111111111111111", "Card +[CARD_1]", "CARD", "4111111111111111")]
    [InlineData("Card +4111 1111 1111 1111", "Card +[CARD_1]", "CARD", "4111 1111 1111 1111")]
    [InlineData("Card +4111111111111111 2026", "Card +[CARD_1] 2026", "CARD", "4111111111111111")]
    [InlineData("Card+4111111111111111", "Card+[CARD_1]", "CARD", "4111111111111111")]
    [InlineData("card+number+4111111111111111", "card+number+[CARD_1]", "CARD", "4111111111111111")]
    [InlineData("q=CNP+1900515400013", "q=CNP+[CNP_1]", "CNP", "1900515400013")] // URL-encoded query in a log
    [InlineData("Suma +1900515400013.50", "Suma +[CNP_1].50", "CNP", "1900515400013")] // an amount to the phone pattern
    [InlineData("Suma +378282246310005.50", "Suma +[CARD_1].50", "CARD", "378282246310005")]
    public void A_card_after_a_plus_is_still_a_card(string text, string expected, string type, string value)
    {
        Assert.Equal(expected, new PiiVault().Mask(text));
        Assert.Equal(new PiiItem(type, value, $"[{type}_1]"), Assert.Single(PiiVault.Detect(text)));
        Assert.Equal(expected.Replace('_', '-'), PiiVault.ForStorage().Mask(text));
    }

    [Fact]
    public void Romanian_and_foreign_numbers_side_by_side()
    {
        var vault = new PiiVault();
        const string text = "RO: 0722 123 456, +40 745 210 963; DE: +49 89 4521 7730; US: +1 (312) 555-0147.";

        var masked = vault.Mask(text);

        Assert.Equal("RO: [PHONE_1], [PHONE_2]; DE: [PHONE_3]; US: [PHONE_4].", masked);
        Assert.Equal(text, vault.Unmask(masked));
    }

    [Fact]
    public void A_long_run_of_numbers_is_masked_without_copying_the_rest_for_each_number()
    {
        // One regex match covers the whole run, and each number in it is masked before the rest is scanned again. A copy
        // of the rest per number made memory grow with the square of the run: 80,000 characters took 3.2 GB.
        var text = string.Join(' ', Enumerable.Range(0, 3000).Select(i => $"0049.89.4521.{7000 + i % 1000}"));
        var before = GC.GetAllocatedBytesForCurrentThread();

        var masked = PiiVault.ForStorage().Mask(text);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16 * 1024 * 1024, $"Masking {text.Length:N0} characters allocated {allocated:N0} bytes.");
        Assert.StartsWith("[PHONE-1] [PHONE-2] ", masked);
        Assert.DoesNotMatch(@"\d", Regex.Replace(masked, @"\[PHONE-\d+\]", "")); // every number is masked
    }

    [Fact]
    public void Pii_leak_flags_a_foreign_phone_the_user_did_not_give()
    {
        var engine = new CheckEngine([new PiiLeakCheck()]);

        var report = engine.Evaluate(new CheckSubject("Clientul are telefonul +49 89 4521 7730.", "Spune-mi ceva despre client"), ["pii-leak"]);

        var result = Assert.Single(report.Results);
        Assert.Equal(CheckOutcome.Fail, result.Outcome);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("PHONE", finding.What);
        Assert.DoesNotContain("4521 7730", finding.Excerpt);
        Assert.Contains("[PHONE-1]", finding.Excerpt);

        var allowed = engine.Evaluate(new CheckSubject("Telefonul +49 89 4521 7730 este din DE.", "Al cui e +498945217730?"), ["pii-leak"]);
        Assert.Equal(CheckOutcome.Pass, allowed.Results[0].Outcome);

        var count = engine.Evaluate(new CheckSubject("Am sunat la +49 89 4521 7730 2 ori.", "Sună la +49 89 4521 7730"), ["pii-leak"]);
        Assert.Equal(CheckOutcome.Pass, count.Results[0].Outcome);

        var csv = engine.Evaluate(new CheckSubject("Clientul 1001,0049 89 4521 7730 are telefon in DE.", "Spune-mi ceva"), ["pii-leak"]);
        Assert.Equal(CheckOutcome.Fail, csv.Results[0].Outcome);

        var us = engine.Evaluate(new CheckSubject("Telefonul +1 (212) 555-0147 e din SUA.", "Maria,+1 (212) 555-0147,35"), ["pii-leak"]);
        Assert.Equal(CheckOutcome.Pass, us.Results[0].Outcome);
    }

    [Fact]
    public async Task The_model_never_sees_a_foreign_phone_and_the_saved_run_holds_none()
    {
        const string phone = "+49 89 4521 7730";
        var dial = new DialAgent();
        var model = new FakeChatClient().CallTool("dial", new { phone = "[PHONE_1]" }).Reply("Dialled [PHONE_1].");
        await using var host = await TestHost.CreateAsync(model, extraAgents: dial);

        var run = await host.RunAsync("dial", $"Call {phone}");
        var saved = (await host.Get<RunStore>().GetAsync(run.Id))!;

        Assert.Equal(new[] { phone }, dial.Received); // the tool got the real number
        Assert.DoesNotContain("4521 7730", model.AllUserText);
        var results = model.Calls.SelectMany(c => c).SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString() ?? "").ToList();
        Assert.NotEmpty(results); // the tool result goes to the model as a function result, not as text
        Assert.All(results, r => Assert.DoesNotContain("4521", r));
        Assert.Contains(results, r => r.Contains("[PHONE_1]"));
        Assert.Contains($"Dialled {phone}.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.Equal("Call [PHONE-1]", saved.Input);
        var everything = string.Join("\n", saved.Steps.Select(s => $"{s.Text} {s.Input} {s.Output} {s.Summary}"));
        Assert.DoesNotContain("4521", everything);
        Assert.Contains("""{"phone":"[PHONE-1]"}""", everything); // the saved tool-call arguments
    }

    private sealed class DialAgent : ToolAgent
    {
        public List<string> Received { get; } = [];
        public override string Name => "dial";
        public override string Label => "Dial";
        public override string Description => "Test agent that calls a phone number.";
        public override string SystemPrompt(ToolContext ctx) => "Dial when asked.";

        public override IEnumerable<AgentTool> Tools(ToolContext ctx)
        {
            yield return AgentTool.Create("dial", "Call a phone number.", (string phone) =>
            {
                Received.Add(phone);
                return new { dialled = phone, phoneCountry = "DE" };
            });
        }

        public override Task MockAsync(MockContext mock) => mock.SayAsync("Mock.");
    }
}
