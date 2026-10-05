using NUnit.Framework;
using sb.Chapters;
using sb.Cli;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb.Tests.Cli;

[TestFixture]
public class ChapterCommandsTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _documents = null!;
    private ChapterStore _chapters = null!;
    private SongStore _songs = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-chapter-commands-tests").FullName;
        _index = new SbIndexDatabase(Path.Combine(_tempRoot, "index.sqlite"));
        _documents = new DocumentStore(_index);
        _chapters = new ChapterStore(_index, _documents);
        _songs = new SongStore(_index, _chapters, _documents);
        _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
    }

    [TearDown]
    public void TearDown()
    {
        _index.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void Add_WithoutAnExplicitName_DefaultsToTheDocumentChpNConvention()
    {
        var chapter = ChapterCommands.Add(_chapters, "doc");

        Assert.That(chapter.Name, Is.EqualTo("doc_chp_1"));
    }

    [Test]
    public void Add_WithAnExplicitName_UsesIt()
    {
        var chapter = ChapterCommands.Add(_chapters, "doc", name: "main");

        Assert.That(chapter.Name, Is.EqualTo("main"));
    }

    [Test]
    public void List_WithSongs_ShowsEachChaptersSongs()
    {
        ChapterCommands.Add(_chapters, "doc", name: "main");
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 2);

        var output = ChapterCommands.List(_chapters, "doc", includeSongs: true, _songs);

        Assert.That(output, Does.Contain("main"));
        Assert.That(output, Does.Contain("2. A Song"));
    }

    [Test]
    public void Remove_UnknownDocument_ThrowsDescriptively()
    {
        Assert.Throws<InvalidOperationException>(() => ChapterCommands.Remove(_chapters, "does-not-exist", new[] { "main" }));
    }
}
