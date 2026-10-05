using NUnit.Framework;
using Sb.Core;
using Sb.Core.Storage;

namespace Sb.Tests.Storage;

[TestFixture]
public class SongsRepositoryTests
{
    private static (DocumentsRepository Documents, ChaptersRepository Chapters, SongsRepository Songs) MakeDatabase()
    {
        var db = Database.Open("Data Source=:memory:");
        return (new DocumentsRepository(db), new ChaptersRepository(db), new SongsRepository(db));
    }

    private static long MakeChapter(DocumentsRepository documents, ChaptersRepository chapters, string docName) =>
        chapters.Insert(documents.Insert(docName, "/" + docName).Value, "Chapter").Value;

    private static Song MakeSong(string title, int number) => Song.Create(title, ["x"], number).Value;

    [Test]
    public void Insert_DuplicateTitleInSameChapter_ReturnsConflict()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");
        songs.Insert(chapterId, MakeSong("Lille Albin", 1));

        var result = songs.Insert(chapterId, MakeSong("Lille Albin", 2));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("Lille Albin"));
    }

    [Test]
    public void Insert_SameTitleInDifferentChapter_Succeeds()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterA = MakeChapter(documents, chapters, "doc-a");
        long chapterB = MakeChapter(documents, chapters, "doc-b");
        songs.Insert(chapterA, MakeSong("Lille Albin", 1));

        var result = songs.Insert(chapterB, MakeSong("Lille Albin", 1));

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Insert_NonExistentChapterId_ReturnsNotFound()
    {
        var (_, _, songs) = MakeDatabase();

        var result = songs.Insert(999, MakeSong("Lille Albin", 1));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void GetByTitle_Found_ReturnsRecord()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");
        long songId = songs.Insert(chapterId, MakeSong("Lille Albin", 1)).Value;

        var result = songs.GetByTitle(chapterId, "Lille Albin");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new SongRecord(songId, chapterId, "Lille Albin", 1)));
    }

    [Test]
    public void GetByTitle_NotFound_ReturnsNotFoundError()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");

        var result = songs.GetByTitle(chapterId, "Ghost Song");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
        Assert.That(result.Error!.SubjectName, Is.EqualTo("Ghost Song"));
    }
}
