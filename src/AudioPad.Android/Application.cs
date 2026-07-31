using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using AudioPad.Audio;
using AudioPad.Core.Persistence;
using AudioPad.UI;
using AudioPad.UI.ViewModels;

namespace AudioPad.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        private LibVlcAudioEngine? _audioEngine;

        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            var setupRepository = new SetupRepository();
            var setupPath = SetupRepository.GetDefaultSetupPath();
            var setup = setupRepository.LoadSetup(setupPath);
            _audioEngine = new LibVlcAudioEngine();

            App.ViewModelFactory = () => new MainWindowViewModel(setup, _audioEngine, setupRepository, setupPath);

            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
