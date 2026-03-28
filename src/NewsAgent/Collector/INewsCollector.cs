using NewsAgent.Models;

namespace NewsAgent.Collector;

/// <summary>
/// Collects news articles from an external source.
/// </summary>
public interface INewsCollector
{
    /// <summary>
    /// Collects news articles asynchronously.
    /// </summary>
    Task<List<NewsArticle>> CollectAsync(CancellationToken cancellationToken = default);
}
