using System.Net;
using System.Net.Mail;

namespace Izmenjuj.Web.Services;

public interface IAppEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public sealed class SmtpAppEmailSender(IConfiguration configuration, ILogger<SmtpAppEmailSender> logger) : IAppEmailSender
{
    private readonly string? _host = configuration["Email:Smtp:Host"]?.Trim();
    private readonly string? _username = configuration["Email:Smtp:Username"]?.Trim();
    private readonly string? _password = configuration["Email:Smtp:Password"];
    private readonly string _fromAddress = configuration["Email:FromAddress"]?.Trim() ?? "noreply@izmenjuj.si";
    private readonly string _fromName = configuration["Email:FromName"]?.Trim() ?? "Izmenjuj.si";
    private readonly int _port = int.TryParse(configuration["Email:Smtp:Port"], out var port) ? port : 587;
    private readonly bool _enableSsl = !bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var ssl) || ssl;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_host);

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("E-mail ni bil poslan, ker SMTP ni konfiguriran. Prejemnik: {Recipient}; zadeva: {Subject}", to, subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(_host!, _port)
        {
            EnableSsl = _enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(_username),
            Credentials = string.IsNullOrWhiteSpace(_username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_username, _password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
