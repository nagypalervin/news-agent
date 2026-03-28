using NewsAgent.Models;

namespace NewsAgent.Processor;

/// <summary>
/// Processes and filters a list of collected news articles.
/// </summary>
public interface IArticleProcessor
{
    /// <summary>
    /// Deduplicates and filters articles by relevance and date.
    /// </summary>
    Task<List<NewsArticle>> ProcessAsync(List<NewsArticle> articles, CancellationToken cancellationToken = default);
}
