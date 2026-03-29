using System.Text.RegularExpressions;
using System.Web;

namespace NewsAgent.Delivery;

/// <summary>
/// Converts HTML content to plain text by stripping tags and decoding entities.
/// </summary>
public static partial class HtmlToPlainText
{
    /// <summary>
    /// Strips HTML tags and converts to readable plain text.
    /// </summary>
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = html;

        // Replace block-level elements with newlines
        text = BlockTagRegex().Replace(text, "\n");
        text = BrTagRegex().Replace(text, "\n");
        text = HrTagRegex().Replace(text, "\n---\n");

        // Remove all remaining HTML tags
        text = HtmlTagRegex().Replace(text, string.Empty);

        // Decode HTML entities
        text = HttpUtility.HtmlDecode(text);

        // Normalize whitespace: collapse multiple blank lines
        text = MultipleNewlinesRegex().Replace(text, "\n\n");

        return text.Trim();
    }

    [GeneratedRegex(@"</(p|div|h[1-6]|li|tr|blockquote)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HrTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();
}
