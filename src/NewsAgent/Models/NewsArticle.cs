namespace NewsAgent.Models;

/// <summary>
/// Represents a single news article collected from any source.
/// </summary>
public record NewsArticle(
    string Title,
    string Url,
    string Source,
    DateTimeOffset PublishedAt,
    string? Summary = null);
