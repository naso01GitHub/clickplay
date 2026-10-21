namespace ClickPlay.Core.Mail;

/// <summary>
/// Validated SMTP connection settings used to send results.
/// </summary>
/// <param name="Host">SMTP server, e.g. <c>smtp.mail.yahoo.com</c>.</param>
/// <param name="Port">SMTP port, 1–65535 (465 = SSL, 587 = STARTTLS).</param>
/// <param name="User">Login name. Empty means no authentication.</param>
/// <param name="Password">Login password or app password.</param>
/// <param name="From">Sender address.</param>
/// <example>
/// <code>
/// var smtp = new SmtpSettings("smtp.mail.yahoo.com", 465, "me@yahoo.com", "app-password", "me@yahoo.com");
/// </code>
/// </example>
public sealed record SmtpSettings(string Host, int Port, string User, string Password, string From)
{
    /// <summary>
    /// Hides the password so the settings can be logged safely.
    /// </summary>
    /// <returns>Host, port, user, and sender; the password is replaced by stars.</returns>
    /// <example>
    /// <code>
    /// var text = smtp.ToString(); // ... Password = *** ...
    /// </code>
    /// </example>
    public override string ToString() =>
        $"SmtpSettings {{ Host = {Host}, Port = {Port}, User = {User}, Password = ***, From = {From} }}";
}
