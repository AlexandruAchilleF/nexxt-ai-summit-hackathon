using HackathonStarter.Services;
using HackathonStarter.Tests.Support;

namespace HackathonStarter.Tests.Crs;

public class BedrockProviderTests
{
    [Fact]
    public void Region_defaults_to_us_west_2_and_profiles_inherit_or_override_it()
    {
        var options = new AiOptions { Provider = "Bedrock", Model = "us.anthropic.claude-sonnet-5-5" };
        options.Profiles["extract"] = new AiProfile { Model = "us.anthropic.claude-haiku-5-5" };
        options.Profiles["eu"] = new AiProfile { Region = "eu-west-1" };

        var extract = options.ForProfile("extract");
        var eu = options.ForProfile("eu");

        Assert.Equal("us-west-2", options.BedrockRegion);
        Assert.Equal("Bedrock", extract.Provider);
        Assert.Equal("us.anthropic.claude-haiku-5-5", extract.Model);
        Assert.Equal("us-west-2", extract.BedrockRegion);
        Assert.Equal("eu-west-1", eu.BedrockRegion);
    }

    [Fact]
    public void Privacy_page_names_the_bedrock_regional_endpoint_as_an_external_provider()
    {
        var options = new AiOptions { Provider = "Bedrock", Model = "us.anthropic.claude-sonnet-5-5", Region = "us-west-2" };
        var ai = ChatClientProvider.Fixed(options, new FakeChatClient());

        var main = Assert.Single(DataFlows.Models(ai));

        Assert.Equal("bedrock-runtime.us-west-2.amazonaws.com", main.Host);
        Assert.False(main.OnOwnInfrastructure);
    }
}
