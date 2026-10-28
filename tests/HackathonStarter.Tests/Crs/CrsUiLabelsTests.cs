using HackathonStarter.Components.Crs;
using HackathonStarter.Features.Rules;
using HackathonStarter.Tests.Crs.Sentinel;

namespace HackathonStarter.Tests.Crs;

/// <summary>Labels the case page and the audit log show on camera: no raw IDs, no "NEVERIFICAT".</summary>
public class CrsUiLabelsTests
{
    [Theory]
    [InlineData("en-US", "agent:crs-sentinel for user:admin", "CRS Sentinel for Ana Admin")]
    [InlineData("ro-RO", "agent:crs-sentinel for user:admin", "CRS Sentinel pentru Ana Admin")]
    [InlineData("en-US", "agent:crs-sentinel", "CRS Sentinel")]
    [InlineData("en-US", "system:seed", "Seed history")]
    [InlineData("en-US", "system:watcher", "System")]
    [InlineData("en-US", "user:admin", "Ana Admin")]
    [InlineData("en-US", "admin", "Ana Admin")]
    public void Actors_read_as_people_and_systems(string culture, string actor, string expected)
    {
        using var _ = new UiLanguage(culture);
        Assert.Equal(expected, CrsUi.Actor(actor));
    }

    [Theory]
    [InlineData("en-US", "not in the guide: our reading")]
    [InlineData("ro-RO", "nu e în ghid: interpretarea noastră")]
    public void A_design_interpretation_never_shows_as_a_guide_page(string culture, string expected)
    {
        using var _ = new UiLanguage(culture);
        Assert.Equal(expected, CrsUi.Page(RuleCatalog.NotVerified));
        Assert.DoesNotContain(RuleCatalog.NotVerified, CrsUi.Page(RuleCatalog.NotVerified));
    }

    [Fact]
    public void The_us_reads_as_a_country_in_english()
    {
        using var _ = new UiLanguage("en-US");
        Assert.Equal("United States (US)", CrsUi.Country("US"));
        Assert.Equal("Germany (DE)", CrsUi.Country("DE"));
    }
}
