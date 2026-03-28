using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Models;

namespace NewsAgent.Processor;

/// <summary>
/// Deduplicates articles by title similarity, filters by date, and checks topic relevance.
/// </summary>
public class ArticleProcessor(
    IOptions<DigestConfig> config,
    ILogger<ArticleProcessor> logger) : IArticleProcessor
{
    /// <inheritdoc />
    public Task<List<NewsArticle>> ProcessAsync(List<NewsArticle> articles, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-config.Value.Processor.MaxAgeHours);

        // Filter by date
        var recent = articles
            .Where(a => a.PublishedAt >= cutoff)
            .ToList();

        logger.LogInformation("Filtered to {RecentCount}/{TotalCount} articles within {Hours}h window",
            recent.Count, articles.Count, config.Value.Processor.MaxAgeHours);

        // Deduplicate by normalized title
        var deduplicated = recent
            .DistinctBy(a => NormalizeTitle(a.Title))
            .OrderByDescending(a => a.PublishedAt)
            .ToList();

        logger.LogInformation("Deduplicated to {Count} articles", deduplicated.Count);

        // Filter by topic relevance
        var topics = config.Value.Topics;
        var allKeywords = topics.Primary.Concat(topics.Secondary).ToList();

        if (allKeywords.Count == 0)
        {
            logger.LogInformation("No topic keywords configured, skipping relevance filter");
            return Task.FromResult(deduplicated);
        }

        var relevant = deduplicated
            .Where(a => IsRelevant(a, allKeywords))
            .ToList();

        var filtered = deduplicated.Count - relevant.Count;
        logger.LogInformation(
            "Topic relevance: {RelevantCount}/{TotalCount} articles matched, {FilteredCount} filtered out",
            relevant.Count, deduplicated.Count, filtered);

        return Task.FromResult(relevant);
    }

    private static bool IsRelevant(NewsArticle article, List<string> keywords)
    {
        var text = $"{article.Title} {article.Summary}";
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTitle(string title) =>
        title.Trim().ToUpperInvariant();
}
