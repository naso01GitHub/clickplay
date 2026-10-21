using ClickPlay.Core.Mail;

namespace ClickPlay.Tests.Fakes;

/// <summary>
/// In-memory secret store for tests; nothing touches the OS secure store.
/// </summary>
public sealed class FakeSecretStore : ISecretStore
{
    /// <summary>The stored secret (empty when none).</summary>
    public string Secret { get; private set; } = string.Empty;

    /// <summary>
    /// Returns the stored secret.
    /// </summary>
    /// <returns>The secret, or empty.</returns>
    /// <example>
    /// <code>
    /// var store = new FakeSecretStore();
    /// store.Write("pw");
    /// Assert.Equal("pw", store.Read());
    /// </code>
    /// </example>
    public string Read() => Secret;

    /// <summary>
    /// Replaces the stored secret; null becomes empty.
    /// </summary>
    /// <param name="secret">New secret.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// new FakeSecretStore().Write("pw");
    /// </code>
    /// </example>
    public void Write(string secret) => Secret = secret ?? string.Empty;
}
