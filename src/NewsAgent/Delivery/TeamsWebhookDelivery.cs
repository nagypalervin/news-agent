using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Models;

namespace NewsAgent.Delivery;

/// <summary>
/// Delivers the digest to Microsoft Teams via incoming webhook as a structured Adaptive Card.
/// </summary>
public partial class TeamsWebhookDelivery(
    IHttpClientFactory httpClientFactory,
    IOptions<DigestConfig> config,
    ILogger<TeamsWebhookDelivery> logger) : IDigestDelivery
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task DeliverAsync(DigestOutput digest, CancellationToken cancellationToken = default)
    {
        var webhookUrl = config.Value.Webhooks?.Teams?.Url
            ?? throw new InvalidOperationException("Teams webhook URL is not configured.");

        var tz = TimeZoneInfo.FindSystemTimeZoneById(config.Value.Schedule.Timezone);
        var localTime = TimeZoneInfo.ConvertTime(digest.GeneratedAt, tz);
        var datePart = localTime.ToString("yyyy. MM. dd.");

        var payload = BuildAdaptiveCardPayload(datePart, digest.HtmlContent, digest.ArticleCount);

        using var client = httpClientFactory.CreateClient("Webhooks");
        client.Timeout = Timeout;

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(webhookUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Teams webhook delivered successfully ({StatusCode})", (int)response.StatusCode);
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Teams webhook failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
        }
    }

    /// <summary>
    /// Builds an Adaptive Card payload from the digest HTML content.
    /// Parses the HTML structure into typed Adaptive Card elements.
    /// </summary>
    public static string BuildAdaptiveCardPayload(string date, string htmlContent, int articleCount)
    {
        var cardBody = ParseHtmlToCardBody(htmlContent, date, articleCount);

        var card = new AdaptiveCardMessage
        {
            Attachments =
            [
                new()
                {
                    Content = new AdaptiveCard
                    {
                        Body = cardBody
                    }
                }
            ]
        };

        return JsonSerializer.Serialize(card, JsonOptions);
    }

    /// <summary>
    /// Parses digest HTML into a list of Adaptive Card body elements.
    /// </summary>
    public static List<Dictionary<string, object>> ParseHtmlToCardBody(string html, string date, int articleCount)
    {
        var body = new List<Dictionary<string, object>>();

        // Header
        body.Add(TextBlock($"\U0001F4F0 AI Hírek — {date}", size: "Large", weight: "Bolder"));
        body.Add(TextBlock($"{articleCount} cikk összefoglalója", isSubtle: true, spacing: "None"));

        // Split by <hr> to get major sections
        var sections = HrSplitRegex().Split(html);

        foreach (var section in sections)
        {
            var trimmed = section.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Check for section headers (h2)
            var h2Matches = H2Regex().Matches(trimmed);
            if (h2Matches.Count > 0)
            {
                // Add separator before each major section
                body.Add(new Dictionary<string, object>
                {
                    ["type"] = "TextBlock",
                    ["text"] = " ",
                    ["separator"] = true,
                    ["spacing"] = "Medium",
                    ["wrap"] = true
                });
            }

            // Process the section content line by line
            var remaining = trimmed;

            // Extract h1 (date header) — skip it, we already added our own
            remaining = H1Regex().Replace(remaining, "");

            // Extract h2 sections
            remaining = H2Regex().Replace(remaining, m =>
            {
                var headerText = StripTags(m.Groups[1].Value);
                body.Add(TextBlock(headerText, size: "Medium", weight: "Bolder", spacing: "Medium"));
                return "";
            });

            // Extract paragraphs
            var paragraphs = ParagraphRegex().Matches(remaining);
            foreach (Match p in paragraphs)
            {
                var pContent = p.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(pContent))
                    continue;

                // Check if this is a source link paragraph (starts with 🔗)
                var plainContent = StripTags(pContent);
                if (plainContent.StartsWith("\U0001F517"))
                {
                    // Extract the link
                    var linkMatch = AnchorRegex().Match(pContent);
                    if (linkMatch.Success)
                    {
                        var url = linkMatch.Groups[1].Value;
                        var linkText = StripTags(linkMatch.Groups[2].Value);
                        body.Add(new Dictionary<string, object>
                        {
                            ["type"] = "ActionSet",
                            ["actions"] = new List<Dictionary<string, object>>
                            {
                                new()
                                {
                                    ["type"] = "Action.OpenUrl",
                                    ["title"] = $"\U0001F517 {linkText}",
                                    ["url"] = url
                                }
                            }
                        });
                    }
                    else
                    {
                        body.Add(TextBlock(plainContent));
                    }
                    continue;
                }

                // Check if it contains a <strong> title
                var strongMatch = StrongRegex().Match(pContent);
                if (strongMatch.Success)
                {
                    var strongText = StripTags(strongMatch.Groups[1].Value);
                    body.Add(TextBlock($"**{strongText}**", weight: "Bolder"));

                    // If there's text after the strong tag, add it as a separate paragraph
                    var afterStrong = pContent[(strongMatch.Index + strongMatch.Length)..].Trim();
                    if (!string.IsNullOrWhiteSpace(afterStrong))
                    {
                        body.Add(TextBlock(StripTags(afterStrong)));
                    }
                    continue;
                }

                // Regular paragraph
                body.Add(TextBlock(HttpUtility.HtmlDecode(plainContent)));
            }
        }

        // Footer
        body.Add(new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = "Generated by News Agent",
            ["size"] = "Small",
            ["isSubtle"] = true,
            ["separator"] = true,
            ["spacing"] = "Large",
            ["wrap"] = true
        });

        return body;
    }

    private static Dictionary<string, object> TextBlock(
        string text,
        string? size = null,
        string? weight = null,
        string? spacing = null,
        bool isSubtle = false)
    {
        var block = new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = HttpUtility.HtmlDecode(text),
            ["wrap"] = true
        };
        if (size is not null) block["size"] = size;
        if (weight is not null) block["weight"] = weight;
        if (spacing is not null) block["spacing"] = spacing;
        if (isSubtle) block["isSubtle"] = true;
        return block;
    }

    private static string StripTags(string html) =>
        HttpUtility.HtmlDecode(TagRegex().Replace(html, "")).Trim();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HrSplitRegex();

    [GeneratedRegex(@"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1Regex();

    [GeneratedRegex(@"<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H2Regex();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"<a\s+href=""([^""]+)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<strong>(.*?)</strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Typed models for clean JSON serialization
    private sealed class AdaptiveCardMessage
    {
        public string Type { get; init; } = "message";
        public List<AdaptiveCardAttachment> Attachments { get; init; } = [];
    }

    private sealed class AdaptiveCardAttachment
    {
        public string ContentType { get; init; } = "application/vnd.microsoft.card.adaptive";
        public AdaptiveCard Content { get; init; } = new();
    }

    private sealed class AdaptiveCard
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; init; } = "http://adaptivecards.io/schemas/adaptive-card.json";
        public string Type { get; init; } = "AdaptiveCard";
        public string Version { get; init; } = "1.4";
        public MsTeamsProperties MsTeams { get; init; } = new();
        public List<Dictionary<string, object>> Body { get; init; } = [];
    }

    private sealed class MsTeamsProperties
    {
        public string Width { get; init; } = "Full";
    }
}
