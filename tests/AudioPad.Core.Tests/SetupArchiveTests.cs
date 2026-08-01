using AudioPad.Core.Models;
using AudioPad.Core.Persistence;

namespace AudioPad.Core.Tests;

public class SetupArchiveTests : IDisposable
{
    private readonly string _sourceAudioPath = Path.Combine(Path.GetTempPath(), $"audiopad-test-audio-{Guid.NewGuid()}.wav");
    private readonly byte[] _audioBytes = [1, 2, 3, 4, 5];

    public SetupArchiveTests()
    {
        File.WriteAllBytes(_sourceAudioPath, _audioBytes);
    }

    [Fact]
    public async Task ExportPage_ThenImportPage_RoundTripsStructureAndMediaBytes()
    {
        var page = Page.CreateDefault(title: "Effects", rows: 1, columns: 1);
        page.Pads[0].Label = "Applause";
        page.Pads[0].AudioFilePath = _sourceAudioPath;

        await using var archive = new MemoryStream();
        await SetupArchive.ExportPageAsync(archive, page);
        archive.Position = 0;

        var imported = await SetupArchive.ImportPageAsync(archive);

        Assert.Equal("Effects", imported.Title);
        Assert.Equal("Applause", imported.Pads[0].Label);
        Assert.NotNull(imported.Pads[0].AudioFilePath);
        Assert.NotEqual(_sourceAudioPath, imported.Pads[0].AudioFilePath);
        Assert.True(File.Exists(imported.Pads[0].AudioFilePath));
        Assert.Equal(_audioBytes, await File.ReadAllBytesAsync(imported.Pads[0].AudioFilePath!));
    }

    [Fact]
    public async Task ExportSetup_ThenImportSetup_RoundTripsMultiplePagesAndDedupesSharedMedia()
    {
        var pageA = Page.CreateDefault(title: "A", rows: 1, columns: 2);
        pageA.Pads[0].AudioFilePath = _sourceAudioPath;
        pageA.Pads[1].AudioFilePath = _sourceAudioPath; // same source file, referenced twice
        var pageB = Page.CreateDefault(title: "B", rows: 1, columns: 1);
        var setup = new Setup { Pages = { pageA, pageB } };

        await using var archive = new MemoryStream();
        await SetupArchive.ExportSetupAsync(archive, setup);
        archive.Position = 0;

        var imported = await SetupArchive.ImportSetupAsync(archive);

        Assert.Equal(2, imported.Pages.Count);
        Assert.Equal("A", imported.Pages[0].Title);
        Assert.Equal("B", imported.Pages[1].Title);
        Assert.Equal(
            imported.Pages[0].Pads[0].AudioFilePath,
            imported.Pages[0].Pads[1].AudioFilePath);
    }

    [Fact]
    public async Task Import_ReportsAPageArchiveAsAPage()
    {
        await using var archive = new MemoryStream();
        await SetupArchive.ExportPageAsync(archive, Page.CreateDefault(title: "Effects", rows: 1, columns: 1));
        archive.Position = 0;

        var contents = await SetupArchive.ImportAsync(archive);

        Assert.Null(contents.Setup);
        Assert.Equal("Effects", contents.Page?.Title);
    }

    [Fact]
    public async Task Import_ReportsASetupArchiveAsASetup()
    {
        var setup = new Setup { Pages = { Page.CreateDefault(title: "A", rows: 1, columns: 1) } };

        await using var archive = new MemoryStream();
        await SetupArchive.ExportSetupAsync(archive, setup);
        archive.Position = 0;

        var contents = await SetupArchive.ImportAsync(archive);

        Assert.Null(contents.Page);
        Assert.Equal("A", Assert.Single(contents.Setup!.Pages).Title);
    }

    /// <summary>
    /// Android's Storage Access Framework hands back a stream that can't seek, which is exactly what
    /// <see cref="ZipArchive"/> needs — so importing has to buffer. Desktop never exercises this,
    /// which is why it's pinned by a test rather than left to be discovered on the tablet.
    /// </summary>
    [Fact]
    public async Task Import_ReadsAStreamThatCannotSeek()
    {
        var page = Page.CreateDefault(title: "Effects", rows: 1, columns: 1);
        page.Pads[0].AudioFilePath = _sourceAudioPath;

        await using var archive = new MemoryStream();
        await SetupArchive.ExportPageAsync(archive, page);

        await using var forwardOnly = new ForwardOnlyStream(archive.ToArray());
        var contents = await SetupArchive.ImportAsync(forwardOnly);

        Assert.Equal("Effects", contents.Page?.Title);
        Assert.Equal(_audioBytes, await File.ReadAllBytesAsync(contents.Page!.Pads[0].AudioFilePath!));
    }

    [Fact]
    public async Task Import_RejectsSomethingThatIsNotAnAudioPadArchive()
    {
        await using var notAnArchive = new MemoryStream("just some text"u8.ToArray());

        await Assert.ThrowsAnyAsync<Exception>(() => SetupArchive.ImportAsync(notAnArchive));
    }

    public void Dispose()
    {
        if (File.Exists(_sourceAudioPath))
        {
            File.Delete(_sourceAudioPath);
        }
    }

    /// <summary>A read-only, non-seekable stream, standing in for an Android content:// handle.</summary>
    private sealed class ForwardOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
