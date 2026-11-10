using AgroPlan.Application.Abstractions;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace AgroPlan.Infrastructure.Email;

public sealed class EmailDeliveryNotConfiguredException()
    : InvalidOperationException("Configure the email delivery settings before sending account recovery messages.");

public sealed class ConfiguredEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var fromAddress = configuration["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
            throw new EmailDeliveryNotConfiguredException();

        var port = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;
        if (port is < 1 or > 65535) throw new InvalidOperationException("Email:SmtpPort must be a valid TCP port.");
        var username = configuration["Email:SmtpUserName"];
        var password = configuration["Email:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            throw new EmailDeliveryNotConfiguredException();

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = textBody };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        var security = configuration.GetValue<bool>("Email:SmtpUseSslOnConnect")
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
        await client.ConnectAsync(host, port, security, cancellationToken);
        if (!string.IsNullOrWhiteSpace(username))
            await client.AuthenticateAsync(username, password!, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
