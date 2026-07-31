using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AudioPad.UI.ViewModels;

namespace AudioPad.UI.Views;

public partial class PadConfigView : UserControl
{
    private static readonly FilePickerFileType AudioFileType = new("Audio files")
    {
        Patterns = ["*.mp3", "*.wav", "*.ogg", "*.flac", "*.m4a", "*.aac"],
    };

    public PadConfigView()
    {
        InitializeComponent();
    }

    private async void OnBrowseAudioClick(object? sender, RoutedEventArgs e)
    {
        var path = await PickFilePathAsync("Choose an audio file", AudioFileType, "audio");
        if (path is not null && DataContext is PadConfigViewModel vm)
        {
            vm.AudioFilePath = path;
        }
    }

    private async void OnBrowseIconClick(object? sender, RoutedEventArgs e)
    {
        var path = await PickFilePathAsync("Choose an icon image", FilePickerFileTypes.ImageAll, "icons");
        if (path is not null && DataContext is PadConfigViewModel vm)
        {
            vm.IconPath = path;
        }
    }

    private async Task<string?> PickFilePathAsync(string title, FilePickerFileType fileType, string importSubfolder)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [fileType],
        });

        if (files.Count == 0)
        {
            return null;
        }

        var file = files[0];

        // A real filesystem path (typical on Desktop) can be used as-is. Anything else — e.g. an
        // Android `content://` URI from the Storage Access Framework — isn't something native
        // playback code can open directly, so import a copy into app-private storage instead.
        return file.Path.IsFile ? file.Path.LocalPath : await ImportFileAsync(file, importSubfolder);
    }

    private static async Task<string> ImportFileAsync(IStorageFile file, string subfolder)
    {
        var importDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioPad", subfolder);
        Directory.CreateDirectory(importDir);

        var destinationPath = Path.Combine(importDir, $"{Guid.NewGuid()}{Path.GetExtension(file.Name)}");

        await using var source = await file.OpenReadAsync();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);

        return destinationPath;
    }
}
