using NUnit.Framework;
using Sb.Core;
using Sb.Core.Storage;

namespace Sb.Tests.Storage;

[TestFixture]
public class DocumentsRepositoryTests
{
    [Test]
    public void Insert_NewFilename_ReturnsNewId()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.Insert("my-doc", "/some/path", "My Doc");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.GreaterThan(0));
    }

    [Test]
    public void Insert_DuplicateFilename_ReturnsConflict()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        repository.Insert("my-doc", "/some/path", "My Doc");

        var result = repository.Insert("my-doc", "/some/other/path", "My Doc Again");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("my-doc"));
    }

    [Test]
    public void Insert_IllegalFilename_ReturnsValidation_NoRowInserted()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.Insert("foo/bar", "/some/path", "Title");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
        Assert.That(repository.GetAll().Value, Is.Empty);
    }

    [Test]
    public void Rename_ToIllegalName_ReturnsValidation_NameUnchanged()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("old-name", "/some/path", "Title").Value;

        var result = repository.Rename(id, "foo/bar", "/some/other-path");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
        Assert.That(repository.GetByFilename("old-name").IsSuccess, Is.True);
    }

    [Test]
    public void GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.GetAll();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public void GetByFilename_Found_ReturnsRecord()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("my-doc", "/some/path", "My Doc", "A subtitle").Value;

        var result = repository.GetByFilename("my-doc");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new DocumentRecord(id, "my-doc", "/some/path", "My Doc", "A subtitle")));
    }

    [Test]
    public void GetByFilename_NoSubtitleGiven_ReturnsNullSubtitle()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        repository.Insert("my-doc", "/some/path", "My Doc");

        var result = repository.GetByFilename("my-doc");

        Assert.That(result.Value.Subtitle, Is.Null);
    }

    [Test]
    public void GetByFilename_NotFound_ReturnsNotFoundError()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.GetByFilename("ghost");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("ghost"));
    }

    [Test]
    public void Rename_ToUntakenName_Succeeds()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("old-name", "/some/path", "Title").Value;

        var result = repository.Rename(id, "new-name", "/some/new-path");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repository.GetByFilename("new-name").Value.Path, Is.EqualTo("/some/new-path"));
    }

    [Test]
    public void Rename_ToAlreadyTakenName_ReturnsConflict()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        repository.Insert("doc-a", "/path-a", "Doc A");
        long docB = repository.Insert("doc-b", "/path-b", "Doc B").Value;

        var result = repository.Rename(docB, "doc-a", "/path-a-renamed");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(repository.GetByFilename("doc-b").IsSuccess, Is.True, "rename must not have partially applied");
    }

    [Test]
    public void Rename_NonExistentId_ReturnsNotFound()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.Rename(999, "new-name", "/some/path");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Delete_ExistingId_RemovesRow()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("my-doc", "/some/path", "Title").Value;

        var result = repository.Delete(id);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repository.GetByFilename("my-doc").IsFailure, Is.True);
    }

    [Test]
    public void Delete_NonExistentId_ReturnsNotFound()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.Delete(999);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Delete_CascadesToChaptersAndSongs()
    {
        using var db = Database.Open("Data Source=:memory:");
        var documents = new DocumentsRepository(db);
        var chapters = new ChaptersRepository(db);
        var songs = new SongsRepository(db);
        long documentId = documents.Insert("my-doc", "/some/path", "Title").Value;
        long chapterId = chapters.Insert(documentId, "chapter-1", "Chapter One").Value;
        songs.Insert(chapterId, Song.Create("Lille Albin", ["x"], 1).Value);

        var result = documents.Delete(documentId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(chapters.GetByName(documentId, "chapter-1").IsFailure, Is.True);
        Assert.That(songs.GetByTitle(chapterId, "Lille Albin").IsFailure, Is.True);
    }
}
