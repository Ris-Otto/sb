using NUnit.Framework;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Documents;

/// <summary>FR-1.7.5: when a folder or file the database expects has been moved or deleted outside SB,
/// the user is told about the mismatch — listings flag it, and commands that need the folder stop with
/// a clear error instead of failing obscurely or silently recreating it.</summary>
[TestFixture]
public class MismatchHandlingTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private DocumentOperations _documents = null!;
    private ChapterOperations _chapters = null!;
    private SongOperations _songs = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-mismatch-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _documents = new DocumentOperations(_db);
        _chapters = new ChapterOperations(_db);
        _songs = new SongOperations(_db);

        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        _songs.Add("my-doc", "chapter-1", "A", "x", null, null, null);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    private string DocumentFolder => Path.Combine(_outputFolder, "my-doc");
    private string ChapterFolder => Path.Combine(DocumentFolder, "Chapters", "chapter-1");

    // --- listings flag what is missing ---

    [Test]
    public void DocumentsList_FlagsAMissingDocumentFolder()
    {
        Directory.Delete(DocumentFolder, recursive: true);

        var listing = _documents.List(includeChapters: false, includeSongs: false).Value.Single();

        Assert.That(listing.MissingAt, Is.EqualTo(DocumentFolder));
    }

    [Test]
    public void DocumentsList_NothingMissing_FlagsNothing()
    {
        var listing = _documents.List(includeChapters: true, includeSongs: true).Value.Single();

        Assert.That(listing.MissingAt, Is.Null);
        Assert.That(listing.Chapters.Single().MissingAt, Is.Null);
        Assert.That(listing.Chapters.Single().Songs.Single().MissingAt, Is.Null);
    }

    [Test]
    public void ChaptersList_FlagsAMissingChapterFolder_AndTheSongFilesInIt()
    {
        File.Delete(Path.Combine(ChapterFolder, "A.yml"));
        _chapters.Add("my-doc", "chapter-2", null);
        Directory.Delete(Path.Combine(DocumentFolder, "Chapters", "chapter-2"), recursive: true);

        var chapters = _chapters.List("my-doc", includeSongs: true).Value;

        Assert.That(chapters.Single(c => c.Name == "chapter-2").MissingAt,
            Is.EqualTo(Path.Combine(DocumentFolder, "Chapters", "chapter-2")));
        Assert.That(chapters.Single(c => c.Name == "chapter-1").MissingAt, Is.Null);
        Assert.That(chapters.Single(c => c.Name == "chapter-1").Songs.Single().MissingAt,
            Is.EqualTo(Path.Combine(ChapterFolder, "A.yml")));
    }

    [Test]
    public void SongsList_FlagsAMissingSongFile()
    {
        File.Delete(Path.Combine(ChapterFolder, "A.yml"));

        var entry = _songs.List("my-doc", null).Value.Single();

        Assert.That(entry.MissingAt, Is.EqualTo(Path.Combine(ChapterFolder, "A.yml")));
    }

    // --- commands that need the folders stop with a clear error ---

    [Test]
    public void Build_ChapterFolderMissing_FailsClearly_AndWritesNothing()
    {
        string mainTex = Path.Combine(DocumentFolder, "my-doc.tex");
        File.Delete(mainTex);
        Directory.Delete(ChapterFolder, recursive: true);

        var result = _documents.Build("my-doc", SongOrdering.AsListed, LeafletFormat.MultiColumn);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(result.Error.Message, Does.Contain("chapter-1").And.Contain(ChapterFolder));
        Assert.That(Directory.Exists(ChapterFolder), Is.False, "the folder must not be silently recreated");
        Assert.That(File.Exists(mainTex), Is.False, "nothing was written");
    }

    [Test]
    public void Build_DocumentFolderMissing_FailsClearly()
    {
        Directory.Delete(DocumentFolder, recursive: true);

        var result = _documents.Build("my-doc", SongOrdering.AsListed, LeafletFormat.MultiColumn);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(result.Error.Message, Does.Contain(DocumentFolder));
        Assert.That(Directory.Exists(DocumentFolder), Is.False);
    }

    [Test]
    public void Rename_DocumentFolderMissing_FailsClearly_ChangesNothing()
    {
        Directory.Delete(DocumentFolder, recursive: true);

        var result = _documents.Rename("my-doc", "renamed");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain(DocumentFolder));
        Assert.That(new DocumentsRepository(_db).GetByFilename("my-doc").IsSuccess, Is.True, "still recorded under the old name");
    }

    [Test]
    public void ChapterAdd_DocumentFolderMissing_FailsClearly_NoRowAdded()
    {
        Directory.Delete(DocumentFolder, recursive: true);

        var result = _chapters.Add("my-doc", "chapter-2", null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(Directory.Exists(DocumentFolder), Is.False, "not recreated");
        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        Assert.That(new ChaptersRepository(_db).GetByName(documentId, "chapter-2").IsFailure, Is.True);
    }

    [Test]
    public void SongAdd_ChapterFolderMissing_FailsClearly_NoRowAdded()
    {
        Directory.Delete(ChapterFolder, recursive: true);

        var result = _songs.Add("my-doc", "chapter-1", "B", "x", null, null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(result.Error.Message, Does.Contain(ChapterFolder));
        Assert.That(Directory.Exists(ChapterFolder), Is.False, "not recreated");
    }

    [Test]
    public void SongModify_DestinationChapterFolderMissing_FailsClearly_ChangesNothing()
    {
        _chapters.Add("my-doc", "chapter-2", null);
        string chapter2 = Path.Combine(DocumentFolder, "Chapters", "chapter-2");
        Directory.Delete(chapter2, recursive: true);

        var result = _songs.Modify("my-doc", "A", null, null, null, newChapterName: "chapter-2", newNumber: null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain(chapter2));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "A.yml")), Is.True, "the song stays where it was");
    }

    // --- clearing a song's writer and melody (FR-1.11.4) ---

    [Test]
    public void SongModify_EmptyWriterAndMelody_RemoveThem()
    {
        _songs.Add("my-doc", "chapter-1", "B", "x", "Someone", "Something", null);

        var result = _songs.Modify("my-doc", "B", null, writer: "", melody: "  ", null, null);

        Assert.That(result.IsSuccess, Is.True);
        string yaml = File.ReadAllText(Path.Combine(ChapterFolder, "B.yml"));
        var song = SongYamlSerializer.Deserialize(yaml).Value;
        Assert.That(song.Writer, Is.Null);
        Assert.That(song.Melody, Is.Null);
        Assert.That(yaml, Does.Not.Contain("writer").And.Not.Contain("melody"));
    }

    [Test]
    public void SongModify_EmptyWriter_LeavesTheMelodyAlone()
    {
        _songs.Add("my-doc", "chapter-1", "B", "x", "Someone", "Something", null);

        _songs.Modify("my-doc", "B", null, writer: "", melody: null, null, null);

        var song = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "B.yml"))).Value;
        Assert.That(song.Writer, Is.Null);
        Assert.That(song.Melody, Is.EqualTo("Something"));
    }

    [Test]
    public void SongAdd_EmptyWriter_IsTheSameAsOmittingIt()
    {
        _songs.Add("my-doc", "chapter-1", "B", "x", writer: "", melody: null, number: null);

        string yaml = File.ReadAllText(Path.Combine(ChapterFolder, "B.yml"));
        Assert.That(yaml, Does.Not.Contain("writer"));
    }
}
