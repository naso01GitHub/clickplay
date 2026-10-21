namespace ClickPlay.Core.Mail;

/// <summary>
/// Everything the Email section of the GUI remembers between runs, as typed (not yet validated).
/// </summary>
/// <param name="Host">SMTP host text.</param>
/// <param name="Port">SMTP port text.</param>
/// <param name="User">SMTP user text.</param>
/// <param name="Password">SMTP password. Kept in <see cref="ISecretStore"/>, never in the JSON file.</param>
/// <param name="From">Sender address text.</param>
/// <param name="To">Recipients, one per line.</param>
/// <param name="Subject">Subject prefix text.</param>
/// <param name="RememberLogin">
/// When false, User and Password are not saved (and the stored password is erased), so they start empty next time.
/// </param>
/// <example>
/// <code>
/// var settings = new MailSettings("smtp.mail.yahoo.com", "465", "me@yahoo.com", "pw", "me@yahoo.com", "a@b.com", "Free padel");
/// var forget = settings with { RememberLogin = false };
/// </code>
/// </example>
public sealed record MailSettings(
    string Host = "",
    string Port = "587",
    string User = "",
    string Password = "",
    string From = "",
    string To = "",
    string Subject = "",
    bool RememberLogin = true)
{
    /// <summary>
    /// Hides the password so the settings can be logged safely.
    /// </summary>
    /// <returns>All fields except the password.</returns>
    /// <example>
    /// <code>
    /// var text = settings.ToString();
    /// </code>
    /// </example>
    public override string ToString() =>
        $"MailSettings {{ Host = {Host}, Port = {Port}, User = {User}, Password = ***, From = {From}, To = {To}, Subject = {Subject}, RememberLogin = {RememberLogin} }}";
}
