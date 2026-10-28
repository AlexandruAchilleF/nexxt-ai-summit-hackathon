using HackathonStarter.Features.Checks;

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
}
