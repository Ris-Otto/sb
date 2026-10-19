using NUnit.Framework;
using sb.Chapters;
using sb.Cli;
using sb.Documents;
using sb.Index;
using sb.Ordering;
using sb.Songs;

namespace sb.Tests.Cli;

[TestFixture]
public class DocumentCommandsTests
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
        _tempRoot = Directory.CreateTempSubdirectory("sb-document-commands-tests").FullName;
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
    public void Create_WithExplicitPath_CreatesTheDocumentThere()
    {
        var path = Path.Combine(_tempRoot, "custom-location");

        var document = DocumentCommands.Create(_documents, "sommarvisor", "Sommarvisor", path);

        Assert.That(document.Path, Is.EqualTo(path));
        Assert.That(Directory.Exists(path), Is.True);
    }

    [Test]
    public void Create_WithoutPath_DefaultsUnderTheCurrentDirectory()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_tempRoot);
        try
        {
            var document = DocumentCommands.Create(_documents, "defaulted", "Defaulted");
            Assert.That(document.Path, Is.EqualTo(Path.Combine(_tempRoot, "defaulted")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void List_DoesNotCrash_WhenAMismatchedDocumentStillHasChaptersAndSongsRequested()
    {
        var document = DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 1);
        Directory.Move(document.Path, document.Path + "-moved");

        string output = "";
        Assert.DoesNotThrow(() =>
            output = DocumentCommands.List(_documents, _detector, includeChapters: true, includeSongs: true, _chapters, _songs));
        Assert.That(output, Does.Contain("LOCATION MISMATCH"));
    }

    [Test]
    public void List_IncludesMismatchReports_WhenADocumentsFolderWentMissing()
    {
        var document = DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        Directory.Delete(document.Path, recursive: true);

        var output = DocumentCommands.List(_documents, _detector);

        Assert.That(output, Does.Contain("MISMATCH"));
    }

    [Test]
    public void List_WithChaptersAndSongs_NestsSongsUnderTheirChapter()
    {
        DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 5);

        var output = DocumentCommands.List(_documents, _detector, includeChapters: true, includeSongs: true, _chapters, _songs);

        Assert.That(output, Does.Contain("main"));
        Assert.That(output, Does.Contain("5. A Song"));
    }

    [Test]
    public void Delete_UnknownName_ThrowsDescriptively()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DocumentCommands.Delete(_documents, "does-not-exist"));
        Assert.That(ex!.Message, Does.Contain("does-not-exist"));
    }

    [Test]
    public void Rename_UnknownName_ThrowsDescriptively()
    {
        Assert.Throws<InvalidOperationException>(() => DocumentCommands.Rename(_documents, "does-not-exist", "new"));
    }

    [Test]
    public void Build_UnknownDocumentName_ThrowsDescriptively()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DocumentCommands.Build(_documents, _chapters, _songs, _detector, "does-not-exist"));
    }

    [Test]
    public void Build_DocumentWithNoSongs_ThrowsCitingFR012()
    {
        DocumentCommands.Create(_documents, "empty", "Empty", Path.Combine(_tempRoot, "empty"));

        var ex = Assert.Throws<ArgumentException>(() =>
            DocumentCommands.Build(_documents, _chapters, _songs, _detector, "empty"));
        Assert.That(ex!.Message, Does.Contain("FR-012"));
    }


    [Test]
    public void Build_WritesTheDocumentsNamedTexFile_ToTheDocumentsIndexedPath()
    {
        var document = DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "A Song", "some lyrics", singingOrderNumber: 1);

        var outputPath = DocumentCommands.Build(_documents, _chapters, _songs, _detector, "doc", OrderingMode.AsListed);

        Assert.That(outputPath, Is.EqualTo(Path.Combine(document.Path, "doc.tex")));
        Assert.That(File.Exists(outputPath), Is.True);
        Assert.That(File.ReadAllText(outputPath), Does.Contain("\\input{Chapters/main/chapter.tex}"));
    }

    [Test]
    public void Build_WritesEachChaptersOwnChapterTexFile_ContainingItsSongsBehindAChapterHeading()
    {
        var document = DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main", "Main Chapter");
        _songs.Add("doc", "main", "A Song", "some lyrics", singingOrderNumber: 1);

        DocumentCommands.Build(_documents, _chapters, _songs, _detector, "doc", OrderingMode.AsListed);

        var chapterTexPath = Path.Combine(document.Path, "Chapters", "main", "chapter.tex");
        Assert.That(File.Exists(chapterTexPath), Is.True);
        var chapterContent = File.ReadAllText(chapterTexPath);
        Assert.That(chapterContent, Does.StartWith("\\chapter*{Main Chapter}"));
        Assert.That(chapterContent, Does.Contain("A Song"));
        Assert.That(chapterContent, Does.Contain("some lyrics"));
    }

    [Test]
    public void Build_WithNonAsciiChapterTitleAndLyrics_WritesBothFilesWithoutBomOrCorruption()
    {
        // 003-utf8-encoding, research.md §3: chapter.tex and the main document are both written
        // as UTF-8 without a BOM.
        var document = DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main", "Sångblad å ä ö");
        _songs.Add("doc", "main", "Hon var så liten", "Hon var så liten, hon var så frusen", singingOrderNumber: 1);

        var outputPath = DocumentCommands.Build(_documents, _chapters, _songs, _detector, "doc", OrderingMode.AsListed);

        var chapterTexPath = Path.Combine(document.Path, "Chapters", "main", "chapter.tex");
        var chapterContent = File.ReadAllText(chapterTexPath);
        Assert.That(chapterContent, Does.Contain("Sångblad å ä ö"));
        Assert.That(chapterContent, Does.Contain("Hon var så liten, hon var så frusen"));
        Assert.That(File.ReadAllBytes(chapterTexPath).Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.That(File.ReadAllBytes(outputPath).Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Test]
    public void Build_RefusesToRun_WhenTheDocumentsOwnLocationIsMismatched()
    {
        var document = DocumentCommands.Create(_documents, "doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 1);
        Directory.Move(document.Path, document.Path + "-moved");

        Assert.Throws<InvalidOperationException>(() =>
            DocumentCommands.Build(_documents, _chapters, _songs, _detector, "doc"));
    }
}
