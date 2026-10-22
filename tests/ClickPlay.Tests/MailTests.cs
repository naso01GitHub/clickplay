using ClickPlay.App.ViewModels;
using ClickPlay.Core.Mail;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Tests.Fakes;

namespace ClickPlay.Tests;

public sealed class MailTests
{
    private static readonly AvailabilitySlot First = new(
        "Supersport", new DateOnly(2026, 10, 23), new TimeOnly(18, 0), new TimeOnly(19, 0), "Падел 1", null, "23.10.2026");

    private static readonly AvailabilitySlot Second = new(
        "BETVAM Padel Club", new DateOnly(2026, 10, 23), new TimeOnly(20, 0), new TimeOnly(21, 0), "Падел 2");

    [Fact]
    public void Subject_PrefixSportAndRecordCount()
    {
        Assert.Equal("Free padel Падел (3)", ResultsMailFormatter.Subject("  Free padel ", " Падел ", 3));
        Assert.Equal("Free padel Падел (1)", ResultsMailFormatter.Subject("Free padel", "Падел", 1));
    }

    [Fact]
    public void Subject_EmptyPrefixOrSport()
    {
        Assert.Equal("ClickPlay Тенис (2)", ResultsMailFormatter.Subject("", "Тенис", 2));
        Assert.Equal("Free (2)", ResultsMailFormatter.Subject("Free", "", 2));
    }

