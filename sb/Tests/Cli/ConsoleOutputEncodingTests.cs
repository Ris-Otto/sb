using NUnit.Framework;
using sb.Chapters;
using sb.Cli;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb.Tests.Cli;

[TestFixture]
public class ConsoleOutputEncodingTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _documents = null!;
    private ChapterStore _chapters = null!;
    private SongStore _songs = null!;
    private LocationMismatchDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-console-output-encoding-tests").FullName;
        _index = new SbIndexDatabase(Path.Combine(_tempRoot, "index.sqlite"));
        _documents = new DocumentStore(_index);
        _chapters = new ChapterStore(_index, _documents);
        _songs = new SongStore(_index, _chapters, _documents);
        _detector = new LocationMismatchDetector(_index);
    }

    [TearDown]
    public void TearDown()
    {
        _index.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void DocumentsList_ReturnsANonAsciiDocumentTitle_Unmodified()
    {
        // 003-utf8-encoding US3: confirms the in-memory string is correct end-to-end from
        // storage through to the point of display (Program.ConfigureUtf8Console, covered by
        // ConsoleEncodingTests, is what makes the actual console byte output correct).
        DocumentCommands.Create(_documents, "doc", "Sångblad å ä ö", Path.Combine(_tempRoot, "doc"));

        var output = DocumentCommands.List(_documents, _detector);

        Assert.That(output, Does.Contain("Sångblad å ä ö"));
    }

    [Test]
    public void SongsList_ReturnsANonAsciiSongTitle_Unmodified()
    {
        DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "Hon var så liten", "lyrics", singingOrderNumber: 1);

        var output = SongCommands.List(_songs, "doc");

        Assert.That(output, Does.Contain("Hon var så liten"));
    }
}
