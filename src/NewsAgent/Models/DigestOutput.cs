namespace NewsAgent.Models;

/// <summary>
/// The generated newsletter digest content ready for delivery.
/// </summary>
public record DigestOutput(
    string Title,
    string HtmlContent,
    string PlainTextContent,
    DateTimeOffset GeneratedAt,
    int ArticleCount);
