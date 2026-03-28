using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Collector;
using NewsAgent.Models;
using NSubstitute;

namespace NewsAgent.Tests.Collector;

public class RssCollectorTests
{
    private readonly IOptions<DigestConfig> _config;
    private readonly ILogger<RssCollector> _logger;

    public RssCollectorTests()
    {
        var config = new DigestConfig
        {
            Sources = new SourcesConfig { RssFeeds = [] },
            Llm = new LlmConfig { Provider = "openai", Model = "gpt-4o-mini" }
        };
        _config = Options.Create(config);
        _logger = Substitute.For<ILogger<RssCollector>>();
    }

    [Fact]
    public async Task CollectAsync_WithNoFeeds_ReturnsEmptyList()
    {
        var collector = new RssCollector(_config, _logger);

        var result = await collector.CollectAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CollectAsync_WithInvalidFeedUrl_ReturnsEmptyAndLogs()
    {
        var config = new DigestConfig
        {
            Sources = new SourcesConfig
            {
                RssFeeds = [new RssFeedConfig { Url = "https://invalid.example.com/nonexistent-feed.xml", Name = "Invalid" }]
            },
            Llm = new LlmConfig { Provider = "openai", Model = "gpt-4o-mini" }
        };
        var collector = new RssCollector(Options.Create(config), _logger);

        var result = await collector.CollectAsync();

        Assert.Empty(result);
    }
}
