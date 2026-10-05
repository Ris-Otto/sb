using NUnit.Framework;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Documents;

[TestFixture]
public class SongOperationsTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private DocumentOperations _documents = null!;
    private ChapterOperations _chapters = null!;
    private SongOperations _songs = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-song-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _documents = new DocumentOperations(_db);
        _chapters = new ChapterOperations(_db);
        _songs = new SongOperations(_db);
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    private string ChapterFolder => Path.Combine(_outputFolder, "my-doc", "Chapters", "chapter-1");

    // --- Add (9.1) ---

    [Test]
    public void Add_LiteralText_CreatesSongFileAndDbRow()
    {
        var result = _songs.Add("my-doc", "chapter-1", "Lille Albin", "Rad 1\nRad 2", writer: "Okänd", melody: "Trad.", number: null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Number, Is.EqualTo(1));
        string yamlPath = Path.Combine(ChapterFolder, "Lille Albin.yml");
        Assert.That(File.Exists(yamlPath), Is.True);
        var song = SongYamlSerializer.Deserialize(File.ReadAllText(yamlPath)).Value;
        Assert.That(song.Text, Is.EqualTo(new[] { "Rad 1", "Rad 2" }));
        Assert.That(song.Writer, Is.EqualTo("Okänd"));
    }

    [Test]
    public void Add_LiteralTextVsFilePathText_ProduceIdenticalStoredContent()
    {
        string fixturePath = Path.Combine("TestFiles", "Lille_albin.txt");
        string literalText = string.Join('\n', File.ReadAllLines(fixturePath));

        _songs.Add("my-doc", "chapter-1", "From Literal", literalText, null, null, null);
        _songs.Add("my-doc", "chapter-1", "From File", fixturePath, null, null, null);

        var fromLiteral = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "From Literal.yml"))).Value;
        var fromFile = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "From File.yml"))).Value;
        Assert.That(fromLiteral.Text, Is.EqualTo(fromFile.Text));
    }

    [Test]
    public void Add_NumberOmitted_AssignsMaxPlusOne()
    {
        _songs.Add("my-doc", "chapter-1", "First", "x", null, null, null);

        var result = _songs.Add("my-doc", "chapter-1", "Second", "x", null, null, null);

        Assert.That(result.Value.Number, Is.EqualTo(2));
    }

    [Test]
    public void Add_ExplicitNumberInMiddle_ShiftsExistingSongsUp()
    {
        _songs.Add("my-doc", "chapter-1", "First", "x", null, null, null);
        _songs.Add("my-doc", "chapter-1", "Second", "x", null, null, null);

        var result = _songs.Add("my-doc", "chapter-1", "Inserted", "x", null, null, number: 2);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Number, Is.EqualTo(2));
        Assert.That(new SongsRepository(_db).GetByTitle(result.Value.ChapterId, "Second").Value.Number, Is.EqualTo(3));
        var secondFile = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "Second.yml"))).Value;
        Assert.That(secondFile.Number, Is.EqualTo(3), "the shifted song's own yaml file must also reflect its new number");
    }

    [Test]
    public void Add_DuplicateTitleInSameChapter_ReturnsConflict()
    {
        _songs.Add("my-doc", "chapter-1", "Lille Albin", "x", null, null, null);

        var result = _songs.Add("my-doc", "chapter-1", "Lille Albin", "y", null, null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
    }

    [Test]
    public void Add_NonexistentDocument_ReturnsNotFound()
    {
        var result = _songs.Add("ghost", "chapter-1", "Title", "x", null, null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Add_NonexistentChapter_ReturnsNotFound()
    {
        var result = _songs.Add("my-doc", "ghost-chapter", "Title", "x", null, null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Add_NumberOutOfRange_ReturnsValidation()
    {
        var result = _songs.Add("my-doc", "chapter-1", "Title", "x", null, null, number: 5);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    // --- Delete (9.2) ---

    [Test]
    public void Delete_MiddleSong_ShiftsLaterNumbersDown()
    {
        _songs.Add("my-doc", "chapter-1", "First", "x", null, null, null);
        _songs.Add("my-doc", "chapter-1", "Second", "x", null, null, null);
        _songs.Add("my-doc", "chapter-1", "Third", "x", null, null, null);

        var result = _songs.Delete("my-doc", "Second");

        Assert.That(result.IsSuccess, Is.True);
        long chapterId = new ChaptersRepository(_db).GetByName(
            new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id, "chapter-1").Value.Id;
        Assert.That(new SongsRepository(_db).GetByTitle(chapterId, "Third").Value.Number, Is.EqualTo(2));
        var thirdFile = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "Third.yml"))).Value;
        Assert.That(thirdFile.Number, Is.EqualTo(2));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Second.yml")), Is.False);
    }

    [Test]
    public void Delete_NonexistentSongTitle_ReturnsNotFound()
    {
        var result = _songs.Delete("my-doc", "Ghost Song");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Delete_TitleExistsInTwoChapters_ReturnsConflict_NotAnArbitraryPick()
    {
        _chapters.Add("my-doc", "chapter-2", null);
        _songs.Add("my-doc", "chapter-1", "Shared Title", "x", null, null, null);
        _songs.Add("my-doc", "chapter-2", "Shared Title", "y", null, null, null);

        var result = _songs.Delete("my-doc", "Shared Title");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
    }

    // --- Modify (9.4) ---

    [Test]
    public void Modify_OnlyWriter_LeavesTitleMelodyNumberUntouched()
    {
        _songs.Add("my-doc", "chapter-1", "Lille Albin", "x", writer: "Old Writer", melody: "A Melody", number: null);

        var result = _songs.Modify("my-doc", "Lille Albin", newTitle: null, writer: "New Writer", melody: null, newChapterName: null, newNumber: null);

        Assert.That(result.IsSuccess, Is.True);
        var song = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "Lille Albin.yml"))).Value;
        Assert.That(song.Writer, Is.EqualTo("New Writer"));
        Assert.That(song.Melody, Is.EqualTo("A Melody"));
        Assert.That(song.Number, Is.EqualTo(1));
        Assert.That(song.Title, Is.EqualTo("Lille Albin"));
    }

    [Test]
    public void Modify_Number_ShiftsOtherSongsTheSameWayAsAdd()
    {
        _songs.Add("my-doc", "chapter-1", "First", "x", null, null, null);
        _songs.Add("my-doc", "chapter-1", "Second", "x", null, null, null);
        _songs.Add("my-doc", "chapter-1", "Third", "x", null, null, null);

        var result = _songs.Modify("my-doc", "Third", null, null, null, null, newNumber: 1);

        Assert.That(result.IsSuccess, Is.True);
        long chapterId = new ChaptersRepository(_db).GetByName(
            new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id, "chapter-1").Value.Id;
        var repo = new SongsRepository(_db);
        Assert.That(repo.GetByTitle(chapterId, "Third").Value.Number, Is.EqualTo(1));
        Assert.That(repo.GetByTitle(chapterId, "First").Value.Number, Is.EqualTo(2));
        Assert.That(repo.GetByTitle(chapterId, "Second").Value.Number, Is.EqualTo(3));
    }

    [Test]
    public void Modify_Chapter_MovesFileAndDbRowAtomically()
    {
        _chapters.Add("my-doc", "chapter-2", null);
        _songs.Add("my-doc", "chapter-1", "Lille Albin", "x", null, null, null);
        string destFolder = Path.Combine(_outputFolder, "my-doc", "Chapters", "chapter-2");

        var result = _songs.Modify("my-doc", "Lille Albin", null, null, null, newChapterName: "chapter-2", newNumber: null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Lille Albin.yml")), Is.False);
        Assert.That(File.Exists(Path.Combine(destFolder, "Lille Albin.yml")), Is.True);
        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        long destChapterId = new ChaptersRepository(_db).GetByName(documentId, "chapter-2").Value.Id;
        Assert.That(new SongsRepository(_db).GetByTitle(destChapterId, "Lille Albin").IsSuccess, Is.True);
    }

    [Test]
    public void Modify_ChapterMove_RenumbersBothSourceAndDestination()
    {
        _chapters.Add("my-doc", "chapter-2", null);
        _songs.Add("my-doc", "chapter-1", "A", "x", null, null, null);
        _songs.Add("my-doc", "chapter-1", "B", "x", null, null, null);
        _songs.Add("my-doc", "chapter-2", "C", "x", null, null, null);

        var result = _songs.Modify("my-doc", "A", null, null, null, newChapterName: "chapter-2", newNumber: null);

        Assert.That(result.IsSuccess, Is.True);
        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        long sourceId = new ChaptersRepository(_db).GetByName(documentId, "chapter-1").Value.Id;
        long destId = new ChaptersRepository(_db).GetByName(documentId, "chapter-2").Value.Id;
        var repo = new SongsRepository(_db);
        Assert.That(repo.GetByTitle(sourceId, "B").Value.Number, Is.EqualTo(1), "B should close the gap A left behind");
        Assert.That(repo.GetByTitle(destId, "C").Value.Number, Is.EqualTo(1));
        Assert.That(repo.GetByTitle(destId, "A").Value.Number, Is.EqualTo(2), "A appends after C by default");
    }

    [Test]
    public void Modify_TitleRename_RenamesFileInPlace()
    {
        _songs.Add("my-doc", "chapter-1", "Old Title", "x", null, null, null);

        var result = _songs.Modify("my-doc", "Old Title", newTitle: "New Title", null, null, null, null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Old Title.yml")), Is.False);
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "New Title.yml")), Is.True);
    }

    [Test]
    public void Modify_ToTitleTakenInDestinationChapter_ReturnsConflict()
    {
        _songs.Add("my-doc", "chapter-1", "Taken", "x", null, null, null);

        var result = _songs.Modify("my-doc", "Taken", newTitle: "Taken", null, null, null, null);

        // Renaming a song to its OWN current title is a no-op in practice, but renaming it to another
        // EXISTING song's title must be rejected:
        _songs.Add("my-doc", "chapter-1", "Other", "x", null, null, null);
        var collisionResult = _songs.Modify("my-doc", "Other", newTitle: "Taken", null, null, null, null);

        Assert.That(collisionResult.IsFailure, Is.True);
        Assert.That(collisionResult.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
    }

    [Test]
    public void Modify_NoFieldsGiven_ReturnsValidation()
    {
        _songs.Add("my-doc", "chapter-1", "Lille Albin", "x", null, null, null);

        var result = _songs.Modify("my-doc", "Lille Albin", null, null, null, null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Modify_NonexistentSong_ReturnsNotFound()
    {
        var result = _songs.Modify("my-doc", "Ghost", "New Title", null, null, null, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    // --- List (9.3) ---

    [Test]
    public void List_Unfiltered_ReturnsSongsAcrossAllChapters()
    {
        _chapters.Add("my-doc", "chapter-2", null);
        _songs.Add("my-doc", "chapter-1", "Song A", "x", null, null, null);
        _songs.Add("my-doc", "chapter-2", "Song B", "x", null, null, null);

        var result = _songs.List("my-doc", chapterName: null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(s => s.Title), Is.EquivalentTo(new[] { "Song A", "Song B" }));
    }

    [Test]
    public void List_FilteredByChapter_ReturnsOnlyThatChaptersSongs()
    {
        _chapters.Add("my-doc", "chapter-2", null);
        _songs.Add("my-doc", "chapter-1", "Song A", "x", null, null, null);
        _songs.Add("my-doc", "chapter-2", "Song B", "x", null, null, null);

        var result = _songs.List("my-doc", chapterName: "chapter-1");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(s => s.Title), Is.EqualTo(new[] { "Song A" }));
    }

    [Test]
    public void List_NonexistentChapterFilter_ReturnsNotFound()
    {
        var result = _songs.List("my-doc", chapterName: "ghost-chapter");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void List_NonexistentDocument_ReturnsNotFound()
    {
        var result = _songs.List("ghost", chapterName: null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }
}
