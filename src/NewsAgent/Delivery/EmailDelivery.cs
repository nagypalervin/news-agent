using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NewsAgent.Models;

namespace NewsAgent.Delivery;

/// <summary>
/// Delivers the digest via SMTP email using MailKit.
/// </summary>
public class EmailDelivery(
    IOptions<DigestConfig> config,
    ILogger<EmailDelivery> logger) : IDigestDelivery
{
    /// <inheritdoc />
    public async Task DeliverAsync(DigestOutput digest, CancellationToken cancellationToken = default)
    {
        var emailConfig = config.Value.Email
            ?? throw new InvalidOperationException("Email delivery is not configured.");

        var subject = emailConfig.Subject.Replace("{date}", DateTime.UtcNow.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(emailConfig.FromAddress ?? "noreply@localhost"));
        foreach (var to in emailConfig.To)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = digest.HtmlContent,
            TextBody = digest.PlainTextContent
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        logger.LogInformation("Connecting to SMTP server {Host}:{Port}", emailConfig.SmtpHost, emailConfig.SmtpPort);

        await client.ConnectAsync(emailConfig.SmtpHost, emailConfig.SmtpPort, emailConfig.UseSsl, cancellationToken);

        var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
        var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        if (smtpUser is not null && smtpPassword is not null)
        {
            await client.AuthenticateAsync(smtpUser, smtpPassword, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Digest email sent to {Recipients}", string.Join(", ", emailConfig.To));
    }
}