    [Fact]
    public void Subject_CountBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResultsMailFormatter.Subject("Free", "Падел", 0));
    }

    [Fact]
    public void Subject_RemovesLineBreaks()
    {
        var subject = ResultsMailFormatter.Subject("Free\r\nBcc: x@evil.com", "Падел", 1);

        Assert.DoesNotContain('\r', subject);
        Assert.DoesNotContain('\n', subject);
    }

    [Fact]
    public void Body_HasStampAndResultsLines()
    {
        var body = ResultsMailFormatter.Body("21.10.2026 21:05:17", [First, Second]);

        var nl = Environment.NewLine;
        Assert.Equal(
            $"21.10.2026 21:05:17{nl}{nl}{First.ToLogLine()}{nl}{nl}{Second.ToLogLine()}{nl}",
            body);
    }

    [Fact]
    public void Presets_StartWithGmailAndYahoo_AndFindByHostIgnoresCase()
    {
        Assert.Equal("Gmail", SmtpPresets.All[0].Name);
        Assert.Equal("Yahoo", SmtpPresets.All[1].Name);
        Assert.Equal("Yahoo", SmtpPresets.FindByHost(" SMTP.Mail.Yahoo.com ")!.Name);
        Assert.Null(SmtpPresets.FindByHost("mail.example.com"));
        Assert.Equal(SmtpPresets.CommonPorts, SmtpPresets.PortsFor("mail.example.com"));
    }

    [Fact]
    public void PickingProvider_FillsHostAndPort_PickingPort_FillsPort()
    {
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()));

        vm.SelectedSmtpPreset = SmtpPresets.FindByHost("smtp.mail.yahoo.com");
        Assert.Equal("smtp.mail.yahoo.com", vm.SmtpHost);
        Assert.Equal("465", vm.SmtpPort);
        Assert.Equal(465, vm.SelectedSmtpPortOption!.Port);

        vm.SelectedSmtpPortOption = vm.SmtpPortOptions.Single(o => o.Port == 587);
        Assert.Equal("587", vm.SmtpPort);
        Assert.Equal("smtp.mail.yahoo.com", vm.SmtpHost);
    }

    [Fact]
    public void TypingHostAndPort_StaysFreeText_AndSyncsDropDowns()
    {
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()));

        vm.SmtpHost = "smtp.gmail.com";
        Assert.Equal("Gmail", vm.SelectedSmtpPreset!.Name);

        vm.SmtpHost = "mail.example.com";
        vm.SmtpPort = "2525";
        Assert.Null(vm.SelectedSmtpPreset);
        Assert.Null(vm.SelectedSmtpPortOption);
        Assert.Equal("mail.example.com", vm.SmtpHost);
        Assert.Equal("2525", vm.SmtpPort);
        Assert.Equal(SmtpPresets.CommonPorts, vm.SmtpPortOptions);
    }

    [Fact]
    public void SavedHost_SelectsProviderOnStartup()
    {
        var dir = TempDir();
        var secrets = new FakeSecretStore();
        new MailSettingsStore(secrets, dir).Save(new MailSettings("smtp.gmail.com", "465"));

        var vm = new MainViewModel(
            new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()),
            new FakeMailSender(),
            new MailSettingsStore(secrets, dir));

        Assert.Equal("Gmail", vm.SelectedSmtpPreset!.Name);
        Assert.Equal(465, vm.SelectedSmtpPortOption!.Port);
        Assert.Equal("465", vm.SmtpPort);
    }

    [Fact]
    public async Task MailExpanded_HidesLocations()
    {
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()));
        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        Assert.True(vm.IsLocationsPanelVisible);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.IsMailExpanded = true;

        Assert.False(vm.IsLocationsPanelVisible);
        Assert.Contains(nameof(MainViewModel.IsLocationsPanelVisible), changed);

        vm.IsMailExpanded = false;
        Assert.True(vm.IsLocationsPanelVisible);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("abc")]
    public void Validator_RejectsBadPort(string port)
    {
        Assert.False(SmtpSettingsValidator.TryCreate("smtp.x.com", port, "u", "p", "me@x.com", out var smtp, out var error));
        Assert.Null(smtp);
        Assert.Contains("port", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("@x.com")]
    public void Validator_RejectsBadFrom(string from)
    {
        Assert.False(SmtpSettingsValidator.TryCreate("smtp.x.com", "587", "u", "p", from, out _, out var error));
        Assert.Contains("From", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RequiresPasswordWhenUserSet()
    {
        Assert.False(SmtpSettingsValidator.TryCreate("smtp.x.com", "587", "u", "", "me@x.com", out _, out _));
    }

    [Fact]
    public void Validator_AcceptsGoodSettings_AndToStringHidesPassword()
    {
        Assert.True(SmtpSettingsValidator.TryCreate(" smtp.x.com ", "465", "me@x.com", "secret-pw", "me@x.com", out var smtp, out _));
        Assert.Equal(new SmtpSettings("smtp.x.com", 465, "me@x.com", "secret-pw", "me@x.com"), smtp);
        Assert.DoesNotContain("secret-pw", smtp!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Store_RoundTrips_AndJsonHasNoPassword()
    {
        var dir = TempDir();
        var secrets = new FakeSecretStore();
        var store = new MailSettingsStore(secrets, dir);
        var settings = new MailSettings("smtp.x.com", "465", "me@x.com", "secret-pw", "me@x.com", "a@b.com\nc@d.com", "Free padel");

        store.Save(settings);

        Assert.Equal(settings, new MailSettingsStore(secrets, dir).Load());
        Assert.Equal("secret-pw", secrets.Secret);
        Assert.DoesNotContain("secret-pw", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Store_RememberLoginOff_ErasesLogin_KeepsOtherFields()
    {
        var dir = TempDir();
        var secrets = new FakeSecretStore();
        var store = new MailSettingsStore(secrets, dir);
        store.Save(new MailSettings("smtp.x.com", "465", "me@x.com", "secret-pw", "me@x.com", "a@b.com", "Free"));

        store.Save(new MailSettings("smtp.x.com", "465", "me@x.com", "secret-pw", "me@x.com", "a@b.com", "Free", RememberLogin: false));

        Assert.Equal(string.Empty, secrets.Secret);
        Assert.DoesNotContain("me@x.com\",\n  \"from", File.ReadAllText(store.FilePath).Replace("\r", ""), StringComparison.Ordinal);
        var loaded = new MailSettingsStore(secrets, dir).Load();
        Assert.Equal(new MailSettings("smtp.x.com", "465", "", "", "me@x.com", "a@b.com", "Free", RememberLogin: false), loaded);
    }

    [Fact]
    public void Store_OldFileWithoutFlag_RemembersLogin()
    {
        var dir = TempDir();
        var secrets = new FakeSecretStore();
        secrets.Write("pw");
        var store = new MailSettingsStore(secrets, dir);
        File.WriteAllText(store.FilePath, """{ "host": "smtp.x.com", "user": "me@x.com" }""");

        var loaded = store.Load();

        Assert.True(loaded.RememberLogin);
        Assert.Equal("me@x.com", loaded.User);
        Assert.Equal("pw", loaded.Password);
    }

    [Fact]
    public void ViewModel_UncheckRememberLogin_ErasesNow_KeepsFieldsForSession()
    {
        var dir = TempDir();
        var secrets = new FakeSecretStore();
        new MailSettingsStore(secrets, dir).Save(new MailSettings("smtp.x.com", "465", "me@x.com", "pw", "me@x.com"));
        var vm = new MainViewModel(
            new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()),
            new FakeMailSender(),
            new MailSettingsStore(secrets, dir));
        Assert.True(vm.RememberLogin);

        vm.RememberLogin = false;

        Assert.Equal(string.Empty, secrets.Secret);
        Assert.Equal("me@x.com", vm.SmtpUser);
        Assert.Equal("pw", vm.SmtpPassword);

        var restarted = new MainViewModel(
            new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()),
            new FakeMailSender(),
            new MailSettingsStore(secrets, dir));
        Assert.False(restarted.RememberLogin);
        Assert.Equal(string.Empty, restarted.SmtpUser);
        Assert.Equal(string.Empty, restarted.SmtpPassword);
        Assert.Equal("smtp.x.com", restarted.SmtpHost);
    }

    [Fact]
    public void Store_CorruptJson_LoadsDefaults()
    {
        var dir = TempDir();
        var store = new MailSettingsStore(new FakeSecretStore(), dir);
        File.WriteAllText(store.FilePath, "{ not json");

        Assert.Equal(new MailSettings(), store.Load());
    }

    [Fact]
    public void ViewModel_LoadsSavedSettings()
    {
        var dir = TempDir();
        var secrets = new FakeSecretStore();
        new MailSettingsStore(secrets, dir).Save(
            new MailSettings("smtp.x.com", "465", "me@x.com", "pw", "me@x.com", "a@b.com", "Free padel"));

        var vm = new MainViewModel(
            new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()),
            new FakeMailSender(),
            new MailSettingsStore(secrets, dir));

        Assert.Equal("smtp.x.com", vm.SmtpHost);
        Assert.Equal("pw", vm.SmtpPassword);
        Assert.Equal("Free padel", vm.MailSubject);
        Assert.Equal(["a@b.com"], vm.MailRecipients);
    }

    [Fact]
    public async Task Search_NewResults_OneMailToAllRecipients_NoMailWhenNothingNew()
    {
        var session = new FakeClickToPlaySession { Availability = [First, Second] };
        var sender = new FakeMailSender();
        var vm = MailViewModel(session, sender);

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => session.SearchCallCount >= 2 && sender.Sent.Count >= 1);

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(["a@b.com", "c@d.com"], mail.To);
        Assert.Equal("Free padel Падел (2)", mail.Subject);
        Assert.Contains(First.ToLogLine(), mail.Body, StringComparison.Ordinal);
        Assert.Contains(vm.Results, line => line.Contains("Mailed 2 new free hour(s) to 2 address(es).", StringComparison.Ordinal));
        Assert.Equal(string.Empty, vm.Results[^1]);

        vm.StopCommand.Execute(null);
        await search;
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task Search_ToAndSubjectEditedDuringSearch_UsedForNextMail()
    {
        var session = new FakeClickToPlaySession
        {
            AvailabilityForCall = call => call == 1 ? [First] : [First, Second]
        };
        var sender = new FakeMailSender();
        var vm = MailViewModel(session, sender);
        vm.SearchLoopDelayOverride = TimeSpan.FromMilliseconds(300);

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => sender.Sent.Count >= 1);
        vm.MailTo = "new@x.com";
        vm.MailSubject = "Changed";
        await WaitUntil(() => sender.Sent.Count >= 2);

        Assert.Equal(["new@x.com"], sender.Sent[1].To);
        Assert.Equal("Changed Падел (1)", sender.Sent[1].Subject);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_MailFails_SearchKeepsRunning_AndLogsFailure()
    {
        var session = new FakeClickToPlaySession
        {
            AvailabilityForCall = call => call == 1 ? [First] : [First, Second]
        };
        var sender = new FakeMailSender { Throw = new InvalidOperationException("auth failed") };
        var vm = MailViewModel(session, sender);

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => session.SearchCallCount >= 3 && sender.Attempts >= 2);

        Assert.True(vm.IsSearching);
        Assert.Contains("Mail failed: InvalidOperationException: auth failed", File.ReadAllText(vm.ErrorsLogPath), StringComparison.Ordinal);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_SmtpHostEmpty_NoMail()
    {
        var session = new FakeClickToPlaySession { Availability = [First] };
        var sender = new FakeMailSender();
        var vm = MailViewModel(session, sender);
        vm.SmtpHost = string.Empty;

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => session.SearchCallCount >= 2);

        Assert.Equal(0, sender.Attempts);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_InvalidSmtp_DoesNotStart()
    {
        var session = new FakeClickToPlaySession();
        var vm = MailViewModel(session, new FakeMailSender());
        vm.SmtpPort = "abc";

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(0, session.SearchCallCount);
        Assert.StartsWith("Email: SMTP port", vm.StatusMessage, StringComparison.Ordinal);
    }

    private static MainViewModel MailViewModel(FakeClickToPlaySession session, FakeMailSender sender) =>
        new(new PlaywrightClickToPlayCatalog(session), sender, new MailSettingsStore(new FakeSecretStore(), TempDir()))
        {
            SearchLoopDelayOverride = TimeSpan.FromMilliseconds(80),
            ErrorsLogPath = Path.Combine(TempDir(), "errors-log.txt"),
            SmtpHost = "smtp.x.com",
            SmtpPort = "587",
            SmtpUser = "me@x.com",
            SmtpPassword = "pw",
            MailFrom = "me@x.com",
            MailTo = "a@b.com\nc@d.com",
            MailSubject = "Free padel"
        };

    private static async Task<Task> StartSearchAsync(MainViewModel vm)
    {
        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;
        return vm.SearchCommand.ExecuteAsync(null);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"clickplay-mail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 120; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("Timed out waiting for search.");
    }
}
