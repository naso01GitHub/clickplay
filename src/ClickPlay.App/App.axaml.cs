using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClickPlay.App.ViewModels;
using ClickPlay.App.Views;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Services;

namespace ClickPlay.App;

public partial class App : Application
{
    private ClickToPlayBrowserSession? _session;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _session = new ClickToPlayBrowserSession();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(new PlaywrightClickToPlayCatalog(_session)),
            };
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _session = null;
    }
}
