using NUnit.Framework;
using Sb.Core;
using Sb.Core.Storage;

namespace Sb.Tests.Storage;

[TestFixture]
public class ChaptersRepositoryTests
{
    private static (Database Db, DocumentsRepository Documents, ChaptersRepository Chapters) MakeDatabase()
    {
        var db = Database.Open("Data Source=:memory:");
        return (db, new DocumentsRepository(db), new ChaptersRepository(db));
    }

    [Test]
    public void Insert_DuplicateNameInSameDocument_ReturnsConflict()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path").Value;
        chapters.Insert(documentId, "Chapter One");

        var result = chapters.Insert(documentId, "Chapter One");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("Chapter One"));
    }

    [Test]
    public void Insert_SameNameInDifferentDocument_Succeeds()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentA = documents.Insert("doc-a", "/path-a").Value;
        long documentB = documents.Insert("doc-b", "/path-b").Value;
        chapters.Insert(documentA, "Chapter One");

        var result = chapters.Insert(documentB, "Chapter One");

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Insert_IllegalName_ReturnsValidation()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path").Value;

        var result = chapters.Insert(documentId, "foo/bar");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Insert_NonExistentDocumentId_ReturnsNotFound()
    {
        var (_, _, chapters) = MakeDatabase();

        var result = chapters.Insert(999, "Chapter One");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void GetByName_Found_ReturnsRecord()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path").Value;
        long chapterId = chapters.Insert(documentId, "Chapter One").Value;

        var result = chapters.GetByName(documentId, "Chapter One");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new ChapterRecord(chapterId, documentId, "Chapter One")));
    }

    [Test]
    public void GetByName_NotFound_ReturnsNotFoundError()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path").Value;

        var result = chapters.GetByName(documentId, "Ghost Chapter");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("Ghost Chapter"));
    }
}
