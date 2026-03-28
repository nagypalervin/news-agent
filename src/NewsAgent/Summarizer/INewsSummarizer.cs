using NewsAgent.Models;

namespace NewsAgent.Summarizer;

/// <summary>
/// Generates a newsletter-style digest from a list of articles using an LLM.
/// </summary>
public interface INewsSummarizer
{
    /// <summary>
    /// Summarizes the given articles into a formatted digest.
    /// </summary>
    Task<DigestOutput> SummarizeAsync(List<NewsArticle> articles, CancellationToken cancellationToken = default);
}
