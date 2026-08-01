using AudioPad.Core.Persistence;
using Avalonia.Controls;
using Avalonia.Input;
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

    /// <summary>
    /// Treats a press on the dimmed area around the panel as Cancel. The check that the press
    /// landed on the scrim itself, rather than on something inside the panel, is what stops an
    /// ordinary click on a dropdown from closing the dialog.
    /// </summary>
    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender) && DataContext is PadConfigViewModel viewModel)
        {
            viewModel.CancelCommand.Execute(null);
        }
    }

    private async void OnImportAudioClick(object? sender, RoutedEventArgs e)
    {
        if (await ImportFileAsync("Import an audio file", AudioFileType, MediaLibrary.AudioFolder) is not { } imported
            || DataContext is not PadConfigViewModel vm)
        {
            return;
        }

        vm.AudioFilePath = imported.Path;
        vm.AudioDisplayName = imported.Name;
    }

    private async void OnImportIconClick(object? sender, RoutedEventArgs e)
    {
        if (await ImportFileAsync("Import an icon image", FilePickerFileTypes.ImageAll, MediaLibrary.IconFolder) is not { } imported
            || DataContext is not PadConfigViewModel vm)
        {
            return;
        }

        vm.IconPath = imported.Path;
        vm.IconDisplayName = imported.Name;
    }

    /// <summary>
    /// Copies the chosen file into the app's media library and returns its new path.
    ///
    /// Always a copy, on every platform. Referencing the original only ever worked on desktop —
    /// Android hands back a content:// URI with no usable path — and left desktop boards silently
    /// dependent on folders the user is free to reorganise.
    /// </summary>
    private async Task<(string Path, string Name)?> ImportFileAsync(string title, FilePickerFileType fileType, string subfolder)
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

        await using var source = await files[0].OpenReadAsync();
        return (await MediaLibrary.ImportAsync(source, files[0].Name, subfolder), files[0].Name);
    }
}
