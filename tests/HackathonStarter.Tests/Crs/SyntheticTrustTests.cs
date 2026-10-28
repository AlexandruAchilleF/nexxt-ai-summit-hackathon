using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

/// <summary>R5's synthetic data against R3's checks: both sides were written from the same rules, so they must agree.</summary>
public class SyntheticTrustTests
{
    [Fact]
    public void Synthetic_tins_pass_the_format_rules_and_wrong_ones_fail()
    {
        var format = new TinFormat();
        var failures = new List<string>();
        foreach (var country in SyntheticData.TinCountries)
            for (var seed = 1; seed <= 200; seed++)
            {
                var tin = new SyntheticData(seed).Tin(country);
                if (format.Check(country, tin).Status != TinFormatStatus.Valid) failures.Add($"should be valid: {country} {tin}");
                foreach (var flaw in Enum.GetValues<TinFlaw>())
                {
                    var wrong = new SyntheticData(seed).WrongTin(country, flaw);
                    if (format.Check(country, wrong).Status != TinFormatStatus.Invalid) failures.Add($"should be invalid: {country} {flaw} {wrong}");
                }
            }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    [Theory]
    [MemberData(nameof(Pdfs))]
    public void Golden_pdf_goes_through_the_shield_and_the_verifier(string fileName)
    {
        var pdf = SyntheticPdfs.Get(fileName);
        var verifier = new EvidenceVerifier();

        var result = new HiddenTextShield().Scan(pdf.Bytes);

        Assert.Equal(pdf.Hidden.Count > 0 ? ShieldVerdict.HiddenTextRemoved : ShieldVerdict.Clean, result.Verdict);
        foreach (var quote in pdf.MockQuotes)
        {
            var check = verifier.Verify(quote.Text, result, quote.Page);
            Assert.True(check.Verified && check.Page == quote.Page, $"{quote.Text} (p. {quote.Page}): {check.Note}");
        }
        foreach (var text in pdf.ExpectedText) Assert.True(verifier.Verify(text, result).Verified, text);
        foreach (var hidden in pdf.Hidden)
        {
            Assert.Contains(result.Hidden, f => f.Page == hidden.Page && f.Reason == ((HiddenReason)(int)hidden.How).ToString());
            Assert.DoesNotContain(TextMatch.Normalize(hidden.Text[..30]), TextMatch.Normalize(result.ModelText()));
            Assert.True(verifier.Verify(hidden.Text[..40], result).FoundOnlyInHiddenText);
        }
    }

    public static TheoryData<string> Pdfs() => new(SyntheticPdfs.FileNames);
}
