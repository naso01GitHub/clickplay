namespace ClickPlay.Core.Mail;

/// <summary>
/// Keeps one secret (the SMTP password) outside plain files. The app uses <see cref="ProtectedSecretStore"/>.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Reads the stored secret.
    /// </summary>
    /// <returns>The secret, or empty when nothing is stored or it cannot be read.</returns>
    /// <example>
    /// <code>
    /// var password = store.Read();
    /// </code>
    /// </example>
    string Read();

    /// <summary>
    /// Replaces the stored secret. An empty value clears it.
    /// </summary>
    /// <param name="secret">New secret.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// store.Write("app-password");
    /// </code>
    /// </example>
    void Write(string secret);
}
