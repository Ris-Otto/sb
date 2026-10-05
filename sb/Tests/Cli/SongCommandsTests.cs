using NUnit.Framework;
using sb.Chapters;
using sb.Cli;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb.Tests.Cli;

[TestFixture]
public class SongCommandsTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _documents = null!;
    private ChapterStore _chapters = null!;
    private SongStore _songs = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-song-commands-tests").FullName;
        _index = new SbIndexDatabase(Path.Combine(_tempRoot, "index.sqlite"));
        _documents = new DocumentStore(_index);
        _chapters = new ChapterStore(_index, _documents);
        _songs = new SongStore(_index, _chapters, _documents);
        _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
    }

    [TearDown]
    public void TearDown()
    {
        _index.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void Add_ThenList_ShowsTheSongWithItsSingingOrderNumber()
    {
        SongCommands.Add(_songs, "doc", "main", "A Song", "lyrics", singingOrderNumber: 4);

        var output = SongCommands.List(_songs, "doc");

        Assert.That(output, Does.Contain("4. A Song"));
    }

    [Test]
    public void Add_UnknownChapter_ThrowsDescriptively()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SongCommands.Add(_songs, "doc", "does-not-exist", "A Song", "lyrics", 1));
        Assert.That(ex!.Message, Does.Contain("does-not-exist"));
    }

    [Test]
    public void Add_WithAnExistingFilePathAsText_ReadsItsContentsAsTheLyricText()
    {
        var textFilePath = Path.Combine(_tempRoot, "lyrics.txt");
        File.WriteAllText(textFilePath, "line one\nline two");

        SongCommands.Add(_songs, "doc", "main", "From File", textFilePath, singingOrderNumber: 1);

        var added = _songs.List("doc").Single();
        Assert.That(added.Text, Is.EqualTo("line one\nline two"));
    }

    [Test]
    public void Add_WithANonAsciiExistingFilePathAsText_ReadsItsContentsAsTheLyricText()
    {
        // 003-utf8-encoding: validates the NFC/NFD normalization defense (research.md §4) — the
        // path itself already arrives correctly encoded here (as SB's own process actually
        // receives arguments), so this is not a reproduction of the originally-reported
        // `.bat`-script scenario, which happens in a parent process no in-process test can exercise.
        var textFilePath = Path.Combine(_tempRoot, "hon_var_så_liten.txt");
        File.WriteAllText(textFilePath, "Hon var så liten\nHon hade istappar under musen");

        SongCommands.Add(_songs, "doc", "main", "Hon var så liten", textFilePath, singingOrderNumber: 1);

        var added = _songs.List("doc").Single();
        Assert.That(added.Text, Is.EqualTo("Hon var så liten\nHon hade istappar under musen"));
    }

    [Test]
    public void Add_WithLiteralTextThatIsNotAnExistingFile_UsesItAsIs()
    {
        SongCommands.Add(_songs, "doc", "main", "Literal", "just some lyrics", singingOrderNumber: 1);

        var added = _songs.List("doc").Single();
        Assert.That(added.Text, Is.EqualTo("just some lyrics"));
    }

    [Test]
    public void Remove_UnknownSong_ThrowsDescriptively()
    {
        Assert.Throws<InvalidOperationException>(() => SongCommands.Remove(_songs, "doc", "does-not-exist"));
    }

    [Test]
    public void Modify_ChangesOnlyTheSpecifiedFields()
    {
        SongCommands.Add(_songs, "doc", "main", "Original", "lyrics", 1, writer: "Old Writer");

        SongCommands.Modify(_songs, "doc", "Original", title: "Updated");

        var song = _songs.List("doc").Single();
        Assert.That(song.Title, Is.EqualTo("Updated"));
        Assert.That(song.Writer, Is.EqualTo("Old Writer"));
    }

    [Test]
    public void Modify_ConflictingSingingNumber_ThrowsDescriptively()
    {
        SongCommands.Add(_songs, "doc", "main", "First", "a", 1);
        SongCommands.Add(_songs, "doc", "main", "Second", "b", 2);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SongCommands.Modify(_songs, "doc", "Second", singingOrderNumber: 1));
        Assert.That(ex!.Message, Does.Contain("First"));
    }

    [Test]
    public void List_FilteredByChapter_OnlyShowsThatChaptersSongs()
    {
        _chapters.Add("doc", "second");
        SongCommands.Add(_songs, "doc", "main", "First", "a", 1);
        SongCommands.Add(_songs, "doc", "second", "Second", "b", 2);

        var output = SongCommands.List(_songs, "doc", "main");

        Assert.That(output, Does.Contain("First"));
        Assert.That(output, Does.Not.Contain("Second"));
    }
}
