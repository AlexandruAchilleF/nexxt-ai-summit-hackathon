using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Sentinel;

namespace HackathonStarter.Tests.Crs.Sentinel;

public class PhonesInBankDataTests
{
    private static readonly Phone[] Maria = [new() { Number = "+498945217730", Country = "DE" }, new() { Number = "+40722123456", Country = "RO", IsCurrent = false }];

    [Fact]
    public void A_phone_written_with_spaces_in_the_document_matches_the_bank_phone_and_gets_its_country()
    {
        var found = SentinelTools.PhonesInBankData(["Angajat: Maria Popescu\nTelefon: +49 89 4521 7730\nAngajator: Müller Logistik GmbH"], Maria);

        var phone = Assert.Single(found);
        Assert.Equal("{ Text = +49 89 4521 7730, Country = DE, IsCurrent = True }", phone.ToString());
    }

    [Fact]
    public void A_phone_that_is_not_in_the_bank_data_is_not_listed()
    {
        Assert.Empty(SentinelTools.PhonesInBankData(["Telefon: +49 30 1234 5678"], Maria));
    }

    [Theory]
    [InlineData("+40 722 123 456")]
    [InlineData("0040722123456")]
    [InlineData("0722 123 456")]
    public void Romanian_numbers_match_in_every_common_form(string written)
    {
        Assert.Equal("40722123456", SentinelTools.PhoneDigits(written));
    }
}
