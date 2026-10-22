using System.Collections.ObjectModel;
using ClickPlay.App.Platform;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Mail;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickPlay.App.ViewModels;

/// <summary>
/// Main window ViewModel — mail part: SMTP fields and provider/port drop-downs, Remember login,
/// loading and saving Email settings, and mailing new Results after a scan.
/// </summary>
public partial class MainViewModel
{
    private readonly IMailSender? _mailSender;
    private readonly MailSettingsStore? _mailSettingsStore;

    [ObservableProperty]
    private string _smtpHost = string.Empty;

    [ObservableProperty]
    private string _smtpPort = "587";

    /// <summary>Known mail providers for the Host drop-down (Gmail, Yahoo, …).</summary>
    public IReadOnlyList<SmtpPreset> SmtpHostPresets => SmtpPresets.All;

    /// <summary>
    /// Provider picked in the Host drop-down. Picking one fills Host and its usual Port.
    /// Follows the Host text: a typed known host selects its provider, any other host clears it.
    /// </summary>
    [ObservableProperty]
    private SmtpPreset? _selectedSmtpPreset;

    /// <summary>Port choices for the Port drop-down: the provider's ports, or common ports for other hosts.</summary>
    public IReadOnlyList<SmtpPortOption> SmtpPortOptions => SmtpPresets.PortsFor(SmtpHost);

    /// <summary>
    /// Port picked in the Port drop-down. Picking one fills Port. Follows the Port text.
    /// </summary>
    [ObservableProperty]
    private SmtpPortOption? _selectedSmtpPortOption;

    private bool _syncingSmtpPicks;

    [ObservableProperty]
    private string _smtpUser = string.Empty;

    [ObservableProperty]
    private string _smtpPassword = string.Empty;

    /// <summary>
    /// "Remember login": when true, User and Password are saved and filled in at the next start.
    /// Turning it off erases the saved login at once; the fields keep their values for this session.
    /// </summary>
    [ObservableProperty]
    private bool _rememberLogin = true;

    private bool _loadingMailSettings;

    [ObservableProperty]
    private string _mailFrom = string.Empty;

    /// <summary>
    /// Recipient addresses, one per line (commas or semicolons also accepted).
    /// </summary>
    [ObservableProperty]
    private string _mailTo = string.Empty;

    /// <summary>
    /// Mail subject prefix. The sent subject is this text + the selected sport + the number of records in the mail, e.g. "Free padel Падел (3)".
    /// Editable while Search runs; read when the mail is sent.
    /// </summary>
    [ObservableProperty]
    private string _mailSubject = string.Empty;

    /// <summary>
    /// Parsed To recipients in order, one address per entry.
    /// </summary>
    public IReadOnlyList<string> MailRecipients => EmailRecipientParser.Parse(MailTo);

    /// <summary>
    /// Called by CommunityToolkit when the Host text changes: refreshes the port choices and
    /// selects the matching provider (or none) without overwriting what was typed.
    /// </summary>
    /// <param name="value">New Host text.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SmtpHost = "smtp.gmail.com"; // SelectedSmtpPreset becomes Gmail
    /// </code>
    /// </example>
    partial void OnSmtpHostChanged(string value)
    {
        OnPropertyChanged(nameof(SmtpPortOptions));
        SyncSmtpPicks();
    }

    /// <summary>
    /// Called by CommunityToolkit when the Port text changes: selects the matching port choice (or none).
    /// </summary>
    /// <param name="value">New Port text.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SmtpPort = "465"; // SelectedSmtpPortOption becomes 465 (SSL)
    /// </code>
    /// </example>
    partial void OnSmtpPortChanged(string value) => SyncSmtpPicks();

    /// <summary>
    /// Called by CommunityToolkit when "Remember login" changes. Turning it off saves at once, which erases
    /// the stored User and Password; the fields keep their values so the current session still works.
    /// </summary>
    /// <param name="value">New checkbox state.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.RememberLogin = false; // saved login erased now; fields empty at the next start
    /// </code>
    /// </example>
    partial void OnRememberLoginChanged(bool value)
    {
        if (_loadingMailSettings || value)
        {
            return;
        }

        SaveMailSettings();
        StatusMessage = "Saved login erased. User and Password will be empty at the next start.";
    }

