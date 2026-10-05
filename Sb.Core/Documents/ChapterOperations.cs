using Sb.Core.LaTeX;
using Sb.Core.Storage;

namespace Sb.Core.Documents;

/// <summary>
/// The CLI-independent implementation of the three `chapters` subcommands (FR-1.10), mirroring
/// <see cref="DocumentOperations"/>'s shape at the chapter scope.
/// </summary>
public sealed class ChapterOperations(Database database)
{
    private readonly DocumentsRepository _documents = new(database);
    private readonly ChaptersRepository _chapters = new(database);
    private readonly SongsRepository _songs = new(database);

    public Result<ChapterRecord> Add(string documentName, string? name, string? title)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return Result<ChapterRecord>.Failure(documentResult.Error!);
        DocumentRecord document = documentResult.Value;

        Result<IReadOnlyList<ChapterRecord>> existingResult = _chapters.GetAllForDocument(document.Id);
        if (existingResult.IsFailure)
            return Result<ChapterRecord>.Failure(existingResult.Error!);

        string chapterName = name ?? NextDefaultName(documentName, existingResult.Value);
        string chapterTitle = title ?? chapterName;

        Result nameValidity = NameValidator.Validate(chapterName);
        if (nameValidity.IsFailure)
            return Result<ChapterRecord>.Failure(nameValidity.Error!);

        // Checked up front, before any filesystem write — same reasoning as DocumentOperations.Create:
        // a conflict discovered only once Do() is already underway would make its rollback delete a
        // folder this call never actually created.
        if (_chapters.GetByName(document.Id, chapterName).IsSuccess)
            return Result<ChapterRecord>.Failure(new Error(
                ErrorKind.Conflict, $"A chapter named '{chapterName}' already exists in document '{documentName}'.",
                chapterName));

        string chapterFolder = DocumentLayout.ChapterFolder(document.Path, chapterName);
        string chapterTexPath = DocumentLayout.ChapterTexFile(document.Path, chapterName);
        string placeholderTex = ChapterEmitter.RenderMultiColumn(chapterTitle, [], LayoutDefaults.ColumnWidth);

        var filesystem = new ReversibleAction(
            Do: () =>
            {
                if (Directory.Exists(chapterFolder))
                    throw new IOException($"A folder already exists at '{chapterFolder}'.");
                Directory.CreateDirectory(chapterFolder);
                try
                {
                    File.WriteAllText(chapterTexPath, placeholderTex);
                }
                catch
                {
                    Directory.Delete(chapterFolder, recursive: true);
                    throw;
                }
            },
            Undo: () => Directory.Delete(chapterFolder, recursive: true));

        Result<long> result = AtomicOperation.Execute(
            filesystem, () => _chapters.Insert(document.Id, chapterName, chapterTitle));

