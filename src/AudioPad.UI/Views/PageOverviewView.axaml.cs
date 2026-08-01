using AudioPad.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AudioPad.UI.Views;

public partial class PageOverviewView : UserControl
{
    /// <summary>
    /// One extension for both kinds of export. Which one a file holds is read from inside it on
    /// import, so there is nothing for the user to keep track of — see <c>ArchiveContents</c>.
    /// </summary>
    private static readonly FilePickerFileType ArchiveFileType = new("AudioPad export")
    {
        Patterns = ["*.audiopad"],
    };

    public PageOverviewView()
    {
        InitializeComponent();
    }

    private async void OnExportSetupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (await PickSaveFileAsync("Export everything", "AudioPad setup") is { } file)
        {
            await using var stream = await file.OpenWriteAsync();
            await viewModel.ExportSetupAsync(stream);
        }
    }

    private async void OnExportPageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SelectedOverviewPage: { } page } viewModel)
        {
            return;
        }

        if (await PickSaveFileAsync("Export page", page.Title) is { } file)
        {
            await using var stream = await file.OpenWriteAsync();
            await viewModel.ExportPageAsync(stream, page);
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import an AudioPad export",
            AllowMultiple = false,
            FileTypeFilter = [ArchiveFileType],
        });

        if (files.Count == 0)
        {
            return;
        }

        await using var stream = await files[0].OpenReadAsync();
        await viewModel.ImportArchiveAsync(stream);
    }

    /// <summary>
    /// Asks for somewhere to write an export. The result is an <see cref="IStorageFile"/> rather
    /// than a path because on Android it is usually a Storage Access Framework handle with no
    /// filesystem path at all — which is the whole reason <c>SetupArchive</c> works on streams.
    /// </summary>
    private async Task<IStorageFile?> PickSaveFileAsync(string title, string suggestedName)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return null;
        }

        return await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = "audiopad",
            FileTypeChoices = [ArchiveFileType],
            ShowOverwritePrompt = true,
        });
    }
}
