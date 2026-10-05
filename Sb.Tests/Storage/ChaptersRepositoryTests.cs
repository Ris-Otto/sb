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
        long documentId = documents.Insert("doc", "/path", "Doc").Value;
        chapters.Insert(documentId, "Chapter One", "Chapter One");

        var result = chapters.Insert(documentId, "Chapter One", "Chapter One Again");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("Chapter One"));
    }

    [Test]
    public void Insert_SameNameInDifferentDocument_Succeeds()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentA = documents.Insert("doc-a", "/path-a", "Doc A").Value;
        long documentB = documents.Insert("doc-b", "/path-b", "Doc B").Value;
        chapters.Insert(documentA, "Chapter One", "Chapter One");

        var result = chapters.Insert(documentB, "Chapter One", "Chapter One");

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Insert_IllegalName_ReturnsValidation()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path", "Doc").Value;

        var result = chapters.Insert(documentId, "foo/bar", "Title");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Insert_NonExistentDocumentId_ReturnsNotFound()
    {
        var (_, _, chapters) = MakeDatabase();

        var result = chapters.Insert(999, "Chapter One", "Chapter One");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void GetByName_Found_ReturnsRecord()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path", "Doc").Value;
        long chapterId = chapters.Insert(documentId, "Chapter One", "Chapter One").Value;

        var result = chapters.GetByName(documentId, "Chapter One");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new ChapterRecord(chapterId, documentId, "Chapter One", "Chapter One")));
    }

    [Test]
    public void GetByName_NotFound_ReturnsNotFoundError()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path", "Doc").Value;

        var result = chapters.GetByName(documentId, "Ghost Chapter");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("Ghost Chapter"));
    }

    [Test]
    public void GetAllForDocument_ReturnsChaptersInInsertionOrder()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path", "Doc").Value;
        chapters.Insert(documentId, "chapter-1", "Chapter One");
        chapters.Insert(documentId, "chapter-2", "Chapter Two");

        var result = chapters.GetAllForDocument(documentId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(c => c.Name), Is.EqualTo(new[] { "chapter-1", "chapter-2" }));
    }

    [Test]
    public void GetAllForDocument_NoChapters_ReturnsEmptyList()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path", "Doc").Value;

        var result = chapters.GetAllForDocument(documentId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public void Delete_ExistingId_RemovesRow()
    {
        var (_, documents, chapters) = MakeDatabase();
        long documentId = documents.Insert("doc", "/path", "Doc").Value;
        long chapterId = chapters.Insert(documentId, "chapter-1", "Chapter One").Value;

        var result = chapters.Delete(chapterId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(chapters.GetByName(documentId, "chapter-1").IsFailure, Is.True);
    }

    [Test]
    public void Delete_NonExistentId_ReturnsNotFound()
    {
        var (_, _, chapters) = MakeDatabase();

        var result = chapters.Delete(999);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void Delete_CascadesToSongs()
    {
        var (db, documents, chapters) = MakeDatabase();
        var songs = new SongsRepository(db);
        long documentId = documents.Insert("doc", "/path", "Doc").Value;
        long chapterId = chapters.Insert(documentId, "chapter-1", "Chapter One").Value;
        songs.Insert(chapterId, Song.Create("Lille Albin", ["x"], 1).Value);

        var result = chapters.Delete(chapterId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(songs.GetByTitle(chapterId, "Lille Albin").IsFailure, Is.True);
    }
}
