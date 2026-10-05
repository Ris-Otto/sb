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
    public void GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        var result = repository.GetAll();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }
}