    /// <summary>
    /// Called by CommunityToolkit when a provider is picked: fills Host and the provider's usual Port.
    /// </summary>
    /// <param name="value">Picked provider; null (cleared) changes nothing.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SelectedSmtpPreset = SmtpPresets.FindByHost("smtp.mail.yahoo.com"); // Host + Port 465
    /// </code>
    /// </example>
    partial void OnSelectedSmtpPresetChanged(SmtpPreset? value)
    {
        if (_syncingSmtpPicks || value is null)
        {
            return;
        }

        SmtpHost = value.Host;
        SmtpPort = value.DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Called by CommunityToolkit when a port is picked: fills Port.
    /// </summary>
    /// <param name="value">Picked port; null (cleared) changes nothing.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SelectedSmtpPortOption = viewModel.SmtpPortOptions[1]; // Port = 587
    /// </code>
    /// </example>
    partial void OnSelectedSmtpPortOptionChanged(SmtpPortOption? value)
    {
        if (_syncingSmtpPicks || value is null)
        {
            return;
        }

        SmtpPort = value.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets both drop-downs to match the Host and Port text, without writing back to the text boxes.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// SyncSmtpPicks(); // after loading saved settings
    /// </code>
    /// </example>
    private void SyncSmtpPicks()
    {
        _syncingSmtpPicks = true;
        try
        {
            SelectedSmtpPreset = SmtpPresets.FindByHost(SmtpHost);
            var port = int.TryParse(SmtpPort?.Trim(), out var p) ? p : -1;
            SelectedSmtpPortOption = SmtpPortOptions.FirstOrDefault(o => o.Port == port);
        }
        finally
        {
            _syncingSmtpPicks = false;
        }
    }

    partial void OnMailToChanged(string value) => OnPropertyChanged(nameof(MailRecipients));

    /// <summary>
    /// After a scan with new Results records, sends one mail with all of them to every To address.
    /// To and Subject are read now, so edits made during Search apply. A mail failure never stops Search.
    /// Adds a "Mailed …", "Mail skipped …", or "Mail failed …" line to Results; a failure also goes to the errors log.
    /// </summary>
    /// <param name="smtp">SMTP settings snapshotted when Search started; null means mail is off.</param>
    /// <param name="sport">Sport the Search runs for; goes into the subject.</param>
    /// <param name="newSlots">New slots of this scan; nothing is sent when empty.</param>
    /// <param name="cancellationToken">Stop button; cancels sending.</param>
    /// <returns>A task that completes when the mail was sent, skipped, or failed.</returns>
    /// <exception cref="OperationCanceledException">Stop was pressed while sending.</exception>
    /// <example>
    /// <code>
    /// await MailNewResultsAsync(smtp, sport, newSlots, cancellationToken);
    /// </code>
    /// </example>
    private async Task MailNewResultsAsync(
        SmtpSettings? smtp,
        Sport sport,
        IReadOnlyList<AvailabilitySlot> newSlots,
        CancellationToken cancellationToken)
    {
        if (smtp is null || _mailSender is null || newSlots.Count == 0)
        {
            return;
        }

        var recipients = MailRecipients;
        string note;
        var failed = false;
        if (recipients.Count == 0)
        {
            note = "Mail skipped: no To address.";
        }
        else
        {
            StatusMessage = $"Mailing {newSlots.Count} new free hour(s)…";
            try
            {
                await _mailSender.SendAsync(
                        smtp,
                        recipients,
                        ResultsMailFormatter.Subject(MailSubject, sport.Name, newSlots.Count),
                        ResultsMailFormatter.Body(FormatScanStamp(ScanClock()), newSlots),
                        cancellationToken)
                    .ConfigureAwait(true);
                note = $"Mailed {newSlots.Count} new free hour(s) to {recipients.Count} address(es).";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                note = $"Mail failed: {ex.GetType().Name}: {ex.Message}";
                failed = true;
            }
        }

        StatusMessage = note;
        var line = $"{FormatScanStamp(ScanClock())} {note}";
        if (Results.Count > 0 && Results[^1].Length == 0)
        {
            Results.Insert(Results.Count - 1, line);
        }
        else
        {
            Results.Add(line);
        }

        if (failed)
        {
            LogError(note);
        }
    }

    /// <summary>
    /// Fills the Email fields (Host, Port, User, Password, From, To, Subject) from the saved settings.
    /// Does nothing when no settings store was given.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// // Called once from the constructor.
    /// LoadMailSettings();
    /// </code>
    /// </example>
    private void LoadMailSettings()
    {
        if (_mailSettingsStore is null)
        {
            return;
        }

        var saved = _mailSettingsStore.Load();
        _loadingMailSettings = true;
        try
        {
            SmtpHost = saved.Host;
            SmtpPort = saved.Port;
            SmtpUser = saved.User;
            SmtpPassword = saved.Password;
            MailFrom = saved.From;
            MailTo = saved.To;
            MailSubject = saved.Subject;
            RememberLogin = saved.RememberLogin;
        }
        finally
        {
            _loadingMailSettings = false;
        }
    }

    /// <summary>
    /// Saves the Email section: the password to the OS secure store, the rest to <c>smtp.settings.json</c>.
    /// Called when Search starts and when the app exits.
    /// </summary>
    /// <example>
    /// <code>
    /// viewModel.SaveMailSettings();
    /// </code>
    /// </example>
    public void SaveMailSettings()
    {
        if (_mailSettingsStore is null)
        {
            return;
        }

        try
        {
            _mailSettingsStore.Save(new MailSettings(
                SmtpHost, SmtpPort, SmtpUser, SmtpPassword, MailFrom, MailTo, MailSubject, RememberLogin));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save email settings: {ex.Message}";
            LogError(StatusMessage);
        }
    }
}
