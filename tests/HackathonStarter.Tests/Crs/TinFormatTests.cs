using HackathonStarter.Features.Tin;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

public class TinFormatTests
{
    private static readonly TinFormat Format = new();

    // Checked against TIN on Europa on 28.10.2026 (US is not covered there: IRS/SSA ranges).
    [Theory]
    [InlineData("DE", "86095174223")]
    [InlineData("DE", "11213456783")]
    [InlineData("IT", "DBRCST90A41Z129T")]
    [InlineData("AT", "123456782")]
    [InlineData("AT", "931234561")]
    [InlineData("ES", "12345678Z")]
    [InlineData("ES", "X1234567L")]
    [InlineData("ES", "Y7654321G")]
    [InlineData("FR", "3023217600053")]
    [InlineData("FR", "1234567890066")]
    [InlineData("RO", "2910608400319")]
    [InlineData("US", "521-48-3907")]
    [InlineData("US", "912-70-1234")]
    public void Valid_formats(string country, string tin) =>
        Assert.Equal(TinFormatStatus.Valid, Format.Check(country, tin).Status);

    [Theory]
    [InlineData("DE", "86095174224")] // check digit
    [InlineData("DE", "12345678903")] // no repeated digit
    [InlineData("DE", "06095174223")] // starts with 0
    [InlineData("DE", "11123456786")] // the same digit three times in a row
    [InlineData("DE", "11223456785")] // two digits repeated
    [InlineData("IT", "DBRCST90A41Z129X")]
    [InlineData("AT", "931234562")]
    [InlineData("ES", "12345678A")]
    [InlineData("FR", "3023217600054")]
    [InlineData("FR", "4023217600053")]
    [InlineData("RO", "2910608400318")]
    [InlineData("US", "000-12-3456")]
    [InlineData("US", "666-12-3456")]
    [InlineData("US", "923-45-6789")]
    public void Invalid_formats(string country, string tin) =>
        Assert.Equal(TinFormatStatus.Invalid, Format.Check(country, tin).Status);

    [Fact]
    public void Golden_case_2_italian_tin_without_check_character_is_invalid() =>
        Assert.Equal(TinFormatStatus.Invalid, Format.Check("IT", "DBRCST90A41Z129").Status);

    [Fact]
    public void Golden_case_3_missing_austrian_tin_is_missing() =>
        Assert.Equal(TinFormatStatus.Missing, Format.Check("AT", null!).Status);

    [Fact]
    public void Human_formatting_is_normalized()
    {
        var german = Format.Check("de", "860 951 742 23");

        Assert.Equal(TinFormatStatus.Valid, german.Status);
        Assert.Equal("86095174223", german.Normalized);
        Assert.True(Format.Check("IT", "dbrcst90a41z129t").FormatValid);
        Assert.Equal("521483907", Format.Check("US", "521-48-3907").Normalized);
    }

    [Fact]
    public void Unknown_country_is_unsupported_not_invalid()
    {
        var polish = Format.Check("PL", "44051401359");

        Assert.Equal(TinFormatStatus.Unsupported, polish.Status);
        Assert.Equal(TinCheckSource.NotChecked, polish.Source);
    }

    [Fact]
    public void Empty_tin_is_missing()
    {
        Assert.Equal(TinFormatStatus.Missing, Format.Check("DE", "").Status);
        Assert.Equal(TinFormatStatus.Missing, Format.Check("DE", "   ").Status);
    }

    [Fact]
    public void Rule_sources_never_say_tin_valid()
    {
        Assert.Equal(["AT", "DE", "ES", "FR", "IT", "RO", "US"], TinFormat.Countries);
        foreach (var country in TinFormat.Countries)
        {
            var check = Format.Check(country, "X");
            Assert.Equal(TinCheckSource.LocalRules, check.Source);
            Assert.NotEmpty(check.Note);
            Assert.DoesNotContain("tin valid", TextMatch.Normalize(check.Note));
        }
    }

    [Fact]
    public void Only_known_countries_say_whether_they_issue_a_tin_to_everyone()
    {
        Assert.False(Format.IssuesTinToAllResidents("at")); // OECD sheet AT, GOLDEN-CASES case 3
        Assert.Null(Format.IssuesTinToAllResidents("DE"));
    }
}
