using MailKit.Net.Smtp;
using MailKit.Security;
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

        var message = BuildMessage(emailConfig, digest, config.Value.Schedule.Timezone);

        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? emailConfig.SmtpHost;
        var smtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : emailConfig.SmtpPort;

        logger.LogInformation("Connecting to SMTP server {Host}:{Port}", smtpHost, smtpPort);

        using var client = new SmtpClient();
        var socketOptions = emailConfig.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(smtpHost, smtpPort, socketOptions, cancellationToken);

        var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
        var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        if (smtpUser is not null && smtpPassword is not null)
        {
            await client.AuthenticateAsync(smtpUser, smtpPassword, cancellationToken);
        }

        var messageId = await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        var recipients = string.Join(", ", emailConfig.ToAddresses);
        logger.LogInformation("Email sent to {Recipients} (MessageId: {MessageId})", recipients, messageId);
    }

    public static MimeMessage BuildMessage(EmailConfig emailConfig, DigestOutput digest, string timezone)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var localTime = TimeZoneInfo.ConvertTime(digest.GeneratedAt, tz);
        var datePart = localTime.ToString("yyyy. MM. dd.");

        var subject = emailConfig.Subject.Replace("{date}", datePart, StringComparison.OrdinalIgnoreCase);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(emailConfig.FromAddress));
        foreach (var to in emailConfig.ToAddresses)
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

        return message;
    }
}
