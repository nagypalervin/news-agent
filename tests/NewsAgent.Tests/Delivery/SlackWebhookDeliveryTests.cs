using System.Text.Json;
using NewsAgent.Delivery;

namespace NewsAgent.Tests.Delivery;

public class SlackWebhookDeliveryTests
{
    private const string SampleHtml = """
        <h1>📰 AI Hírek – 2026. március 29.</h1>
        <p>Az OpenAI leállítja a Sora alkalmazást, miközben az Anthropic növekedést mutat.</p>
        <hr>
        <h2>🔥 A nap sztorija</h2>
        <p><strong>OpenAI – Leáll a Sora videógeneráló</strong></p>
        <p>Az OpenAI bejelentette, hogy megszünteti a Sora önálló videógeneráló alkalmazását.</p>
        <p>🔗 <a href="https://example.com/sora">The Verge – Sora leállítás</a></p>
        <hr>
        <h2>📌 Egyéb fontos hírek</h2>
        <p><strong>Anthropic – A Claude fizetős bázisa nő</strong></p>
        <p>A Claude fizetős előfizetéseinek száma idén megduplázódott.</p>
        <p>🔗 <a href="https://example.com/claude">TechCrunch – Claude növekedés</a></p>
        <p><strong>xAI – Távozik Musk társalapítója</strong></p>
        <p>Az xAI egyik utolsó társalapítója is elhagyja a céget.</p>
        <p>🔗 <a href="https://example.com/xai">TechCrunch – xAI távozás</a></p>
        <hr>
        <h2>💡 Összefoglalás</h2>
        <p>A nap legfontosabb mintázata az AI-ipar gyors változása.</p>
        """;

    private static JsonElement GetBlocks(string payload)
    {
        var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("blocks");
    }

    private static List<string> GetBlockTypes(JsonElement blocks) =>
        Enumerable.Range(0, blocks.GetArrayLength())
            .Select(i => blocks[i].GetProperty("type").GetString()!)
            .ToList();

    [Fact]
    public void BuildBlockKitPayload_IsValidJson()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var doc = JsonDocument.Parse(payload);
        Assert.NotNull(doc.RootElement.GetProperty("blocks"));
    }

    [Fact]
    public void BuildBlockKitPayload_HasHeaderBlock()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);

        var header = blocks[0];
        Assert.Equal("header", header.GetProperty("type").GetString());
        Assert.Contains("AI Hírek", header.GetProperty("text").GetProperty("text").GetString());
        Assert.Contains("2026. 03. 29.", header.GetProperty("text").GetProperty("text").GetString());
        Assert.Equal("plain_text", header.GetProperty("text").GetProperty("type").GetString());
    }

    [Fact]
    public void BuildBlockKitPayload_HasIntroParagraph()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);

        // Second block should be a section with intro text in italic
        var intro = blocks[1];
        Assert.Equal("section", intro.GetProperty("type").GetString());
        var text = intro.GetProperty("text").GetProperty("text").GetString()!;
        Assert.Contains("Sora", text);
        Assert.StartsWith("_", text);
        Assert.EndsWith("_", text);
    }

    [Fact]
    public void BuildBlockKitPayload_HasDividerAfterIntro()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);

        Assert.Equal("divider", blocks[2].GetProperty("type").GetString());
    }

    [Fact]
    public void BuildBlockKitPayload_HasSectionHeaders()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);
        var allText = string.Join("\n", Enumerable.Range(0, blocks.GetArrayLength())
            .Where(i => blocks[i].GetProperty("type").GetString() == "section")
            .Select(i => blocks[i].GetProperty("text").GetProperty("text").GetString()));

        Assert.Contains("*🔥 A nap sztorija*", allText);
        Assert.Contains("*📌 Egyéb fontos hírek*", allText);
    }

    [Fact]
    public void BuildBlockKitPayload_HasBoldArticleTitles()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);
        var sectionTexts = Enumerable.Range(0, blocks.GetArrayLength())
            .Where(i => blocks[i].GetProperty("type").GetString() == "section")
            .Select(i => blocks[i].GetProperty("text").GetProperty("text").GetString()!)
            .ToList();

        Assert.Contains(sectionTexts, t => t.Contains("*OpenAI – Leáll a Sora"));
        Assert.Contains(sectionTexts, t => t.Contains("*Anthropic – A Claude"));
        Assert.Contains(sectionTexts, t => t.Contains("*xAI – Távozik Musk"));
    }

    [Fact]
    public void BuildBlockKitPayload_HasSourceLinks()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);
        var sectionTexts = Enumerable.Range(0, blocks.GetArrayLength())
            .Where(i => blocks[i].GetProperty("type").GetString() == "section")
            .Select(i => blocks[i].GetProperty("text").GetProperty("text").GetString()!)
            .ToList();

        Assert.Contains(sectionTexts, t => t.Contains("<https://example.com/sora|The Verge"));
        Assert.Contains(sectionTexts, t => t.Contains("<https://example.com/claude|TechCrunch"));
        Assert.Contains(sectionTexts, t => t.Contains("<https://example.com/xai|TechCrunch"));
    }

    [Fact]
    public void BuildBlockKitPayload_HasDividersBetweenItems()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);
        var types = GetBlockTypes(blocks);

        // Should have multiple dividers (after intro + between news items)
        var dividerCount = types.Count(t => t == "divider");
        Assert.True(dividerCount >= 3, $"Expected at least 3 dividers, got {dividerCount}");
    }

    [Fact]
    public void BuildBlockKitPayload_HasSummarySection()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);
        var sectionTexts = Enumerable.Range(0, blocks.GetArrayLength())
            .Where(i => blocks[i].GetProperty("type").GetString() == "section")
            .Select(i => blocks[i].GetProperty("text").GetProperty("text").GetString()!)
            .ToList();

        Assert.Contains(sectionTexts, t => t.Contains("Összefoglalás"));
        Assert.Contains(sectionTexts, t => t.Contains("AI-ipar gyors változása"));
    }

    [Fact]
    public void BuildBlockKitPayload_HasContextFooter()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);

        var lastBlock = blocks[blocks.GetArrayLength() - 1];
        Assert.Equal("context", lastBlock.GetProperty("type").GetString());
        var text = lastBlock.GetProperty("elements")[0].GetProperty("text").GetString();
        Assert.Contains("News Agent", text);
        Assert.Contains("2026. 03. 29.", text);
    }

    [Fact]
    public void BuildBlockKitPayload_AllSectionsUseMrkdwn()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);

        for (var i = 0; i < blocks.GetArrayLength(); i++)
        {
            if (blocks[i].GetProperty("type").GetString() == "section")
            {
                Assert.Equal("mrkdwn", blocks[i].GetProperty("text").GetProperty("type").GetString());
            }
        }
    }

    [Fact]
    public void BuildBlockKitPayload_Under50BlockLimit()
    {
        var payload = SlackWebhookDelivery.BuildBlockKitPayload("2026. 03. 29.", SampleHtml, 5);
        var blocks = GetBlocks(payload);

        Assert.True(blocks.GetArrayLength() <= 50, $"Block count {blocks.GetArrayLength()} exceeds Slack 50-block limit");
    }
}
