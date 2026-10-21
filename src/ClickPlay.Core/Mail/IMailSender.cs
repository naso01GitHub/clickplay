namespace ClickPlay.Core.Mail;

/// <summary>
/// Sends one plain-text mail. Tests use a fake; the app uses <see cref="MailKitMailSender"/>.
/// </summary>
public interface IMailSender
{
    /// <summary>
    /// Sends one mail to every recipient.
    /// </summary>
    /// <param name="smtp">Validated SMTP settings.</param>
    /// <param name="to">Recipient addresses; at least one.</param>
    /// <param name="subject">One-line subject.</param>
    /// <param name="body">Plain-text body.</param>
    /// <param name="cancellationToken">Cancels connecting or sending.</param>
    /// <returns>A task that completes when the server accepted the mail.</returns>
    /// <example>
    /// <code>
    /// await sender.SendAsync(smtp, ["a@b.com"], "Subject", "Body", CancellationToken.None);
    /// </code>
    /// </example>
    Task SendAsync(
        SmtpSettings smtp,
        IReadOnlyList<string> to,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
