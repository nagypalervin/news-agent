using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Models;

namespace NewsAgent.Collector;

/// <summary>
/// Collects news articles from configured RSS feeds.
/// </summary>
public class RssCollector(
    IOptions<DigestConfig> config,
    ILogger<RssCollector> logger) : INewsCollector
{
    /// <inheritdoc />
    public async Task<List<NewsArticle>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var articles = new List<NewsArticle>();

        foreach (var rssFeed in config.Value.Sources.RssFeeds)
        {
            try
            {
                logger.LogInformation("Fetching RSS feed: {FeedUrl}", rssFeed.Url);
                var feed = await FeedReader.ReadAsync(rssFeed.Url, cancellationToken);

                foreach (var item in feed.Items)
                {
                    var publishedAt = item.PublishingDate ?? DateTimeOffset.UtcNow;

                    articles.Add(new NewsArticle(
                        Title: item.Title ?? "Untitled",
                        Url: item.Link ?? string.Empty,
                        Source: rssFeed.Name.Length > 0 ? rssFeed.Name : feed.Title ?? rssFeed.Url,
                        PublishedAt: new DateTimeOffset(publishedAt.UtcDateTime, TimeSpan.Zero),
                        Summary: item.Description));
                }

                logger.LogInformation("Collected {Count} articles from {FeedUrl}", feed.Items.Count, rssFeed.Url);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to fetch RSS feed: {FeedUrl}", rssFeed.Url);
            }
        }

        return articles;
    }
}
