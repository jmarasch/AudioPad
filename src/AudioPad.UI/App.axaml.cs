using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AudioPad.UI.ViewModels;
using AudioPad.UI.Views;

namespace AudioPad.UI;

public partial class App : Application
{
    /// <summary>
    /// Set by the platform head (Desktop/Android) before framework init, so it can supply a
    /// <see cref="MainWindowViewModel"/> wired to the real audio engine. Falls back to a
    /// design-time/no-op engine when unset (e.g. the XAML previewer).
    /// </summary>
    public static Func<MainWindowViewModel>? ViewModelFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = CreateMainWindowViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = CreateMainWindowViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = CreateMainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindowViewModel CreateMainWindowViewModel() => ViewModelFactory?.Invoke() ?? new MainWindowViewModel();
}
