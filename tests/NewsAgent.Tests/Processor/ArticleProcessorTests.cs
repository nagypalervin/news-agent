using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Models;
using NewsAgent.Processor;
using NSubstitute;

namespace NewsAgent.Tests.Processor;

public class ArticleProcessorTests
{
    private readonly ArticleProcessor _processor;

    public ArticleProcessorTests()
    {
        var config = new DigestConfig
        {
            Processor = new ProcessorConfig { MaxAgeHours = 24 },
            Llm = new LlmConfig { Provider = "openai", Model = "gpt-4o-mini" }
        };
        var logger = Substitute.For<ILogger<ArticleProcessor>>();
        _processor = new ArticleProcessor(Options.Create(config), logger);
    }

    [Fact]
    public async Task ProcessAsync_FiltersOldArticles()
    {
        var articles = new List<NewsArticle>
        {
            new("Recent Article", "https://example.com/1", "Test", DateTimeOffset.UtcNow.AddHours(-1)),
            new("Old Article", "https://example.com/2", "Test", DateTimeOffset.UtcNow.AddHours(-48))
        };

        var result = await _processor.ProcessAsync(articles);

        Assert.Single(result);
        Assert.Equal("Recent Article", result[0].Title);
    }

    [Fact]
    public async Task ProcessAsync_DeduplicatesByTitle()
    {
        var articles = new List<NewsArticle>
        {
            new("Breaking News", "https://example.com/1", "Source A", DateTimeOffset.UtcNow.AddHours(-1)),
            new("Breaking News", "https://example.com/2", "Source B", DateTimeOffset.UtcNow.AddMinutes(-30))
        };

        var result = await _processor.ProcessAsync(articles);

        Assert.Single(result);
    }

    [Fact]
    public async Task ProcessAsync_DeduplicatesIgnoringCase()
    {
        var articles = new List<NewsArticle>
        {
            new("Breaking News", "https://example.com/1", "Source A", DateTimeOffset.UtcNow.AddHours(-1)),
            new("breaking news", "https://example.com/2", "Source B", DateTimeOffset.UtcNow.AddMinutes(-30))
        };

        var result = await _processor.ProcessAsync(articles);

        Assert.Single(result);
    }

    [Fact]
    public async Task ProcessAsync_OrdersByDateDescending()
    {
        var articles = new List<NewsArticle>
        {
            new("Older Article", "https://example.com/1", "Test", DateTimeOffset.UtcNow.AddHours(-3)),
            new("Newest Article", "https://example.com/2", "Test", DateTimeOffset.UtcNow.AddMinutes(-10)),
            new("Middle Article", "https://example.com/3", "Test", DateTimeOffset.UtcNow.AddHours(-1))
        };

        var result = await _processor.ProcessAsync(articles);

        Assert.Equal("Newest Article", result[0].Title);
        Assert.Equal("Middle Article", result[1].Title);
        Assert.Equal("Older Article", result[2].Title);
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyList_ReturnsEmpty()
    {
        var result = await _processor.ProcessAsync([]);

        Assert.Empty(result);
    }
}
