using MimeKit;

namespace ClickPlay.Core.Mail;

/// <summary>
/// Validates the SMTP fields typed in the GUI before a mail is sent.
/// </summary>
public static class SmtpSettingsValidator
{
    /// <summary>
    /// True when the user has started filling SMTP settings (Host is not empty).
    /// An empty Host means "do not send mail".
    /// </summary>
    /// <param name="host">Host field text.</param>
    /// <returns>True when Host has text.</returns>
    /// <example>
    /// <code>
    /// if (!SmtpSettingsValidator.IsConfigured(SmtpHost)) return;
    /// </code>
    /// </example>
    public static bool IsConfigured(string? host) => !string.IsNullOrWhiteSpace(host);

    /// <summary>
    /// Parses and checks the SMTP fields.
    /// </summary>
    /// <param name="host">SMTP server name.</param>
    /// <param name="portText">Port text, integer 1–65535.</param>
    /// <param name="user">Login name; may be empty.</param>
    /// <param name="password">Password; may be empty when <paramref name="user"/> is empty.</param>
    /// <param name="from">Sender address.</param>
    /// <param name="settings">Validated settings, or null when invalid.</param>
    /// <param name="error">User-visible error, or empty.</param>
    /// <returns>True when all fields are valid.</returns>
    /// <example>
    /// <code>
    /// if (SmtpSettingsValidator.TryCreate("smtp.mail.yahoo.com", "465", "me@yahoo.com", "pw", "me@yahoo.com", out var smtp, out var error)) { }
    /// </code>
    /// </example>
    public static bool TryCreate(
        string? host,
        string? portText,
        string? user,
        string? password,
        string? from,
        out SmtpSettings? settings,
        out string error)
    {
        settings = null;
        if (string.IsNullOrWhiteSpace(host) || host.Trim().Any(char.IsWhiteSpace))
        {
            error = "SMTP host is required (for example smtp.mail.yahoo.com).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(portText) || !int.TryParse(portText.Trim(), out var port) || port is < 1 or > 65535)
        {
            error = "SMTP port must be a number from 1 to 65535 (usually 465 or 587).";
            return false;
        }

        if (!IsAddress(from))
        {
            error = "From must be a valid email address.";
            return false;
        }

        var login = user?.Trim() ?? string.Empty;
        if (login.Length > 0 && string.IsNullOrEmpty(password))
        {
            error = "SMTP password is required when User is set.";
            return false;
        }

        settings = new SmtpSettings(host.Trim(), port, login, password ?? string.Empty, from!.Trim());
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// True for a single plain address such as <c>name@example.com</c>.
    /// </summary>
    /// <param name="text">Address text.</param>
    /// <returns>True when MimeKit parses it as one mailbox with an <c>@</c>.</returns>
    /// <example>
    /// <code>
    /// var ok = SmtpSettingsValidator.IsAddress("a@b.com"); // true
    /// </code>
    /// </example>
    public static bool IsAddress(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && MailboxAddress.TryParse(text.Trim(), out var mailbox)
        && mailbox.Address.Contains('@', StringComparison.Ordinal)
        && !mailbox.Address.StartsWith('@')
        && !mailbox.Address.EndsWith('@');
}
