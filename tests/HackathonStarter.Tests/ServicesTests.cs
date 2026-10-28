using HackathonStarter.Agents;
using HackathonStarter.Services;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Tests;

public class PiiVaultTests
{
    [Theory]
    [InlineData("1900515400013", true)]   // valid checksum
    [InlineData("1900515400014", false)]  // wrong checksum
    [InlineData("0900515400013", false)]  // can't start with 0
    public void Cnp_checksum(string cnp, bool valid) => Assert.Equal(valid, PiiVault.IsValidCnp(cnp));

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111 1111 1111 1112", false)]
    public void Card_luhn(string card, bool valid) => Assert.Equal(valid, PiiVault.IsLuhnValid(card));

    [Fact]
    public void Masks_each_kind_and_restores_it()
    {
        var vault = new PiiVault();
        const string text = "CNP 1900515400013, IBAN RO49 AAAA 1B31 0075 9384 0000, card 4111 1111 1111 1111, ion@x.ro, tel 0722 123 456.";

        var masked = vault.Mask(text);

        Assert.Equal("CNP [CNP_1], IBAN [IBAN_1], card [CARD_1], [EMAIL_1], tel [PHONE_1].", masked);
        Assert.Equal(5, vault.Count);
        Assert.Equal(text, vault.Unmask(masked));
    }

    [Theory]
    [InlineData("Pay RO49AAAA1B31007593840000 and done", "Pay [IBAN_1] and done")]
    [InlineData("Is RO49 AAAA 1B31 0075 9384 0000 valid?", "Is [IBAN_1] valid?")]
    [InlineData("iban ro49aaaa1b31007593840000.", "iban [IBAN_1].")]
    public void Iban_followed_by_words_is_masked_exactly(string text, string expected) =>
        Assert.Equal(expected, new PiiVault().Mask(text));

    [Fact]
    public void Same_value_gets_the_same_placeholder_and_random_numbers_are_kept()
    {
        var vault = new PiiVault();
        var masked = vault.Mask("ion@x.ro wrote to ion@x.ro about order 1234567890123 and 42 EUR");

        Assert.Equal("[EMAIL_1] wrote to [EMAIL_1] about order 1234567890123 and 42 EUR", masked); // invalid CNP checksum → kept
        Assert.Equal(1, vault.Count);
    }
}

public class DocumentSearchTests
{
    [Fact]
    public void Finds_the_relevant_passage_in_a_long_document()
    {
        var filler = string.Join(" ", Enumerable.Repeat("General terms and conditions apply to all products.", 80));
        var doc = new AgentFile("1", "contract.pdf", filler + " The monthly income of the applicant is 6,500 RON. " + filler, 3);

        var results = DocumentSearch.Search([doc], "monthly income applicant");

        Assert.NotEmpty(results);
        Assert.Contains("6,500 RON", results[0].Text);
    }

    [Fact]
    public void Images_are_not_searched_as_text()
    {
        var image = DocumentText.FromImage([1, 2, 3], "income.png");
        Assert.Empty(DocumentSearch.Search([image], "income image"));
    }

    [Fact]
    public void Search_ignores_case_and_diacritics()
    {
        var doc = new AgentFile("1", "a.txt", "Rata lunară este de 1.200 lei.", 1);
        Assert.NotEmpty(DocumentSearch.Search([doc], "RATA LUNARA"));
        Assert.Empty(DocumentSearch.Search([doc], "ipoteca"));
    }
}

public class TextMatchTests
{
    [Theory]
    [InlineData("Arhivează elementul", "arhiv", true)]
    [InlineData("Câte elemente avem?", "cate", true)]
    [InlineData("Monthly LOAN payment", "loan", true)]
    [InlineData("Hello", "loan", false)]
    public void Contains_any_ignores_case_and_diacritics(string text, string keyword, bool expected) =>
        Assert.Equal(expected, TextMatch.ContainsAny(text, keyword));

    [Fact]
    public void Similarity_between_questions()
    {
        Assert.Equal(1, Recordings.Similarity("Which items are active?", "which items are ACTIVE"));
        Assert.True(Recordings.Similarity("Which items are active?", "Loan payment for 20000?") < 0.2);
    }
}

public class RateLimiterTests
{
    [Fact]
    public void Limits_per_user_per_minute_and_per_day()
    {
        var limiter = new AgentRateLimiter(Options.Create(new SecurityOptions { AgentRunsPerMinute = 2, DailyLiveRunBudget = 3 }), TimeProvider.System);

        Assert.True(limiter.TryAcquire("a", out _));
        Assert.True(limiter.TryAcquire("a", out _));
        Assert.False(limiter.TryAcquire("a", out var perMinute));
        Assert.Contains("per minute", perMinute);

        Assert.True(limiter.TryAcquire("b", out _));   // other user: own minute window, 3rd of the day
        Assert.False(limiter.TryAcquire("b", out var daily));
        Assert.Contains("daily", daily);
    }
}
