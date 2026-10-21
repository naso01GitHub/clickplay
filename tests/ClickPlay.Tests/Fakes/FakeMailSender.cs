using ClickPlay.Core.Mail;

namespace ClickPlay.Tests.Fakes;

/// <summary>
/// Test mail sender: records every mail instead of sending it, or throws <see cref="Throw"/> when set.
/// </summary>
public sealed class FakeMailSender : IMailSender
{
    /// <summary>Mails "sent" so far, in order.</summary>
    public List<SentMail> Sent { get; } = [];

    /// <summary>When set, every send throws this exception (to test mail failures).</summary>
    public Exception? Throw { get; init; }

    /// <summary>Number of send calls, including failed ones.</summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Records the mail in <see cref="Sent"/>, or throws <see cref="Throw"/>.
    /// </summary>
    /// <param name="smtp">SMTP settings passed by the ViewModel.</param>
    /// <param name="to">Recipients.</param>
    /// <param name="subject">Subject.</param>
    /// <param name="body">Body.</param>
    /// <param name="cancellationToken">Throws when already cancelled.</param>
    /// <returns>A completed task.</returns>
    /// <example>
    /// <code>
    /// var sender = new FakeMailSender();
    /// await sender.SendAsync(smtp, ["a@b.com"], "S", "B", CancellationToken.None);
    /// Assert.Single(sender.Sent);
    /// </code>
    /// </example>
    public Task SendAsync(
        SmtpSettings smtp,
        IReadOnlyList<string> to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        Attempts++;
        cancellationToken.ThrowIfCancellationRequested();
        if (Throw is not null)
        {
            throw Throw;
        }

        Sent.Add(new SentMail(smtp, to.ToArray(), subject, body));
        return Task.CompletedTask;
    }

    /// <summary>
    /// One recorded mail.
    /// </summary>
    /// <param name="Smtp">SMTP settings used.</param>
    /// <param name="To">Recipients.</param>
    /// <param name="Subject">Subject.</param>
    /// <param name="Body">Body.</param>
    /// <example>
    /// <code>
    /// Assert.Equal(["a@b.com"], sender.Sent[0].To);
    /// </code>
    /// </example>
    public sealed record SentMail(SmtpSettings Smtp, IReadOnlyList<string> To, string Subject, string Body);
}
