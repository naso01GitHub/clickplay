namespace ClickPlay.Core.Mail;

/// <summary>
/// One SMTP port choice for the Port drop-down.
/// </summary>
/// <param name="Port">Port number.</param>
/// <param name="Security">How the connection is secured on this port, e.g. "SSL" or "STARTTLS".</param>
/// <example>
/// <code>
/// var option = new SmtpPortOption(465, "SSL"); // shown as "465 (SSL)"
/// </code>
/// </example>
public sealed record SmtpPortOption(int Port, string Security)
{
    /// <summary>
    /// Text shown in the drop-down.
    /// </summary>
    /// <returns>For example <c>465 (SSL)</c>.</returns>
    /// <example>
    /// <code>
    /// var text = new SmtpPortOption(587, "STARTTLS").ToString(); // "587 (STARTTLS)"
    /// </code>
    /// </example>
    public override string ToString() => $"{Port} ({Security})";
}

/// <summary>
/// A known mail provider for the Host drop-down.
/// </summary>
/// <param name="Name">Provider name shown to the user, e.g. "Gmail".</param>
/// <param name="Host">SMTP server name.</param>
/// <param name="Ports">Ports this provider accepts; the first one is filled in when the provider is picked.</param>
/// <param name="Note">Login hint shown as a tooltip, e.g. that an app password is required.</param>
/// <example>
/// <code>
/// var gmail = SmtpPresets.FindByHost("smtp.gmail.com"); // Gmail preset
/// </code>
/// </example>
public sealed record SmtpPreset(string Name, string Host, IReadOnlyList<SmtpPortOption> Ports, string Note)
{
    /// <summary>The port filled in when this provider is picked.</summary>
    public int DefaultPort => Ports[0].Port;

    /// <summary>
    /// Text shown in the drop-down.
    /// </summary>
    /// <returns>For example <c>Gmail (smtp.gmail.com)</c>.</returns>
    /// <example>
    /// <code>
    /// var text = SmtpPresets.All[0].ToString(); // "Gmail (smtp.gmail.com)"
    /// </code>
    /// </example>
    public override string ToString() => $"{Name} ({Host})";
}

/// <summary>
/// Popular mail providers and common SMTP ports offered in the Email section. Host and Port stay editable,
/// so any other server can still be typed in.
/// </summary>
public static class SmtpPresets
{
    private static readonly SmtpPortOption Ssl465 = new(465, "SSL");
    private static readonly SmtpPortOption StartTls587 = new(587, "STARTTLS");
    private static readonly SmtpPortOption Plain25 = new(25, "plain / STARTTLS");

    /// <summary>
    /// Known providers, most common first: Gmail, Yahoo, then others.
    /// </summary>
    public static IReadOnlyList<SmtpPreset> All { get; } =
    [
        new("Gmail", "smtp.gmail.com", [StartTls587, Ssl465],
            "Needs 2-Step Verification and an app password (Google Account → Security → App passwords)."),
        new("Yahoo", "smtp.mail.yahoo.com", [Ssl465, StartTls587],
            "Needs an app password (Yahoo Account security → Generate app password)."),
        new("Outlook / Hotmail", "smtp-mail.outlook.com", [StartTls587],
            "Microsoft may require modern authentication; an app password works for accounts with 2-step verification."),
        new("iCloud Mail", "smtp.mail.me.com", [StartTls587],
            "Needs an app-specific password (appleid.apple.com → Sign-In and Security)."),
        new("ABV.bg", "smtp.abv.bg", [Ssl465],
            "Use the full address as User."),
    ];

    /// <summary>
    /// Ports offered when the host is not a known provider.
    /// </summary>
    public static IReadOnlyList<SmtpPortOption> CommonPorts { get; } = [Ssl465, StartTls587, Plain25];

    /// <summary>
    /// Finds the provider for a host name, ignoring case and spaces.
    /// </summary>
    /// <param name="host">Host text as typed.</param>
    /// <returns>The matching preset, or null for any other server.</returns>
    /// <example>
    /// <code>
    /// var yahoo = SmtpPresets.FindByHost(" SMTP.mail.yahoo.com "); // Yahoo preset
    /// var none = SmtpPresets.FindByHost("mail.example.com");     // null
    /// </code>
    /// </example>
    public static SmtpPreset? FindByHost(string? host)
    {
        var key = host?.Trim();
        return string.IsNullOrEmpty(key)
            ? null
            : All.FirstOrDefault(p => string.Equals(p.Host, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ports to offer for a host: the provider's own ports for a known host, otherwise <see cref="CommonPorts"/>.
    /// </summary>
    /// <param name="host">Host text as typed.</param>
    /// <returns>Port choices for the Port drop-down.</returns>
    /// <example>
    /// <code>
    /// var ports = SmtpPresets.PortsFor("smtp.mail.yahoo.com"); // 465 (SSL), 587 (STARTTLS)
    /// </code>
    /// </example>
    public static IReadOnlyList<SmtpPortOption> PortsFor(string? host) =>
        FindByHost(host)?.Ports ?? CommonPorts;
}
