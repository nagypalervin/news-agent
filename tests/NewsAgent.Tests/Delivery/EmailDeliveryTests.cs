using NewsAgent.Delivery;
using NewsAgent.Models;

namespace NewsAgent.Tests.Delivery;

public class EmailDeliveryTests
{
    private static readonly EmailConfig TestEmailConfig = new()
    {
        Enabled = true,
        FromAddress = "news@test.com",
        ToAddresses = ["alice@test.com", "bob@test.com"],
        Subject = "AI Hírek — {date}",
        SmtpHost = "smtp.test.com",
        SmtpPort = 587,
        UseSsl = true
    };

    private static readonly DigestOutput TestDigest = new(
        Title: "Test Digest",
        HtmlContent: "<h1>Test</h1><p>Hello</p>",
        PlainTextContent: "Test\nHello",
        GeneratedAt: new DateTimeOffset(2026, 3, 28, 5, 0, 0, TimeSpan.Zero),
        ArticleCount: 3);

    [Fact]
    public void BuildMessage_SetsFromAddress()
    {
        var message = EmailDelivery.BuildMessage(TestEmailConfig, TestDigest, "UTC");

        Assert.Single(message.From);
        Assert.Equal("news@test.com", message.From.Mailboxes.First().Address);
    }

    [Fact]
    public void BuildMessage_SetsAllRecipients()
    {
        var message = EmailDelivery.BuildMessage(TestEmailConfig, TestDigest, "UTC");

        Assert.Equal(2, message.To.Count);
        Assert.Contains(message.To.Mailboxes, m => m.Address == "alice@test.com");
        Assert.Contains(message.To.Mailboxes, m => m.Address == "bob@test.com");
    }

    [Fact]
    public void BuildMessage_ReplacesDateInSubject()
    {
        var message = EmailDelivery.BuildMessage(TestEmailConfig, TestDigest, "UTC");

        Assert.Equal("AI Hírek — 2026. 03. 28.", message.Subject);
    }

    [Fact]
    public void BuildMessage_SubjectUsesConfiguredTimezone()
    {
        // GeneratedAt is 2026-03-28 05:00 UTC = 2026-03-28 06:00 CET (Europe/Budapest is UTC+1 in winter, UTC+2 in summer)
        // March 28 is in CEST (summer time), so UTC+2 → 07:00 local, still same date
        var message = EmailDelivery.BuildMessage(TestEmailConfig, TestDigest, "Europe/Budapest");

        Assert.Equal("AI Hírek — 2026. 03. 28.", message.Subject);
    }

    [Fact]
    public void BuildMessage_SetsHtmlBody()
    {
        var message = EmailDelivery.BuildMessage(TestEmailConfig, TestDigest, "UTC");

        Assert.NotNull(message.Body);
        Assert.Contains("text/html", message.Body.ToString());
    }

    [Fact]
    public void BuildMessage_SetsPlainTextBody()
    {
        var message = EmailDelivery.BuildMessage(TestEmailConfig, TestDigest, "UTC");

        Assert.Equal("Test\nHello", message.TextBody);
    }
}
