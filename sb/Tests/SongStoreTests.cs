using NUnit.Framework;
using sb.Chapters;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb.Tests;

[TestFixture]
public class SongStoreTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _documents = null!;
    private ChapterStore _chapters = null!;
    private SongStore _songs = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-song-store-tests").FullName;
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
    public void Add_RejectsAnUnknownParentChapter()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _songs.Add("doc", "does-not-exist", "A Song", "lyrics", singingOrderNumber: 1));
    }

    [Test]
    public void Add_WritesASingleSongFile_AndAnIndexRow()
    {
        _songs.Add("doc", "main", "Idas sommarvisa", "line one\nline two", singingOrderNumber: 1);

        var chapterPath = Path.Combine(_tempRoot, "doc", "Chapters", "main");
        var ymlFiles = Directory.GetFiles(chapterPath, "*.yml");
        var txtFiles = Directory.GetFiles(chapterPath, "*.txt");

        Assert.That(ymlFiles, Has.Length.EqualTo(1));
        Assert.That(txtFiles, Is.Empty, "songs no longer write a separate .txt file (research.md §9)");
        Assert.That(File.ReadAllText(ymlFiles[0]), Does.Contain("line one").And.Contain("line two"));
        var added = _songs.List("doc").Single();
        Assert.That(added.Title, Is.EqualTo("Idas sommarvisa"));
        Assert.That(added.Text, Is.EqualTo("line one\nline two"));
    }

    [Test]
    public void List_ReturnsEverySongAcrossAllChapters_WhenNoChapterFilterIsGiven()
    {
        _chapters.Add("doc", "second");
        _songs.Add("doc", "main", "First", "a", singingOrderNumber: 1);
        _songs.Add("doc", "second", "Second", "b", singingOrderNumber: 2);

        var all = _songs.List("doc");

        Assert.That(all.Select(s => s.Title), Is.EquivalentTo(new[] { "First", "Second" }));
    }

    [Test]
    public void List_FiltersToOneChapter_WhenChapterNameIsGiven()
    {
        _chapters.Add("doc", "second");
        _songs.Add("doc", "main", "First", "a", singingOrderNumber: 1);
        _songs.Add("doc", "second", "Second", "b", singingOrderNumber: 2);

        var mainOnly = _songs.List("doc", "main");

        Assert.That(mainOnly.Select(s => s.Title), Is.EqualTo(new[] { "First" }));
    }

    [Test]
    public void Remove_DeletesTheSongsFilesAndIndexRow()
    {
        _songs.Add("doc", "main", "Removable", "lyrics", singingOrderNumber: 1);

        _songs.Remove("doc", "Removable");

        Assert.That(_songs.List("doc"), Is.Empty);
        var chapterPath = Path.Combine(_tempRoot, "doc", "Chapters", "main");
        Assert.That(Directory.GetFiles(chapterPath), Is.Empty);
    }

    [Test]
    public void Remove_UnknownSongName_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => _songs.Remove("doc", "does-not-exist"));
    }

    [Test]
    public void Add_RejectsADuplicateSingingOrderNumber_WithinTheSameDocument()
    {
        _songs.Add("doc", "main", "First", "a", singingOrderNumber: 1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _songs.Add("doc", "main", "Second", "b", singingOrderNumber: 1));
        Assert.That(ex!.Message, Does.Contain("First"));
    }

    [Test]
    public void Add_RejectsADuplicateSingingOrderNumber_AcrossDifferentChaptersInTheSameDocument()
    {
        _chapters.Add("doc", "second");
        _songs.Add("doc", "main", "First", "a", singingOrderNumber: 1);

        Assert.Throws<InvalidOperationException>(() =>
            _songs.Add("doc", "second", "Second", "b", singingOrderNumber: 1));
    }

    [Test]
    public void Add_AllowsTheSameSingingOrderNumber_InADifferentDocument()
    {
        _documents.Create("other", "Other", Path.Combine(_tempRoot, "other"));
        _chapters.Add("other", "main");
        _songs.Add("doc", "main", "First", "a", singingOrderNumber: 1);

        Assert.DoesNotThrow(() => _songs.Add("other", "main", "First Elsewhere", "a", singingOrderNumber: 1));
    }

    [Test]
    public void Modify_MovingChapter_DoesNotChangeTheSingingOrderNumber()
    {
        _chapters.Add("doc", "second");
        _songs.Add("doc", "main", "Movable", "lyrics", singingOrderNumber: 3);

        _songs.Modify("doc", "Movable", chapter: "second");

        var moved = _songs.List("doc", "second").Single();
        Assert.That(moved.SingingOrderNumber, Is.EqualTo(3));
        Assert.That(_songs.List("doc", "main"), Is.Empty);
    }

    [Test]
    public void Modify_ChangingSingingNumber_RunsTheSameUniquenessCheckAsAdd()
    {
        _songs.Add("doc", "main", "First", "a", singingOrderNumber: 1);
        _songs.Add("doc", "main", "Second", "b", singingOrderNumber: 2);

        Assert.Throws<InvalidOperationException>(() => _songs.Modify("doc", "Second", singingOrderNumber: 1));
    }

    [Test]
    public void GetSingingOrderNumber_ReturnsTheNumber_IndependentOfPhysicalPosition()
    {
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 7);

        Assert.That(_songs.GetSingingOrderNumber("doc", "A Song"), Is.EqualTo(7));
    }

    [Test]
    public void Add_RoundTripsTheOptionalScoreField()
    {
        _songs.Add("doc", "main", "Scored", "lyrics", singingOrderNumber: 1, score: new[] { "scored-1.png", "scored-2.png" });

        var added = _songs.List("doc").Single();
        Assert.That(added.Score, Is.EqualTo(new[] { "scored-1.png", "scored-2.png" }));
    }

    [Test]
    public void Add_ThenList_RoundTripsNonAsciiCharacters_InTitleWriterMelodyAndLyrics()
    {
        // 003-utf8-encoding, research.md §2/§3: explicit UTF-8 read/write for the song's own
        // .yml file, using this project's own real fixture line.
        _songs.Add(
            "doc", "main", "Hon var så liten",
            "Hon var så liten, hon var så frusen\nHon hade istappar under musen",
            singingOrderNumber: 1,
            writer: "Åke Öberg",
            melody: "Trad. från Ångermanland");

        var added = _songs.List("doc").Single();

        Assert.That(added.Title, Is.EqualTo("Hon var så liten"));
        Assert.That(added.Text, Is.EqualTo("Hon var så liten, hon var så frusen\nHon hade istappar under musen"));
        Assert.That(added.Writer, Is.EqualTo("Åke Öberg"));
        Assert.That(added.Melody, Is.EqualTo("Trad. från Ångermanland"));
    }

    [Test]
    public void Modify_UpdatesTitleWriterAndMelody()
    {
        _songs.Add("doc", "main", "Original", "lyrics", singingOrderNumber: 1, writer: "Old Writer");

        _songs.Modify("doc", "Original", title: "Updated", writer: "New Writer", melody: "New Melody");

        var updated = _songs.List("doc").Single();
        Assert.That(updated.Title, Is.EqualTo("Updated"));
        Assert.That(updated.Writer, Is.EqualTo("New Writer"));
        Assert.That(updated.Melody, Is.EqualTo("New Melody"));
    }
}
