using System;
using Avalonia;
using AudioPad.Audio;
using AudioPad.Core.Persistence;
using AudioPad.UI;
using AudioPad.UI.ViewModels;

namespace AudioPad.Desktop;

sealed class Program
{
    private static LibVlcAudioEngine? _audioEngine;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var setupRepository = new SetupRepository();
        var setupPath = SetupRepository.GetDefaultSetupPath();
        var setup = setupRepository.LoadSetup(setupPath);
        _audioEngine = new LibVlcAudioEngine();

        App.ViewModelFactory = () => new MainWindowViewModel(setup, _audioEngine, setupRepository, setupPath);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _audioEngine.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
