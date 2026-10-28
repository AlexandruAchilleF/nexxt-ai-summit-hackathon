using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Features.Checks;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

/// <summary>What a check stores and shows about an answer must not carry personal data in clear (PLAN 13.1).</summary>
public class CheckPrivacyTests
{
    [Fact]
    public void An_excerpt_that_starts_inside_a_phone_shows_none_of_its_digits()
    {
        // "indiciu" is at index 51, so the 40-character window starts right after the "+": cut first, the rest of the
        // number ("49 89 4521 7730") no longer looks like a phone and would be stored and shown in clear.
        const string answer = "Telefonul +49 89 4521 7730 al Mariei este și el un indiciu.";
        var engine = new CheckEngine([new RuleCitedCheck()]);

        var result = Assert.Single(engine.Evaluate(new CheckSubject(answer, "Ce indicii are Maria?"), ["rule-cited"]).Results);

        Assert.Equal(CheckOutcome.Fail, result.Outcome);
        var excerpt = Assert.Single(result.Findings).Excerpt;
        Assert.NotNull(excerpt);
        Assert.DoesNotContain("4521", excerpt);
        Assert.DoesNotContain("7730", excerpt);
        Assert.Contains("indiciu", excerpt);
    }

    [Fact]
    public void A_phone_the_model_put_in_a_tool_input_is_not_allowed_in_the_answer()
    {
        // Only what the bank's tools returned may be repeated: the model writes the inputs itself.
        const string toolResults = """[{"tool":"dial","input":{"phone":"+49 89 4521 7730"},"output":{"dialled":true}}]""";
        var subject = new CheckSubject("Am sunat la +49 89 4521 7730.", "Sună-l pe client", new Dictionary<string, string> { ["tool-results"] = toolResults });

        var result = Assert.Single(new CheckEngine([new PiiLeakCheck()]).Evaluate(subject, ["pii-leak"]).Results);

        Assert.Equal(CheckOutcome.Fail, result.Outcome);
    }

    [Fact]
    public void A_phone_a_tool_returned_may_be_quoted()
    {
        const string toolResults = """[{"tool":"recordEvidence","input":{"documentId":7},"output":{"quote":"Telefon: +49 89 4521 7730","verified":true}}]""";
        var subject = new CheckSubject("Dovada pentru CRS-IND-PHONE (p. 29): „Telefon: +49 89 4521 7730”.", "Pregătește cazul Mariei",
            new Dictionary<string, string> { ["tool-results"] = toolResults });

        var result = Assert.Single(new CheckEngine([new PiiLeakCheck()]).Evaluate(subject, ["pii-leak"]).Results);

        Assert.Equal(CheckOutcome.Pass, result.Outcome);
    }

    [Theory]
    [InlineData("Tel: +49\u00A089\u00A04521\u00A07730.")] // non-breaking spaces, as PDFs and Word often write
    [InlineData("Tel: +33\u202F6\u202F12\u202F34\u202F56\u202F78.")] // narrow non-breaking spaces (French)
    [InlineData("Tel: +49  89 4521 7730.")] // a double space
    public void Phones_with_unusual_spaces_are_masked_and_restored(string text)
    {
        var vault = new PiiVault();

        var masked = vault.Mask(text);

        Assert.Equal("Tel: [PHONE_1].", masked);
        Assert.Equal(text, vault.Unmask(masked));
    }

    [Theory]
    [InlineData("q=CNP+1900515400013", "[CNP-1]")]
    [InlineData("Suma +1900515400013.50", "[CNP-1]")]
    [InlineData("Suma +378282246310005.50", "[CARD-1]")]
    public void Digits_after_an_escaped_plus_in_saved_json_are_still_masked(string text, string token)
    {
        var json = JsonSerializer.Serialize(new { value = text }, RunStore.Json);

        var stored = PiiVault.ForStorage().Mask(json);

        Assert.Contains(token, stored);
        Assert.DoesNotContain("1900515400013", stored);
        Assert.DoesNotContain("378282246310005", stored);
    }

    [Fact]
    public void A_phone_a_tool_only_echoed_from_its_input_is_not_allowed_in_the_answer()
    {
        const string toolResults = """[{"tool":"dial","input":{"phone":"+49 89 4521 7730"},"output":{"dialled":"+49 89 4521 7730"}}]""";
        var subject = new CheckSubject("Am sunat la +49 89 4521 7730.", "Sună-l pe client", new Dictionary<string, string> { ["tool-results"] = toolResults });

        Assert.Equal(CheckOutcome.Fail, Assert.Single(new CheckEngine([new PiiLeakCheck()]).Evaluate(subject, ["pii-leak"]).Results).Outcome);
    }

    [Theory]
    [InlineData("Telefonul +49 89\n4521 7730 al Mariei este și el un indiciu.")] // a phone broken by a line break
    [InlineData("Telefonul \\u002B49 89 4521 7730 al Mariei este și el un indiciu.")] // the JSON escape of "+"
    public void An_excerpt_never_shows_the_digits_of_a_phone(string answer)
    {
        var result = Assert.Single(new CheckEngine([new RuleCitedCheck()]).Evaluate(new CheckSubject(answer, "x"), ["rule-cited"]).Results);

        var excerpt = Assert.Single(result.Findings).Excerpt;
        Assert.NotNull(excerpt);
        Assert.DoesNotContain("4521", excerpt);
        Assert.DoesNotContain("7730", excerpt);
    }

    [Theory]
    [InlineData("0049 89 4521 7730", "+49 89 4521 7730")] // "00" in, "+" out
    [InlineData("0722 123 456", "+40 722 123 456")] // Romanian national in, international out
    public void A_phone_echoed_in_another_form_is_still_an_echo(string input, string output)
    {
        var toolResults = $$$"""[{"tool":"lookup","input":{"phone":"{{{input}}}"},"output":{"phone":"{{{output}}}"}}]""";
        var subject = new CheckSubject($"Numărul este {output}.", "Verifică numărul", new Dictionary<string, string> { ["tool-results"] = toolResults });

        Assert.Equal(CheckOutcome.Fail, Assert.Single(new CheckEngine([new PiiLeakCheck()]).Evaluate(subject, ["pii-leak"]).Results).Outcome);
    }

    [Fact]
    public void The_maria_evidence_quote_passes_although_record_evidence_echoes_it()
    {
        // readDocument returned the phone (bank data); recordEvidence got it back as the quote and echoed it.
        const string toolResults = """[{"tool":"readDocument","input":{"documentId":7},"output":{"text":"Telefon: +49 89 4521 7730"}},""" +
            """{"tool":"recordEvidence","input":{"quote":"Telefon: +49 89 4521 7730"},"output":{"quote":"Telefon: +49 89 4521 7730","verified":true}}]""";
        var subject = new CheckSubject("Dovada: „Telefon: +49 89 4521 7730”.", "Pregătește cazul", new Dictionary<string, string> { ["tool-results"] = toolResults });

        Assert.Equal(CheckOutcome.Pass, Assert.Single(new CheckEngine([new PiiLeakCheck()]).Evaluate(subject, ["pii-leak"]).Results).Outcome);
    }
}
