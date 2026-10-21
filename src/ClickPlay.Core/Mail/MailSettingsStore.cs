using System.Text.Json;
using System.Text.Json.Serialization;
using ClickPlay.Core.Browser;

namespace ClickPlay.Core.Mail;

/// <summary>
/// Saves the Email section between runs. Non-secret fields go to <c>smtp.settings.json</c> in the
/// per-user settings folder; the password goes to an <see cref="ISecretStore"/> only.
/// </summary>
public sealed class MailSettingsStore
{
    private const string FileName = "smtp.settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISecretStore _secrets;
    private readonly string _filePath;

    /// <summary>
    /// Creates a store.
    /// </summary>
    /// <param name="secrets">Where the password is kept.</param>
    /// <param name="directory">Folder for the JSON file; null uses <see cref="AppPaths.SettingsDirectory"/>.</param>
    /// <example>
    /// <code>
    /// var store = new MailSettingsStore(new ProtectedSecretStore());
    /// </code>
    /// </example>
    public MailSettingsStore(ISecretStore secrets, string? directory = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        var dir = directory ?? AppPaths.SettingsDirectory();
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, FileName);
    }

    /// <summary>Full path of the JSON file (no password inside).</summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Loads saved settings. A missing or unreadable file gives default settings.
    /// When "remember login" was turned off, User and Password come back empty.
    /// </summary>
    /// <returns>Saved settings with the password from the secret store (or empty when not remembered).</returns>
    /// <example>
    /// <code>
    /// var settings = store.Load();
    /// </code>
    /// </example>
    public MailSettings Load()
    {
        var saved = new SavedFields();
        try
        {
            if (File.Exists(_filePath))
            {
                saved = JsonSerializer.Deserialize<SavedFields>(File.ReadAllText(_filePath), JsonOptions) ?? new SavedFields();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            saved = new SavedFields();
        }

        var remember = saved.RememberLogin ?? true;
        return new MailSettings(
            saved.Host ?? string.Empty,
            string.IsNullOrWhiteSpace(saved.Port) ? "587" : saved.Port,
            remember ? saved.User ?? string.Empty : string.Empty,
            remember ? _secrets.Read() : string.Empty,
            saved.From ?? string.Empty,
            saved.To ?? string.Empty,
            saved.Subject ?? string.Empty,
            remember);
    }

    /// <summary>
    /// Saves the settings: password to the secret store, the rest to JSON.
    /// When <see cref="MailSettings.RememberLogin"/> is false, User is not written and the stored password is erased.
    /// </summary>
    /// <param name="settings">Settings as typed in the GUI.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// store.Save(new MailSettings(Host: "smtp.mail.yahoo.com", Password: "pw"));
    /// </code>
    /// </example>
    public void Save(MailSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var remember = settings.RememberLogin;
        _secrets.Write(remember ? settings.Password : string.Empty);
        var saved = new SavedFields
        {
            Host = settings.Host,
            Port = settings.Port,
            User = remember ? settings.User : string.Empty,
            From = settings.From,
            To = settings.To,
            Subject = settings.Subject,
            RememberLogin = remember
        };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(saved, JsonOptions));
    }

    private sealed class SavedFields
    {
        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("port")]
        public string? Port { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }

        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        /// <summary>Missing in files saved before this option existed; treated as true.</summary>
        [JsonPropertyName("rememberLogin")]
        public bool? RememberLogin { get; set; }
    }
}
