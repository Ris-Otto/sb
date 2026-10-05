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
        chapters.Insert(documents.Insert(docName, "/" + docName, docName).Value, "Chapter", "Chapter").Value;

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

    [Test]
    public void GetAllForChapter_ReturnsSongsInNumberOrder()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");
        songs.Insert(chapterId, MakeSong("Second", 2));
        songs.Insert(chapterId, MakeSong("First", 1));

        var result = songs.GetAllForChapter(chapterId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(s => s.Title), Is.EqualTo(new[] { "First", "Second" }));
    }

    [Test]
    public void GetAllForChapter_NoSongs_ReturnsEmptyList()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");

        var result = songs.GetAllForChapter(chapterId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public void Delete_ExistingId_RemovesRow()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");
        long songId = songs.Insert(chapterId, MakeSong("Lille Albin", 1)).Value;

        var result = songs.Delete(songId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(songs.GetByTitle(chapterId, "Lille Albin").IsFailure, Is.True);
    }

    [Test]
    public void Delete_NonExistentId_ReturnsNotFound()
    {
        var (_, _, songs) = MakeDatabase();

        var result = songs.Delete(999);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void UpdateChapterTitleAndNumber_ChangesAllThreeFields()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterA = MakeChapter(documents, chapters, "doc-a");
        long chapterB = chapters.Insert(documents.GetByFilename("doc-a").Value.Id, "chapter-b", "Chapter B").Value;
        long songId = songs.Insert(chapterA, MakeSong("Old Title", 1)).Value;

        var result = songs.UpdateChapterTitleAndNumber(songId, chapterB, "New Title", 2);

        Assert.That(result.IsSuccess, Is.True);
        var updated = songs.GetByTitle(chapterB, "New Title");
        Assert.That(updated.IsSuccess, Is.True);
        Assert.That(updated.Value.Number, Is.EqualTo(2));
    }

    [Test]
    public void UpdateChapterTitleAndNumber_ToTakenTitleInDestinationChapter_ReturnsConflict()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");
        songs.Insert(chapterId, MakeSong("Taken", 1));
        long songId = songs.Insert(chapterId, MakeSong("Movable", 2)).Value;

        var result = songs.UpdateChapterTitleAndNumber(songId, chapterId, "Taken", 2);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
    }

    [Test]
    public void UpdateChapterTitleAndNumber_NonExistentChapterId_ReturnsNotFound()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");
        long songId = songs.Insert(chapterId, MakeSong("Lille Albin", 1)).Value;

        var result = songs.UpdateChapterTitleAndNumber(songId, 999, "Lille Albin", 1);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }

    [Test]
    public void UpdateChapterTitleAndNumber_NonExistentSongId_ReturnsNotFound()
    {
        var (documents, chapters, songs) = MakeDatabase();
        long chapterId = MakeChapter(documents, chapters, "doc");

        var result = songs.UpdateChapterTitleAndNumber(999, chapterId, "Title", 1);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
    }
}
