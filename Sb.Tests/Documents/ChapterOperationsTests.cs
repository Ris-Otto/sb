using NUnit.Framework;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Documents;

[TestFixture]
public class ChapterOperationsTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private DocumentOperations _documents = null!;
    private ChapterOperations _chapters = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-chapter-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _documents = new DocumentOperations(_db);
        _chapters = new ChapterOperations(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    private string DocumentPath => Path.Combine(_outputFolder, "my-doc");

    // --- Add (8.1) ---

    [Test]
    public void Add_ExplicitNameAndTitle_CreatesFolderFileAndDbRow()
    {
        _documents.Create("my-doc", "My Document", null, _outputFolder);

        var result = _chapters.Add("my-doc", "first-chapter", "First Chapter");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Name, Is.EqualTo("first-chapter"));
        Assert.That(result.Value.Title, Is.EqualTo("First Chapter"));
        string chapterFolder = Path.Combine(DocumentPath, "Chapters", "first-chapter");
        Assert.That(Directory.Exists(chapterFolder), Is.True);
        string chapterTex = File.ReadAllText(Path.Combine(chapterFolder, "first-chapter.tex"));
        Assert.That(chapterTex, Does.Contain("\\chapter*{First Chapter}"));
    }

    [Test]
    public void Add_NoNameGiven_DefaultsToDocumentChpPattern()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var result = _chapters.Add("my-doc", null, null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Name, Is.EqualTo("my-doc_chp_1"));
        Assert.That(result.Value.Title, Is.EqualTo("my-doc_chp_1"), "title defaults to the name when omitted");
    }

    [Test]
    public void Add_DefaultNaming_DoesNotReuseFreedNumberIntoACollision()
    {
        // my-doc_chp_1, _2, _3 added; _2 deleted; the next default must be _4, not a naive
        // count-based _3 (which is still taken) or _2 (freed, but would collide if chapter counting
        // was later resumed from the wrong baseline).
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", null, null);
        var second = _chapters.Add("my-doc", null, null);
        _chapters.Add("my-doc", null, null);
        _chapters.Delete("my-doc", [second.Value.Name], force: true);

        var result = _chapters.Add("my-doc", null, null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Name, Is.EqualTo("my-doc_chp_4"));
    }

    [Test]
    public void Add_DuplicateNameInSameDocument_ReturnsConflict()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", "Chapter One");

        var result = _chapters.Add("my-doc", "chapter-1", "Chapter One Again");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
    }

    [Test]
    public void Add_IllegalExplicitName_ReturnsValidation()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var result = _chapters.Add("my-doc", "foo/bar", null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Add_NonexistentDocument_ReturnsNotFound()
    {
        var result = _chapters.Add("ghost", null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    // --- Delete (8.2) ---

    [Test]
    public void Delete_MultipleChaptersInOneCall_RemovesAll()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        _chapters.Add("my-doc", "chapter-2", null);

        var result = _chapters.Delete("my-doc", ["chapter-1", "chapter-2"], force: true);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.Exists(Path.Combine(DocumentPath, "Chapters", "chapter-1")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(DocumentPath, "Chapters", "chapter-2")), Is.False);
    }

    [Test]
    public void Delete_NonEmptyChapter_WithoutForce_IsBlocked_NothingDeleted()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        SeedSong("chapter-1", "Lille Albin");

        var result = _chapters.Delete("my-doc", ["chapter-1"], force: false);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(Directory.Exists(Path.Combine(DocumentPath, "Chapters", "chapter-1")), Is.True);
    }

    [Test]
    public void Delete_NonEmptyChapter_WithForce_CascadesToSongs()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        long chapterId = SeedSong("chapter-1", "Lille Albin");

        var result = _chapters.Delete("my-doc", ["chapter-1"], force: true);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(new SongsRepository(_db).GetByTitle(chapterId, "Lille Albin").IsFailure, Is.True);
    }

    [Test]
    public void Delete_OneNameDoesNotExist_ReportsNotFound_DeletesNeither()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);

        var result = _chapters.Delete("my-doc", ["chapter-1", "ghost-chapter"], force: true);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
        Assert.That(Directory.Exists(Path.Combine(DocumentPath, "Chapters", "chapter-1")), Is.True,
            "nothing should be deleted when any named chapter doesn't exist");
    }

    [Test]
    public void Delete_EmptyChapters_NeedNoForce()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);

        var result = _chapters.Delete("my-doc", ["chapter-1"], force: false);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Delete_NonexistentDocument_ReturnsNotFound()
    {
        var result = _chapters.Delete("ghost", ["chapter-1"], force: true);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Delete_EmptyChapterList_ReturnsValidation_NotASilentNoOp()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var result = _chapters.Delete("my-doc", [], force: true);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Delete_OneChapterFolderAlreadyMissingFromDisk_StillDeletesTheOthers_ReportsBothOutcomes()
    {
        // Simulates drift from outside SB (FR-1.7.5): chapter-1's folder is gone from disk, but its DB
        // row (and chapter-2's) are both still there. A batch delete covering both must not let
        // chapter-1's filesystem-cleanup failure stop chapter-2 from being deleted too.
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        _chapters.Add("my-doc", "chapter-2", null);
        Directory.Delete(Path.Combine(DocumentPath, "Chapters", "chapter-1"), recursive: true);

        var result = _chapters.Delete("my-doc", ["chapter-1", "chapter-2"], force: true);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(result.Error!.Message, Does.Contain("chapter-1"));
        Assert.That(result.Error!.Message, Does.Contain("chapter-2"), "chapter-2's successful deletion should be noted too");
        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        Assert.That(new ChaptersRepository(_db).GetByName(documentId, "chapter-2").IsFailure, Is.True,
            "chapter-2 should still have been deleted despite chapter-1's cleanup failure");
    }

    // --- List (8.3) ---

    [Test]
    public void List_NoChapters_ReturnsEmptyList()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var result = _chapters.List("my-doc", includeSongs: false);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public void List_WithoutSongsFlag_DoesNotIncludeSongs()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        SeedSong("chapter-1", "Lille Albin");

        var result = _chapters.List("my-doc", includeSongs: false);

        Assert.That(result.Value, Has.Count.EqualTo(1));
        Assert.That(result.Value[0].Songs, Is.Empty);
    }

    [Test]
    public void List_WithSongsFlag_IncludesSongs()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
        SeedSong("chapter-1", "Lille Albin");

        var result = _chapters.List("my-doc", includeSongs: true);

        Assert.That(result.Value[0].Songs, Has.Count.EqualTo(1));
        Assert.That(result.Value[0].Songs[0].Title, Is.EqualTo("Lille Albin"));
    }

    [Test]
    public void List_NonexistentDocument_ReturnsNotFound()
    {
        var result = _chapters.List("ghost", includeSongs: false);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    private long SeedSong(string chapterName, string title)
    {
        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        long chapterId = new ChaptersRepository(_db).GetByName(documentId, chapterName).Value.Id;
        var song = Song.Create(title, ["x"], 1).Value;
        new SongsRepository(_db).Insert(chapterId, song);

        string chapterFolder = Path.Combine(DocumentPath, "Chapters", chapterName);
        File.WriteAllText(Path.Combine(chapterFolder, $"{title}.yml"), SongYamlSerializer.Serialize(song));
        return chapterId;
    }
}
