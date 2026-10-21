using System.Text;
using ClickPlay.Core.Browser;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ClickPlay.Core.Mail;

/// <summary>
/// Stores the SMTP password with the operating system's protection, through Microsoft's cross-platform
/// MSAL extensions storage: on Windows a file under <c>%AppData%/ClickPlay</c> encrypted with DPAPI
/// (only the same Windows user can decrypt it); on Mac a Keychain item <c>ClickPlay / smtp-password</c>.
/// </summary>
public sealed class ProtectedSecretStore : ISecretStore
{
    private const string FileName = "smtp.password.bin";
    private const string KeychainService = "ClickPlay";
    private const string KeychainAccount = "smtp-password";

    private readonly Storage _storage;

    /// <summary>
    /// Creates the store in the ClickPlay app-data folder.
    /// </summary>
    /// <example>
    /// <code>
    /// ISecretStore store = new ProtectedSecretStore();
    /// </code>
    /// </example>
    public ProtectedSecretStore()
    {
        var properties = new StorageCreationPropertiesBuilder(FileName, AppPaths.SettingsDirectory())
            .WithMacKeyChain(KeychainService, KeychainAccount)
            .Build();
        _storage = Storage.Create(properties);
    }

    /// <summary>
    /// Decrypts and returns the stored password.
    /// </summary>
    /// <returns>The password, or empty when nothing is stored or it cannot be decrypted (e.g. another Windows user).</returns>
    /// <example>
    /// <code>
    /// var password = new ProtectedSecretStore().Read();
    /// </code>
    /// </example>
    public string Read()
    {
        try
        {
            var data = _storage.ReadData();
            return data is { Length: > 0 } ? Encoding.UTF8.GetString(data) : string.Empty;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Encrypts and stores the password, replacing the old one. An empty value deletes the stored password.
    /// </summary>
    /// <param name="secret">New password; empty or null clears it.</param>
    /// <returns>No return value.</returns>
    /// <exception cref="Exception">The OS store cannot be written (rare; e.g. Keychain locked).</exception>
    /// <example>
    /// <code>
    /// new ProtectedSecretStore().Write("app-password");
    /// </code>
    /// </example>
    public void Write(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            _storage.Clear(ignoreExceptions: true);
            return;
        }

        _storage.WriteData(Encoding.UTF8.GetBytes(secret));
    }
}
