using System.Text.Json;
using NewsAgent.Delivery;
using NewsAgent.Models;

namespace NewsAgent.Tests.Delivery;

public class TeamsWebhookDeliveryTests
{
    private const string SampleHtml = """
        <h1>📰 AI Hírek – 2026. március 29.</h1>
        <p>Ma az OpenAI és az Anthropic uralták a híreket.</p>
        <hr>
        <h2>🔥 A nap sztorija</h2>
        <p><strong>OpenAI – Visszavonja a Sora-t</strong></p>
        <p>Az OpenAI leállítja a Sora alkalmazást.</p>
        <p>🔗 <a href="https://example.com/sora">The Verge AI</a></p>
        <hr>
        <h2>📌 Egyéb fontos hírek</h2>
        <p><strong>Anthropic – Claude növekedés</strong></p>
        <p>A Claude fizetős felhasználói bázisa duplázódott.</p>
        <p>🔗 <a href="https://example.com/claude">TechCrunch AI</a></p>
        <hr>
        <h2>💡 Összefoglalás</h2>
        <p>A nap legfontosabb trendje a verseny erősödése volt.</p>
        """;

    [Fact]
    public void BuildAdaptiveCardPayload_IsValidJson()
    {
        var payload = TeamsWebhookDelivery.BuildAdaptiveCardPayload("2026. 03. 29.", SampleHtml, 5);

        var doc = JsonDocument.Parse(payload);
        Assert.NotNull(doc);
    }

    [Fact]
    public void BuildAdaptiveCardPayload_HasCorrectEnvelope()
    {
        var payload = TeamsWebhookDelivery.BuildAdaptiveCardPayload("2026. 03. 29.", SampleHtml, 5);

        var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("message", root.GetProperty("type").GetString());
        var attachment = root.GetProperty("attachments")[0];
        Assert.Equal("application/vnd.microsoft.card.adaptive", attachment.GetProperty("contentType").GetString());

        var content = attachment.GetProperty("content");
        Assert.Equal("AdaptiveCard", content.GetProperty("type").GetString());
        Assert.Equal("1.4", content.GetProperty("version").GetString());
    }

    [Fact]
    public void BuildAdaptiveCardPayload_HasDateHeader()
    {
        var payload = TeamsWebhookDelivery.BuildAdaptiveCardPayload("2026. 03. 29.", SampleHtml, 5);

        var body = GetCardBody(payload);
        var header = body[0];

        Assert.Equal("TextBlock", header.GetProperty("type").GetString());
        Assert.Contains("2026. 03. 29.", header.GetProperty("text").GetString());
        Assert.Equal("Large", header.GetProperty("size").GetString());
        Assert.Equal("Bolder", header.GetProperty("weight").GetString());
    }

    [Fact]
    public void BuildAdaptiveCardPayload_HasArticleCountSubtitle()
    {
        var payload = TeamsWebhookDelivery.BuildAdaptiveCardPayload("2026. 03. 29.", SampleHtml, 7);

        var body = GetCardBody(payload);
        var subtitle = body[1];

        Assert.Contains("7 cikk", subtitle.GetProperty("text").GetString());
        Assert.True(subtitle.GetProperty("isSubtle").GetBoolean());
    }

    [Fact]
    public void ParseHtmlToCardBody_CreatesSectionHeaders()
    {
        var body = TeamsWebhookDelivery.ParseHtmlToCardBody(SampleHtml, "2026. 03. 29.", 5);

        var sectionHeaders = body
            .Where(b => b.TryGetValue("weight", out var w) && w is "Bolder"
                        && b.TryGetValue("size", out var s) && s is "Medium")
            .Select(b => (string)b["text"])
            .ToList();

        Assert.Contains(sectionHeaders, h => h.Contains("A nap sztorija"));
        Assert.Contains(sectionHeaders, h => h.Contains("Egyéb fontos hírek"));
        Assert.Contains(sectionHeaders, h => h.Contains("Összefoglalás"));
    }

    [Fact]
    public void ParseHtmlToCardBody_CreatesBoldTitles()
    {
        var body = TeamsWebhookDelivery.ParseHtmlToCardBody(SampleHtml, "2026. 03. 29.", 5);

        var boldBlocks = body
            .Where(b => b.TryGetValue("type", out var t) && t is "TextBlock"
                        && b.TryGetValue("weight", out var w) && w is "Bolder"
                        && !b.ContainsKey("size"))
            .Select(b => (string)b["text"])
            .ToList();

        Assert.Contains(boldBlocks, t => t.Contains("OpenAI") && t.Contains("Sora"));
        Assert.Contains(boldBlocks, t => t.Contains("Anthropic") && t.Contains("Claude"));
    }

    [Fact]
    public void ParseHtmlToCardBody_CreatesActionOpenUrlForLinks()
    {
        var body = TeamsWebhookDelivery.ParseHtmlToCardBody(SampleHtml, "2026. 03. 29.", 5);

        var actionSets = body.Where(b => b.TryGetValue("type", out var t) && t is "ActionSet").ToList();

        Assert.True(actionSets.Count >= 2, $"Expected at least 2 ActionSets, got {actionSets.Count}");

        var firstActions = (List<Dictionary<string, object>>)actionSets[0]["actions"];
        Assert.Equal("Action.OpenUrl", firstActions[0]["type"]);
        Assert.Equal("https://example.com/sora", firstActions[0]["url"]);
    }

    [Fact]
    public void ParseHtmlToCardBody_HasFooter()
    {
        var body = TeamsWebhookDelivery.ParseHtmlToCardBody(SampleHtml, "2026. 03. 29.", 5);

        var footer = body.Last();
        Assert.Equal("TextBlock", footer["type"]);
        Assert.Contains("News Agent", (string)footer["text"]);
        Assert.Equal("Small", footer["size"]);
        Assert.True((bool)footer["isSubtle"]);
    }

    [Fact]
    public void ParseHtmlToCardBody_AllTextBlocksHaveWrap()
    {
        var body = TeamsWebhookDelivery.ParseHtmlToCardBody(SampleHtml, "2026. 03. 29.", 5);

        var textBlocks = body.Where(b => b.TryGetValue("type", out var t) && t is "TextBlock").ToList();

        foreach (var block in textBlocks)
        {
            Assert.True(block.ContainsKey("wrap"), $"TextBlock missing wrap: {block["text"]}");
            Assert.True((bool)block["wrap"]);
        }
    }

    [Fact]
    public void ParseHtmlToCardBody_IncludesSummaryContent()
    {
        var body = TeamsWebhookDelivery.ParseHtmlToCardBody(SampleHtml, "2026. 03. 29.", 5);

        var allText = string.Join(" ", body
            .Where(b => b.TryGetValue("type", out var t) && t is "TextBlock")
            .Select(b => (string)b["text"]));

        Assert.Contains("leállítja a Sora", allText);
        Assert.Contains("duplázódott", allText);
        Assert.Contains("verseny erősödése", allText);
    }

    private static JsonElement GetCardBody(string payload)
    {
        var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("attachments")[0].GetProperty("content").GetProperty("body");
    }
}
