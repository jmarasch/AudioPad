using System.Collections.ObjectModel;
using AudioPad.Core.Models;
using AudioPad.UI.Interactions;
using AudioPad.Core.Persistence;
using AudioPad.Core.Playback;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPad.UI.ViewModels;

/// <summary>Owns the current setup (all pages) and exposes it for the UI to bind to.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly Setup _setup;
    private readonly IAudioEngine _audioEngine;
    private readonly SetupRepository _setupRepository;
    private readonly string _setupPath;

    /// <summary>The real, ordered pages — what page reordering and Manage Pages act on.</summary>
    public ObservableCollection<PageViewModel> Pages { get; }

    /// <summary>
    /// Whether the board is being arranged rather than played. Shared with every pad, so a tap
    /// means "edit this" instead of "play this" while it's on.
    /// </summary>
    public EditModeState EditMode { get; } = new();

    /// <summary>
    /// <see cref="Pages"/> padded with a clone of the last page at index 0 and a clone of the
    /// first page at the end, so the Carousel can be swiped/advanced one step past either real
    /// end and then be silently snapped back to the corresponding real page — the illusion of a
    /// seamless, endless carousel. See <see cref="IsSentinelIndex"/>/<see cref="ResolveSentinelIndex"/>.
    /// </summary>
    public ObservableCollection<CarouselSlot> CarouselItems { get; } = new();

    /// <summary>Two-way bound to the Carousel's SelectedIndex. Starts at 1: the first real page.</summary>
    [ObservableProperty]
    private int _selectedCarouselIndex = 1;

    /// <summary>The pad currently being edited via the double-tap overlay, or null when it's closed.</summary>
    [ObservableProperty]
    private PadConfigViewModel? _activeConfig;

    /// <summary>The page whose settings are being edited from the overview, or null when closed.</summary>
    [ObservableProperty]
    private PageConfigViewModel? _activePageConfig;

    /// <summary>Whether the zoomed-out page overview is open.</summary>
    [ObservableProperty]
    private bool _isPageOverviewOpen;

    /// <summary>The tile selected in the page overview, or null when none is. Transient — not persisted.</summary>
    [ObservableProperty]
    private PageViewModel? _selectedOverviewPage;

    /// <summary>
    /// Whether Delete is "armed" (first press) and a second press on the same selection will
    /// actually delete — a lightweight confirm-by-repeating instead of a modal dialog.
    /// </summary>
    [ObservableProperty]
    private bool _isDeleteArmed;

    /// <summary>
    /// What the last import or export did, shown in the overview. Import in particular can fail for
    /// reasons only the file knows about (wrong file picked, truncated copy), and silently doing
    /// nothing would be indistinguishable from success.
    /// </summary>
    [ObservableProperty]
    private string? _archiveStatus;

    public MainWindowViewModel()
        : this(Setup.CreateDefault(), new NullAudioEngine(), new SetupRepository(), SetupRepository.GetDefaultSetupPath())
    {
    }

    public MainWindowViewModel(Setup setup, IAudioEngine audioEngine, SetupRepository setupRepository, string setupPath)
    {
        _setup = setup;
        _audioEngine = audioEngine;
        _setupRepository = setupRepository;
        _setupPath = setupPath;

        // Pull any clip still living in the user's own folders into the app's library, so the board
        // stops depending on paths outside it. Backed up first: this rewrites every such path, and
        // the previous file is the only way back if it goes wrong.
        if (MediaLibrary.AdoptAll(setup))
        {
            BackUpCurrentSetup();
            PersistSetup();
        }

        Pages = new ObservableCollection<PageViewModel>();
        foreach (var page in setup.Pages)
        {
            Pages.Add(CreatePageViewModel(page));
        }

        RebuildCarouselItems();
    }

    /// <summary>
    /// Label for the overview's delete button. Deleting is confirm-by-repeating rather than a
    /// modal, so the button has to say which press it's on.
    /// </summary>
    public string DeleteButtonText => IsDeleteArmed ? "Confirm delete" : "Delete";

    /// <summary>True if a Carousel index is one of the sentinel clones at either end.</summary>
    public bool IsSentinelIndex(int carouselIndex) => carouselIndex == 0 || carouselIndex == CarouselItems.Count - 1;

    /// <summary>Maps a sentinel index to the real index it stands in for, within <see cref="CarouselItems"/>.</summary>
    public int ResolveSentinelIndex(int carouselIndex) => carouselIndex == 0 ? CarouselItems.Count - 2 : 1;

    [RelayCommand]
    private void NextPage()
    {
        SelectedCarouselIndex = Math.Min(SelectedCarouselIndex + 1, CarouselItems.Count - 1);
        GestureLog.Write($"next page -> {SelectedCarouselIndex} of {CarouselItems.Count}");
    }

    [RelayCommand]
    private void PreviousPage()
    {
        SelectedCarouselIndex = Math.Max(SelectedCarouselIndex - 1, 0);
        GestureLog.Write($"previous page -> {SelectedCarouselIndex} of {CarouselItems.Count}");
    }

    [RelayCommand]
    private void AddPage()
    {
        var page = Page.CreateDefault(title: $"Page {Pages.Count + 1}");
        _setup.Pages.Add(page);
        Pages.Add(CreatePageViewModel(page));
        RebuildCarouselItems();
        PersistSetup();
    }

    [RelayCommand]
    private void OpenPageOverview() => IsPageOverviewOpen = true;

    /// <summary>Switches between playing the board and arranging it.</summary>
    [RelayCommand]
    private void ToggleEditMode() => EditMode.IsEditing = !EditMode.IsEditing;

    [RelayCommand]
    private void CloseOverview()
    {
        IsPageOverviewOpen = false;
        SelectedOverviewPage = null;

        // The import/export status describes the visit that's ending, so it shouldn't be waiting
        // there, stale, the next time the overview is opened.
        ArchiveStatus = null;
    }

    /// <summary>Closes the overview and jumps the carousel straight to the given page — a quick
    /// way to skip several pages at once instead of swiping/arrow-clicking through each one.</summary>
    [RelayCommand]
    private void NavigateToPage(PageViewModel page)
    {
        var index = Pages.IndexOf(page);
        if (index < 0)
        {
            return;
        }

        CloseOverview();
        SelectedCarouselIndex = index + 1;
        GestureLog.Write($"navigate to page {index} -> carousel {SelectedCarouselIndex} of {CarouselItems.Count}");
    }

    [RelayCommand]
    private void DeleteSelectedPage()
    {
        if (SelectedOverviewPage is not { } page || Pages.Count <= 1)
        {
            return;
        }

        if (!IsDeleteArmed)
        {
            IsDeleteArmed = true;
            return;
        }

        var index = Pages.IndexOf(page);

        // The page's clips and icons become unreachable with it, so they go too. Only files the
        // library owns are ever touched — a pad still pointing at an original somewhere in the
        // user's own folders is left alone. See MediaLibrary.Delete.
        foreach (var pad in _setup.Pages[index].Pads)
        {
            MediaLibrary.Delete(pad.AudioFilePath);
            MediaLibrary.Delete(pad.IconPath);
        }

        page.Detach();
        Pages.RemoveAt(index);
        _setup.Pages.RemoveAt(index);
        SelectedOverviewPage = null;
        RebuildCarouselItems();
        PersistSetup();
    }

    /// <summary>Opens the settings overlay for the page selected in the overview.</summary>
    [RelayCommand]
    private void ConfigureSelectedPage()
    {
        if (SelectedOverviewPage is not { } page)
        {
            return;
        }

        ActivePageConfig = new PageConfigViewModel(page.Page, saved => OnPageConfigClosed(page, saved));
    }

    private void OnPageConfigClosed(PageViewModel page, bool saved)
    {
        if (saved)
        {
            page.RefreshFromPage();
            PersistSetup();
        }

        ActivePageConfig = null;
    }

    partial void OnSelectedOverviewPageChanged(PageViewModel? value) => IsDeleteArmed = false;

    partial void OnIsDeleteArmedChanged(bool value) => OnPropertyChanged(nameof(DeleteButtonText));

    /// <summary>Moves a page to a new index, shifting the pages between along — the same
    /// list-reorder semantics as <see cref="PageViewModel.MovePad"/>. Called once per drag, when
    /// the finger lifts.</summary>
    public void MovePage(PageViewModel page, int index)
    {
        var from = Pages.IndexOf(page);
        var to = Math.Clamp(index, 0, Pages.Count - 1);
        if (from < 0 || from == to)
        {
            return;
        }

        Pages.Move(from, to);

        var moved = _setup.Pages[from];
        _setup.Pages.RemoveAt(from);
        _setup.Pages.Insert(to, moved);

        MirrorReorderIntoCarousel(from, to);
        PersistSetup();
    }

    /// <summary>
    /// Applies a page reorder to <see cref="CarouselItems"/> as a move plus, at most, two sentinel
    /// replacements — never a full rebuild. <see cref="RebuildCarouselItems"/> clears and re-adds,
    /// which raises a Reset that makes the bound Carousel discard its SelectedIndex; doing that
    /// while the overview is covering it left the carousel showing a stale page and refusing to
    /// navigate afterwards. A Move notification keeps the Carousel's own bookkeeping intact.
    /// </summary>
    private void MirrorReorderIntoCarousel(int from, int to)
    {
        // CarouselItems is Pages padded with one sentinel at each end, so real page N sits at N+1.
        CarouselItems.Move(from + 1, to + 1);

        // Moving the first or last page changes which page the sentinels stand in for.
        var last = CarouselItems.Count - 1;
        if (!ReferenceEquals(CarouselItems[0].Page, Pages[^1]))
        {
            CarouselItems[0] = new CarouselSlot(Pages[^1]);
        }

        if (!ReferenceEquals(CarouselItems[last].Page, Pages[0]))
        {
            CarouselItems[last] = new CarouselSlot(Pages[0]);
        }
    }

    /// <summary>
    /// Rebuilds the padded <see cref="CarouselItems"/> list from <see cref="Pages"/>. Clearing and
    /// re-adding fires a Reset notification that a bound Carousel reacts to by resetting its own
    /// SelectedIndex — which flows straight back into <see cref="SelectedCarouselIndex"/> through
    /// the two-way binding — so the previously-selected page is captured by reference beforehand
    /// and explicitly restored to its new position afterward, overriding whatever transient value
    /// the reset left behind.
    /// </summary>
    private void RebuildCarouselItems()
    {
        var selectedPage = SelectedCarouselIndex >= 0 && SelectedCarouselIndex < CarouselItems.Count
            ? CarouselItems[SelectedCarouselIndex].Page
            : null;

        CarouselItems.Clear();
        CarouselItems.Add(new CarouselSlot(Pages[^1]));
        foreach (var page in Pages)
        {
            CarouselItems.Add(new CarouselSlot(page));
        }

        CarouselItems.Add(new CarouselSlot(Pages[0]));

        var realIndex = selectedPage is null ? -1 : Pages.IndexOf(selectedPage);
        SelectedCarouselIndex = realIndex >= 0 ? realIndex + 1 : 1;
    }

    /// <summary>Writes the whole setup — every page, with copies of all its audio and icons — to a
    /// portable archive. This is the desktop half of "build it here, run it on the tablet".</summary>
    public async Task ExportSetupAsync(Stream destination)
    {
        try
        {
            await SetupArchive.ExportSetupAsync(destination, _setup);
            ArchiveStatus = $"Exported {DescribePageCount(Pages.Count)}.";
        }
        catch (Exception exception)
        {
            ArchiveStatus = $"Export failed: {exception.Message}";
        }
    }

    /// <summary>Writes one page, with copies of its media, to a portable archive.</summary>
    public async Task ExportPageAsync(Stream destination, PageViewModel page)
    {
        try
        {
            await SetupArchive.ExportPageAsync(destination, page.Page);
            ArchiveStatus = $"Exported \"{page.Title}\".";
        }
        catch (Exception exception)
        {
            ArchiveStatus = $"Export failed: {exception.Message}";
        }
    }

    /// <summary>
    /// Imports an archive of either kind. A single page is added to the end of the current setup; a
    /// whole setup replaces it, since that archive describes the entire board rather than a part
    /// of it. The replaced setup is backed up first.
    /// </summary>
    public async Task ImportArchiveAsync(Stream source)
    {
        try
        {
            var contents = await SetupArchive.ImportAsync(source);

            if (contents.Setup is { } setup)
            {
                ReplaceSetup(setup);
            }
            else if (contents.Page is { } page)
            {
                AppendImportedPage(page);
            }
        }
        catch (Exception exception)
        {
            ArchiveStatus = $"Import failed: {exception.Message}";
        }
    }

    private void ReplaceSetup(Setup imported)
    {
        if (imported.Pages.Count == 0)
        {
            ArchiveStatus = "That archive has no pages in it — nothing was changed.";
            return;
        }

        BackUpCurrentSetup();

        foreach (var page in Pages)
        {
            page.Detach();
        }

        _setup.Pages.Clear();
        _setup.Pages.AddRange(imported.Pages);

        Pages.Clear();
        foreach (var page in _setup.Pages)
        {
            Pages.Add(CreatePageViewModel(page));
        }

        SelectedOverviewPage = null;
        RebuildCarouselItems();
        PersistSetup();
        ArchiveStatus = $"Imported {DescribePageCount(Pages.Count)}, replacing the previous setup.";
    }

    private void AppendImportedPage(Page page)
    {
        _setup.Pages.Add(page);
        Pages.Add(CreatePageViewModel(page));
        RebuildCarouselItems();
        PersistSetup();
        ArchiveStatus = $"Added \"{page.Title}\" to the end.";
    }

    /// <summary>
    /// Keeps a timestamped copy of the setup an import is about to overwrite. Importing the wrong
    /// file would otherwise destroy a board with no way back, and a failed backup must not stop the
    /// import the user actually asked for.
    /// </summary>
    private void BackUpCurrentSetup()
    {
        try
        {
            var backupPath = Path.Combine(
                AppStorage.GetDirectory("backups"), $"setup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            _setupRepository.SaveSetup(backupPath, _setup);
        }
        catch (Exception)
        {
            // Best effort only — see above.
        }
    }

    private static string DescribePageCount(int count) => count == 1 ? "1 page" : $"{count} pages";

    private PageViewModel CreatePageViewModel(Page page) => new(page, _audioEngine, EditMode, OnPadConfigRequested, PersistSetup);

    private void OnPadConfigRequested(PadViewModel pad)
    {
        ActiveConfig = new PadConfigViewModel(pad.Config, saved => OnConfigClosed(pad, saved));
    }

    private void OnConfigClosed(PadViewModel pad, bool saved)
    {
        if (saved)
        {
            pad.RefreshFromConfig();
            PersistSetup();
        }

        ActiveConfig = null;
    }

    private void PersistSetup() => _setupRepository.SaveSetup(_setupPath, _setup);
}
