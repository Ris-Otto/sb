namespace Sb.Core.Storage;

/// <summary>
/// Detects drift between the database and the filesystem (FR-1.7.5) — expected to arise only from
/// changes made outside of SB, since SB's own operations are atomic (see <see cref="AtomicOperation"/>).
/// Used by the listings, which show what is missing, and by the operations that need a document's or
/// chapter's folder, which stop with a clear error instead of failing obscurely or recreating the folder.
/// </summary>
public static class MismatchDetector
{
    public static IReadOnlyList<StorageMismatch> FindDocumentMismatches(IEnumerable<DocumentRecord> documents) =>
        documents
            .Where(d => !Directory.Exists(d.Path))
            .Select(d => new StorageMismatch(d.Filename, d.Path))
            .ToList();

    public static IReadOnlyList<StorageMismatch> FindChapterMismatches(
        DocumentRecord document, IEnumerable<ChapterRecord> chapters) =>
        chapters
            .Select(c => new StorageMismatch(c.Name, DocumentLayout.ChapterFolder(document.Path, c.Name)))
            .Where(m => !Directory.Exists(m.ExpectedPath))
            .ToList();

    public static IReadOnlyList<StorageMismatch> FindSongMismatches(
        DocumentRecord document, ChapterRecord chapter, IEnumerable<SongRecord> songs) =>
        songs
            .Select(s => new StorageMismatch(s.Title, DocumentLayout.SongFile(document.Path, chapter.Name, s.Title)))
            .Where(m => !File.Exists(m.ExpectedPath))
            .ToList();

    /// <summary>A failed result naming the missing document folder, or success if it is there.</summary>
    public static Result CheckDocumentFolder(DocumentRecord document) =>
        Directory.Exists(document.Path)
            ? Result.Success()
            : Result.Failure(new Error(
                ErrorKind.Storage,
                $"Document '{document.Filename}' is recorded at '{document.Path}', but that folder doesn't exist — " +
                "it was probably moved or deleted outside sb. Move it back, or remove the document from sb with " +
                "'documents delete'.",
                document.Filename));

    /// <summary>A failed result naming the missing chapter folder, or success if it is there.</summary>
    public static Result CheckChapterFolder(DocumentRecord document, ChapterRecord chapter)
    {
        string folder = DocumentLayout.ChapterFolder(document.Path, chapter.Name);
        return Directory.Exists(folder)
            ? Result.Success()
            : Result.Failure(new Error(
                ErrorKind.Storage,
                $"Chapter '{chapter.Name}' of document '{document.Filename}' is recorded at '{folder}', but that " +
                "folder doesn't exist — it was probably moved or deleted outside sb. Move it back, or remove the " +
                "chapter from sb with 'chapters delete'.",
                chapter.Name));
    }
}
