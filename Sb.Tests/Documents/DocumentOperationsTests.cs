using NUnit.Framework;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Documents;

[TestFixture]
public class DocumentOperationsTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private DocumentOperations _operations = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _operations = new DocumentOperations(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    // --- Create (7.1) ---

    [Test]
    public void Create_HappyPath_CreatesFolderFileAndDbRow()
    {
        var result = _operations.Create("my-doc", "My Document", "A subtitle", _outputFolder);

        Assert.That(result.IsSuccess, Is.True);
        string documentPath = Path.Combine(_outputFolder, "my-doc");
        Assert.That(Directory.Exists(documentPath), Is.True);
        string mainTex = File.ReadAllText(Path.Combine(documentPath, "my-doc.tex"));
        Assert.That(mainTex, Does.Contain("My Document"));
        Assert.That(mainTex, Does.Contain("A subtitle"));
        Assert.That(new DocumentsRepository(_db).GetByFilename("my-doc").IsSuccess, Is.True);
    }

    [Test]
    public void Create_RelativeOutputFolder_StoresAbsolutePath()
    {
        // A relative --output-folder must be resolved against the current directory at Create time
        // and stored absolute — otherwise a later rename/delete/build invoked from a different
        // working directory would resolve the stored path incorrectly or fail with not-found.
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_outputFolder);
        try
        {
            var result = _operations.Create("my-doc", "Title", null, "./sub-folder");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(Path.IsPathRooted(result.Value.Path), Is.True);
            Assert.That(result.Value.Path, Is.EqualTo(Path.Combine(_outputFolder, "sub-folder", "my-doc")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Create_DuplicateName_ReturnsConflict_NoSecondRowOrOverwrite()
    {
        _operations.Create("my-doc", "Title", null, _outputFolder);

        var result = _operations.Create("my-doc", "Other Title", null, _outputFolder);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        string mainTex = File.ReadAllText(Path.Combine(_outputFolder, "my-doc", "my-doc.tex"));
        Assert.That(mainTex, Does.Contain("Title"));
        Assert.That(mainTex, Does.Not.Contain("Other Title"));
    }

    [Test]
    public void Create_IllegalName_ReturnsValidation_NoFolderCreated()
    {
        var result = _operations.Create("foo/bar", "Title", null, _outputFolder);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "foo")), Is.False);
    }

    [Test]
    public void Create_BlankTitle_ReturnsValidation_NoFolderCreated()
    {
        var result = _operations.Create("my-doc", "   ", null, _outputFolder);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.False);
    }

    // --- List (7.2) ---

    [Test]
    public void List_EmptyDatabase_ReturnsEmptyList_NotAnError()
    {
        var result = _operations.List(includeChapters: false, includeSongs: false);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public void List_WithoutChapterFlag_DoesNotIncludeChapters()
    {
        CreateDocumentWithChapterAndSong(out _, out _, out _);

        var result = _operations.List(includeChapters: false, includeSongs: false);

        Assert.That(result.Value, Has.Count.EqualTo(1));
        Assert.That(result.Value[0].Chapters, Is.Empty);
    }

    [Test]
    public void List_WithChapterFlag_IncludesChapterNamesButNotSongs()
    {
        CreateDocumentWithChapterAndSong(out _, out string chapterName, out _);

        var result = _operations.List(includeChapters: true, includeSongs: false);

        Assert.That(result.Value[0].Chapters, Has.Count.EqualTo(1));
        Assert.That(result.Value[0].Chapters[0].Name, Is.EqualTo(chapterName));
        Assert.That(result.Value[0].Chapters[0].Songs, Is.Empty);
    }

    [Test]
    public void List_WithChapterAndSongFlags_GroupsSongsUnderChapters()
    {
        CreateDocumentWithChapterAndSong(out _, out _, out string songTitle);

        var result = _operations.List(includeChapters: true, includeSongs: true);

        Assert.That(result.Value[0].Chapters[0].Songs, Has.Count.EqualTo(1));
        Assert.That(result.Value[0].Chapters[0].Songs[0].Title, Is.EqualTo(songTitle));
    }

    // --- Delete (7.3) ---

    [Test]
    public void Delete_EmptyDocument_NoForceNeeded_DeletesFolderAndRow()
    {
        _operations.Create("my-doc", "Title", null, _outputFolder);

        var result = _operations.Delete("my-doc", force: false);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.False);
        Assert.That(new DocumentsRepository(_db).GetByFilename("my-doc").IsFailure, Is.True);
    }

    [Test]
    public void Delete_NonEmptyDocument_WithoutForce_IsBlocked_NothingDeleted()
    {
        CreateDocumentWithChapterAndSong(out string documentPath, out _, out _);

        var result = _operations.Delete("my-doc", force: false);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(Directory.Exists(documentPath), Is.True);
        Assert.That(new DocumentsRepository(_db).GetByFilename("my-doc").IsSuccess, Is.True);
    }

    [Test]
    public void Delete_NonEmptyDocument_WithForce_CascadesFully()
    {
        CreateDocumentWithChapterAndSong(out string documentPath, out _, out _);

        var result = _operations.Delete("my-doc", force: true);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.Exists(documentPath), Is.False);
        Assert.That(new DocumentsRepository(_db).GetByFilename("my-doc").IsFailure, Is.True);
    }

    [Test]
    public void Delete_NonexistentDocument_ReturnsNotFound_NotACrash()
    {
        var result = _operations.Delete("ghost", force: false);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    // --- Rename (7.4) ---

    [Test]
    public void Rename_HappyPath_MovesFolderAndFile_UpdatesDb()
    {
        _operations.Create("old-name", "Title", null, _outputFolder);

        var result = _operations.Rename("old-name", "new-name");

        Assert.That(result.IsSuccess, Is.True);
        string newPath = Path.Combine(_outputFolder, "new-name");
        Assert.That(Directory.Exists(newPath), Is.True);
        Assert.That(File.Exists(Path.Combine(newPath, "new-name.tex")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "old-name")), Is.False);
        var record = new DocumentsRepository(_db).GetByFilename("new-name").Value;
        Assert.That(record.Path, Is.EqualTo(newPath));
    }

    [Test]
    public void Rename_ToAlreadyTakenName_ReturnsConflict_NothingMoved()
    {
        _operations.Create("doc-a", "A", null, _outputFolder);
        _operations.Create("doc-b", "B", null, _outputFolder);

        var result = _operations.Rename("doc-b", "doc-a");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "doc-b")), Is.True);
    }

    [Test]
    public void Rename_NonexistentDocument_ReturnsNotFound()
    {
        var result = _operations.Rename("ghost", "new-name");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    // --- Build (7.5) ---

    [Test]
    public void Build_MultiColumnAsListed_WritesWellFormedMainAndChapterTexFiles()
    {
        CreateDocumentWithRealSongs(out string documentPath, out string chapterName);

        var result = _operations.Build("my-doc", SongOrdering.AsListed, LeafletFormat.MultiColumn);

        Assert.That(result.IsSuccess, Is.True);
        string mainTex = File.ReadAllText(Path.Combine(documentPath, "my-doc.tex"));
        Assert.That(mainTex, Does.Contain($"\\input{{Chapters/{chapterName}/{chapterName}.tex}}"));
        string chapterTex = File.ReadAllText(Path.Combine(documentPath, "Chapters", chapterName, $"{chapterName}.tex"));
        Assert.That(chapterTex, Does.Contain("\\chapter*"));
        Assert.That(chapterTex, Does.Contain("\\begin{minipage}[t]"));
    }

    [Test]
    public void Build_SingleColumn_SkipsMinipageLogicEntirely()
    {
        CreateDocumentWithRealSongs(out string documentPath, out string chapterName);

        var result = _operations.Build("my-doc", SongOrdering.AsListed, LeafletFormat.SingleColumn);

        Assert.That(result.IsSuccess, Is.True);
        string chapterTex = File.ReadAllText(Path.Combine(documentPath, "Chapters", chapterName, $"{chapterName}.tex"));
        Assert.That(chapterTex, Does.Not.Contain("minipage"));
        Assert.That(chapterTex, Does.Contain("\\paragraph*"));
    }

    [Test]
    public void Build_AsListedVsSpaceEfficient_ProduceDifferentSongPlacement()
    {
        CreateDocumentWithRealSongs(out string documentPath, out string chapterName);
        string chapterTexPath = Path.Combine(documentPath, "Chapters", chapterName, $"{chapterName}.tex");

        _operations.Build("my-doc", SongOrdering.AsListed, LeafletFormat.MultiColumn);
        string asListedTex = File.ReadAllText(chapterTexPath);

        _operations.Build("my-doc", SongOrdering.SpaceEfficient, LeafletFormat.MultiColumn);
        string spaceEfficientTex = File.ReadAllText(chapterTexPath);

        Assert.That(asListedTex, Is.Not.EqualTo(spaceEfficientTex));
    }

    [Test]
    public void Build_NonexistentDocument_ReturnsNotFound()
    {
        var result = _operations.Build("ghost", SongOrdering.AsListed, LeafletFormat.MultiColumn);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Build_DocumentWithNoChapters_SucceedsWithEmptyMainDocument()
    {
        _operations.Create("my-doc", "Title", null, _outputFolder);

        var result = _operations.Build("my-doc", SongOrdering.AsListed, LeafletFormat.MultiColumn);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Build_SongFileMissingFromDisk_ReturnsStorageError_NamesTheSong()
    {
        CreateDocumentWithChapterAndSong(out string documentPath, out string chapterName, out string songTitle);
        File.Delete(Path.Combine(documentPath, "Chapters", chapterName, $"{songTitle}.yml"));

        var result = _operations.Build("my-doc", SongOrdering.AsListed, LeafletFormat.MultiColumn);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(result.Error!.Message, Does.Contain(songTitle));
    }

    // --- helpers: seed DB + filesystem state that `chapters add`/`songs add` (Milestones 8/9) will
    // eventually create, so Build/List/Delete have something non-trivial to operate on. ---

    private void CreateDocumentWithChapterAndSong(out string documentPath, out string chapterName, out string songTitle)
    {
        _operations.Create("my-doc", "Title", null, _outputFolder);
        documentPath = Path.Combine(_outputFolder, "my-doc");
        chapterName = "chapter-1";
        songTitle = "Lille Albin";

        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        long chapterId = new ChaptersRepository(_db).Insert(documentId, chapterName, "Chapter One").Value;
        Song song = Song.Create(songTitle, ["En rad", "En till rad"], 1).Value;
        new SongsRepository(_db).Insert(chapterId, song);

        string chapterFolder = Path.Combine(documentPath, "Chapters", chapterName);
        Directory.CreateDirectory(chapterFolder);
        File.WriteAllText(Path.Combine(chapterFolder, $"{songTitle}.yml"), SongYamlSerializer.Serialize(song));
    }

    private void CreateDocumentWithRealSongs(out string documentPath, out string chapterName)
    {
        _operations.Create("my-doc", "Title", null, _outputFolder);
        documentPath = Path.Combine(_outputFolder, "my-doc");
        chapterName = "chapter-1";

        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        long chapterId = new ChaptersRepository(_db).Insert(documentId, chapterName, "Chapter One").Value;

        string chapterFolder = Path.Combine(documentPath, "Chapters", chapterName);
        Directory.CreateDirectory(chapterFolder);

        var songsRepository = new SongsRepository(_db);
        string[] fixtureFiles = Directory.GetFiles("TestFiles", "*.txt");
        for (int i = 0; i < fixtureFiles.Length; i++)
        {
            string title = Path.GetFileNameWithoutExtension(fixtureFiles[i]);
            Song song = Song.Create(title, File.ReadAllLines(fixtureFiles[i]), i + 1).Value;
            songsRepository.Insert(chapterId, song);
            File.WriteAllText(Path.Combine(chapterFolder, $"{title}.yml"), SongYamlSerializer.Serialize(song));
        }
    }
}
