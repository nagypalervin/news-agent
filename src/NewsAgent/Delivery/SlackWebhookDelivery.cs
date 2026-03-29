using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Models;

namespace NewsAgent.Delivery;

/// <summary>
/// Delivers the digest to Slack via incoming webhook using Block Kit.
/// Parses the HTML digest into structured Slack blocks for native formatting.
/// </summary>
public partial class SlackWebhookDelivery(
    IHttpClientFactory httpClientFactory,
    IOptions<DigestConfig> config,
    ILogger<SlackWebhookDelivery> logger) : IDigestDelivery
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task DeliverAsync(DigestOutput digest, CancellationToken cancellationToken = default)
    {
        var webhookUrl = config.Value.Webhooks?.Slack?.Url
            ?? throw new InvalidOperationException("Slack webhook URL is not configured.");

        var tz = TimeZoneInfo.FindSystemTimeZoneById(config.Value.Schedule.Timezone);
        var localTime = TimeZoneInfo.ConvertTime(digest.GeneratedAt, tz);
        var datePart = localTime.ToString("yyyy. MM. dd.");

        var payload = BuildBlockKitPayload(datePart, digest.HtmlContent, digest.ArticleCount);

        using var client = httpClientFactory.CreateClient("Webhooks");
        client.Timeout = Timeout;

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(webhookUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Slack webhook delivered successfully ({StatusCode})", (int)response.StatusCode);
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Slack webhook failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
        }
    }

    /// <summary>
    /// Builds a Slack Block Kit payload by parsing the digest HTML into structured blocks.
    /// </summary>
    public static string BuildBlockKitPayload(string date, string htmlContent, int articleCount)
    {
        var blocks = ParseHtmlToBlocks(htmlContent, date, articleCount);
        var payload = new { blocks };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// Parses digest HTML into a list of Slack Block Kit block elements.
    /// </summary>
    public static List<Dictionary<string, object>> ParseHtmlToBlocks(string html, string date, int articleCount)
    {
        var blocks = new List<Dictionary<string, object>>();

        // Header block
        blocks.Add(HeaderBlock($"\U0001F4F0 AI Hírek — {date}"));

        // Split by <hr> to get major sections
        var sections = HrSplitRegex().Split(html);

        var isFirstSection = true;

        foreach (var section in sections)
        {
            var trimmed = section.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Extract intro paragraph (first <p> after <h1>, before any <h2>)
            if (isFirstSection)
            {
                isFirstSection = false;
                var introParagraphs = ParagraphRegex().Matches(trimmed);
                foreach (Match p in introParagraphs)
                {
                    var text = StripTags(p.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        blocks.Add(SectionBlock($"_{text}_"));
                        blocks.Add(DividerBlock());
                    }
                }
                continue;
            }

            // Check for section header (h2)
            var h2Match = H2Regex().Match(trimmed);
            var sectionHeader = h2Match.Success ? StripTags(h2Match.Groups[1].Value) : null;

            // Check if this is the summary section (💡)
            if (sectionHeader is not null && sectionHeader.Contains("\U0001F4A1"))
            {
                blocks.Add(SectionBlock($"*{sectionHeader}*"));
                var summaryParagraphs = ParagraphRegex().Matches(trimmed);
                foreach (Match p in summaryParagraphs)
                {
                    var text = StripTags(p.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(text))
                        blocks.Add(SectionBlock(text));
                }
                continue;
            }

            // News section — add header if present
            if (sectionHeader is not null)
                blocks.Add(SectionBlock($"*{sectionHeader}*"));

            // Parse individual news items: <strong> title, summary <p>, source link <a>
            var remaining = h2Match.Success
                ? trimmed[(h2Match.Index + h2Match.Length)..]
                : trimmed;

            var paragraphs = ParagraphRegex().Matches(remaining);
            foreach (Match p in paragraphs)
            {
                var pContent = p.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(pContent))
                    continue;

                var plainContent = StripTags(pContent);

                // Source link paragraph (🔗)
                if (plainContent.StartsWith("\U0001F517"))
                {
                    var linkMatch = AnchorRegex().Match(pContent);
                    if (linkMatch.Success)
                    {
                        var url = linkMatch.Groups[1].Value;
                        var linkText = StripTags(linkMatch.Groups[2].Value);
                        blocks.Add(SectionBlock($"\U0001F517 <{url}|{linkText}>"));
                    }
                    blocks.Add(DividerBlock());
                    continue;
                }

                // Bold article title
                var strongMatch = StrongRegex().Match(pContent);
                if (strongMatch.Success)
                {
                    var title = StripTags(strongMatch.Groups[1].Value);
                    blocks.Add(SectionBlock($"*{title}*"));
                    continue;
                }

                // Regular summary paragraph
                blocks.Add(SectionBlock(plainContent));
            }
        }

        // Footer context block
        blocks.Add(ContextBlock($"Generated by News Agent | {date}"));

        return blocks;
    }

    private static Dictionary<string, object> HeaderBlock(string text) => new()
    {
        ["type"] = "header",
        ["text"] = new Dictionary<string, object>
        {
            ["type"] = "plain_text",
            ["text"] = text,
            ["emoji"] = true
        }
    };

    private static Dictionary<string, object> SectionBlock(string mrkdwn) => new()
    {
        ["type"] = "section",
        ["text"] = new Dictionary<string, object>
        {
            ["type"] = "mrkdwn",
            ["text"] = HttpUtility.HtmlDecode(mrkdwn)
        }
    };

    private static Dictionary<string, object> DividerBlock() => new()
    {
        ["type"] = "divider"
    };

    private static Dictionary<string, object> ContextBlock(string text) => new()
    {
        ["type"] = "context",
        ["elements"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["type"] = "mrkdwn",
                ["text"] = text
            }
        }
    };

    private static string StripTags(string html) =>
        HttpUtility.HtmlDecode(TagRegex().Replace(html, "")).Trim();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HrSplitRegex();

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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
