using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClickPlay.App.Platform;
using ClickPlay.App.ViewModels;
using ClickPlay.App.Views;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Mail;
using ClickPlay.Core.Services;

namespace ClickPlay.App;

public partial class App : Application
{
    private ClickToPlayBrowserSession? _session;
    private MainViewModel? _viewModel;
    private readonly SystemKeepAwake _keepAwake = new();

    /// <summary>
    /// Loads <c>App.axaml</c> (styles and resources).
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// // Called by Avalonia from Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args).
    /// </code>
    /// </example>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Creates the browser session, mail sender, and settings store, builds <see cref="MainViewModel"/>,
    /// shows the main window, and hooks app exit.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// // Called by Avalonia once the framework is ready; not called directly.
    /// </code>
    /// </example>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _session = new ClickToPlayBrowserSession();
            _viewModel = new MainViewModel(
                new PlaywrightClickToPlayCatalog(_session),
                new MailKitMailSender(),
                CreateMailSettingsStore(),
                _keepAwake);
            desktop.MainWindow = new MainWindow
            {
                DataContext = _viewModel,
            };
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Settings store with the OS-protected password. Null (mail settings kept in memory only) if the
    /// secure store cannot be opened, so the app still starts.
    /// </summary>
    /// <returns>The store, or null when the OS secure store is unavailable.</returns>
    /// <example>
    /// <code>
    /// var vm = new MainViewModel(catalog, new MailKitMailSender(), CreateMailSettingsStore());
    /// </code>
    /// </example>
    private static MailSettingsStore? CreateMailSettingsStore()
    {
        try
        {
            return new MailSettingsStore(new ProtectedSecretStore());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// On app exit: saves the Email settings, lets the PC sleep again, then closes Chromium and disposes the browser session.
    /// </summary>
    /// <param name="sender">The desktop lifetime.</param>
    /// <param name="e">Exit details (not used).</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// desktop.Exit += OnExit;
    /// </code>
    /// </example>
    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _viewModel?.SaveMailSettings();
        _keepAwake.Dispose();
        if (_session is null)
        {
            return;
        }

        _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _session = null;
    }
}