        return result.IsSuccess
            ? Result<ChapterRecord>.Success(new ChapterRecord(result.Value, document.Id, chapterName, chapterTitle))
            : Result<ChapterRecord>.Failure(result.Error!);
    }

    public Result Delete(string documentName, IReadOnlyList<string> chapterNames, bool force)
    {
        if (chapterNames.Count == 0)
            return Result.Failure(new Error(ErrorKind.Validation, "At least one chapter name must be given."));

        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return documentResult;
        DocumentRecord document = documentResult.Value;

        // Resolve every named chapter before deleting any of them, so a name that doesn't exist is
        // reported as not-found rather than silently skipped after other chapters have already gone.
        var chapters = new List<ChapterRecord>();
        foreach (string chapterName in chapterNames)
        {
            Result<ChapterRecord> chapterResult = _chapters.GetByName(document.Id, chapterName);
            if (chapterResult.IsFailure)
                return chapterResult;
            chapters.Add(chapterResult.Value);
        }

        if (!force)
        {
            var nonEmpty = new List<string>();
            foreach (ChapterRecord chapter in chapters)
            {
                Result<IReadOnlyList<SongRecord>> songsResult = _songs.GetAllForChapter(chapter.Id);
                if (songsResult.IsFailure)
                    return songsResult;
                if (songsResult.Value.Count > 0)
                    nonEmpty.Add($"'{chapter.Name}' ({songsResult.Value.Count} song(s))");
            }

            if (nonEmpty.Count > 0)
                return Result.Failure(new Error(
                    ErrorKind.Conflict,
                    $"This would cascade into non-empty chapter(s): {string.Join(", ", nonEmpty)}. " +
                    "Re-run with --force to delete them and everything inside them.",
                    documentName));
        }

        // Every named chapter is attempted, even after an earlier one fails, so a batch delete never
        // silently stops partway through — the caller is told exactly which ones went through and
        // which didn't, rather than being left to guess at the overall state from a single error that
        // only describes the first failure.
        var failures = new List<string>();
        var succeeded = new List<string>();
        foreach (ChapterRecord chapter in chapters)
        {
            Result deleteResult = AtomicOperation.ExecuteDelete(
                () => _chapters.Delete(chapter.Id),
                filesystemCleanup: () =>
                    Directory.Delete(DocumentLayout.ChapterFolder(document.Path, chapter.Name), recursive: true));

            if (deleteResult.IsSuccess)
                succeeded.Add(chapter.Name);
            else
                failures.Add($"'{chapter.Name}': {deleteResult.Error!.Message}");
        }

        if (failures.Count == 0)
            return Result.Success();

        string succeededNote = succeeded.Count > 0 ? $" Already deleted: {string.Join(", ", succeeded)}." : "";
        return Result.Failure(new Error(
            ErrorKind.Storage, $"Failed to delete {string.Join("; ", failures)}.{succeededNote}", documentName));
    }

    public Result<IReadOnlyList<ChapterListing>> List(string documentName, bool includeSongs)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return Result<IReadOnlyList<ChapterListing>>.Failure(documentResult.Error!);

        return ListForDocument(documentResult.Value.Id, includeSongs);
    }

    internal Result<IReadOnlyList<ChapterListing>> ListForDocument(long documentId, bool includeSongs)
    {
        Result<IReadOnlyList<ChapterRecord>> chaptersResult = _chapters.GetAllForDocument(documentId);
        if (chaptersResult.IsFailure)
            return Result<IReadOnlyList<ChapterListing>>.Failure(chaptersResult.Error!);

        var chapterListings = new List<ChapterListing>();
        foreach (ChapterRecord chapter in chaptersResult.Value)
        {
            IReadOnlyList<SongListing> songListings = [];
            if (includeSongs)
            {
                Result<IReadOnlyList<SongRecord>> songsResult = _songs.GetAllForChapter(chapter.Id);
                if (songsResult.IsFailure)
                    return Result<IReadOnlyList<ChapterListing>>.Failure(songsResult.Error!);

                songListings = songsResult.Value.Select(s => new SongListing(s.Title, s.Number)).ToList();
            }

            chapterListings.Add(new ChapterListing(chapter.Name, chapter.Title, songListings));
        }

        return Result<IReadOnlyList<ChapterListing>>.Success(chapterListings);
    }

    /// <summary>The next `{document}_chp_{n}` name that doesn't collide with an existing chapter
    /// (FR-1.10.1): derived from the highest `n` currently in use among matching chapter names, not a
    /// count of existing chapters — otherwise deleting a chapter and adding a new one could regenerate
    /// a name that's still taken by a chapter added later than the deleted one.</summary>
    private static string NextDefaultName(string documentName, IReadOnlyList<ChapterRecord> existing)
    {
        string prefix = $"{documentName}_chp_";
        int maxN = 0;
        foreach (ChapterRecord chapter in existing)
        {
            if (chapter.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(chapter.Name.AsSpan(prefix.Length), out int n) && n > maxN)
            {
                maxN = n;
            }
        }
        return $"{prefix}{maxN + 1}";
    }
}
