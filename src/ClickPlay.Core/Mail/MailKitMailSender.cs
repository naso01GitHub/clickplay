using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ClickPlay.Core.Mail;

/// <summary>
/// Sends mail with MailKit. Port 465 uses SSL; other ports use STARTTLS when the server offers it.
/// </summary>
public sealed class MailKitMailSender : IMailSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Connects (SSL on port 465, otherwise STARTTLS when offered), logs in when a user is set,
    /// sends one plain-text mail to all recipients, and disconnects. Gives up after 30 seconds.
    /// </summary>
    /// <param name="smtp">Validated SMTP settings.</param>
    /// <param name="to">Recipient addresses; at least one.</param>
    /// <param name="subject">One-line subject.</param>
    /// <param name="body">Plain-text body.</param>
    /// <param name="cancellationToken">Cancels connecting, login, or sending.</param>
    /// <returns>A task that completes when the server accepted the mail.</returns>
    /// <exception cref="ArgumentException"><paramref name="to"/> is empty.</exception>
    /// <exception cref="MailKit.Security.AuthenticationException">Wrong user or password (Yahoo needs an app password).</exception>
    /// <exception cref="OperationCanceledException">Cancelled or the 30-second timeout passed.</exception>
    /// <example>
    /// <code>
    /// await new MailKitMailSender().SendAsync(smtp, ["a@b.com"], "Free padel", body, CancellationToken.None);
    /// </code>
    /// </example>
    public async Task SendAsync(
        SmtpSettings smtp,
        IReadOnlyList<string> to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(smtp);
        ArgumentNullException.ThrowIfNull(to);
        if (to.Count == 0)
        {
            throw new ArgumentException("At least one recipient is required.", nameof(to));
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.From));
        foreach (var address in to)
        {
            message.To.Add(MailboxAddress.Parse(address));
        }

        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        using var client = new SmtpClient { Timeout = (int)SendTimeout.TotalMilliseconds };
        var security = smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(smtp.Host, smtp.Port, security, timeout.Token).ConfigureAwait(false);
        if (smtp.User.Length > 0)
        {
            await client.AuthenticateAsync(smtp.User, smtp.Password, timeout.Token).ConfigureAwait(false);
        }

        await client.SendAsync(message, timeout.Token).ConfigureAwait(false);
        await client.DisconnectAsync(true, timeout.Token).ConfigureAwait(false);
    }
}
