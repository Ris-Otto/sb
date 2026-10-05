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

        var result = repository.Insert("my-doc", "/some/path");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.GreaterThan(0));
    }

    [Test]
    public void Insert_DuplicateFilename_ReturnsConflict()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        repository.Insert("my-doc", "/some/path");

        var result = repository.Insert("my-doc", "/some/other/path");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("my-doc"));
    }

    [Test]
    public void Insert_IllegalFilename_ReturnsValidation_NoRowInserted()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.Insert("foo/bar", "/some/path");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
        Assert.That(repository.GetAll().Value, Is.Empty);
    }

    [Test]
    public void Rename_ToIllegalName_ReturnsValidation_NameUnchanged()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("old-name", "/some/path").Value;

        var result = repository.Rename(id, "foo/bar");

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
        long id = repository.Insert("my-doc", "/some/path").Value;

        var result = repository.GetByFilename("my-doc");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new DocumentRecord(id, "my-doc", "/some/path")));
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
        long id = repository.Insert("old-name", "/some/path").Value;

        var result = repository.Rename(id, "new-name");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repository.GetByFilename("new-name").IsSuccess, Is.True);
    }

    [Test]
    public void Rename_ToAlreadyTakenName_ReturnsConflict()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        repository.Insert("doc-a", "/path-a");
        long docB = repository.Insert("doc-b", "/path-b").Value;

        var result = repository.Rename(docB, "doc-a");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(repository.GetByFilename("doc-b").IsSuccess, Is.True, "rename must not have partially applied");
    }

    [Test]
    public void Rename_NonExistentId_ReturnsNotFound()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.Rename(999, "new-name");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }
}
