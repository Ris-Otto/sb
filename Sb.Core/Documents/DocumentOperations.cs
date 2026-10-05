using Sb.Core.LaTeX;
using Sb.Core.Storage;

namespace Sb.Core.Documents;

/// <summary>
/// The CLI-independent implementation of the five `documents` subcommands (FR-1.9), so a front-end
/// other than `Sb.Cli` could reuse the exact same create/list/delete/rename/build behavior rather
/// than reimplementing the atomic-write/cascade/regeneration logic itself (spec's CLI-independent
/// core requirement).
/// </summary>
public sealed class DocumentOperations(Database database)
{
    private readonly DocumentsRepository _documents = new(database);
    private readonly ChaptersRepository _chapters = new(database);
    private readonly SongsRepository _songs = new(database);
    private readonly ChapterOperations _chapterOperations = new(database);

    public Result<DocumentRecord> Create(string name, string title, string? subtitle, string outputFolder)
    {
        Result nameValidity = NameValidator.Validate(name);
        if (nameValidity.IsFailure)
            return Result<DocumentRecord>.Failure(nameValidity.Error!);

        Result<DocumentTemplate> templateResult = DocumentTemplate.Create(title, subtitle);
        if (templateResult.IsFailure)
            return Result<DocumentRecord>.Failure(templateResult.Error!);

        // Checked up front, before any filesystem write: if the name is already taken, Do() below
        // would otherwise find the directory already exists (Directory.CreateDirectory is a silent
        // no-op on an existing path) and write straight into it, and the eventual DB conflict's
        // rollback would then delete a folder this call never actually created.
        if (_documents.GetByFilename(name).IsSuccess)
            return Result<DocumentRecord>.Failure(
                new Error(ErrorKind.Conflict, $"A document named '{name}' already exists.", name));

        // Normalized to absolute so the path stored in the DB is correct however a later command
        // (rename/delete/build) is invoked from a different working directory than this one.
        string documentPath = DocumentLayout.DocumentFolder(Path.GetFullPath(outputFolder), name);
        string mainTexPath = DocumentLayout.MainTexFile(documentPath, name);

        var filesystem = new ReversibleAction(
            Do: () =>
            {
                if (Directory.Exists(documentPath))
                    throw new IOException($"A folder already exists at '{documentPath}'.");
                Directory.CreateDirectory(documentPath);
                try
                {
                    File.WriteAllText(mainTexPath, templateResult.Value.Render());
                }
                catch
                {
                    // Self-heal before rethrowing: AtomicOperation only calls Undo() when Do()
                    // returns successfully but the DB write fails afterward — if Do() itself throws,
                    // Undo() never runs, so a folder created moments ago by this same Do() would
                    // otherwise be left orphaned (FR-1.7.6).
                    Directory.Delete(documentPath, recursive: true);
                    throw;
                }
            },
            Undo: () => Directory.Delete(documentPath, recursive: true));

        Result<long> result = AtomicOperation.Execute(
            filesystem, () => _documents.Insert(name, documentPath, title, subtitle));

        return result.IsSuccess
            ? Result<DocumentRecord>.Success(new DocumentRecord(result.Value, name, documentPath, title, subtitle))
            : Result<DocumentRecord>.Failure(result.Error!);
    }

    public Result<IReadOnlyList<DocumentListing>> List(bool includeChapters, bool includeSongs)
    {
        Result<IReadOnlyList<DocumentRecord>> documentsResult = _documents.GetAll();
        if (documentsResult.IsFailure)
            return Result<IReadOnlyList<DocumentListing>>.Failure(documentsResult.Error!);

        var listings = new List<DocumentListing>();
        foreach (DocumentRecord document in documentsResult.Value)
        {
            IReadOnlyList<ChapterListing> chapterListings = [];
            if (includeChapters)
            {
                Result<IReadOnlyList<ChapterListing>> built = _chapterOperations.ListForDocument(document.Id, includeSongs);
                if (built.IsFailure)
                    return Result<IReadOnlyList<DocumentListing>>.Failure(built.Error!);
                chapterListings = built.Value;
            }

            listings.Add(new DocumentListing(document.Filename, document.Title, chapterListings));
        }

        return Result<IReadOnlyList<DocumentListing>>.Success(listings);
    }

    public Result Delete(string name, bool force)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(name);
        if (documentResult.IsFailure)
            return documentResult;
        DocumentRecord document = documentResult.Value;

        if (!force)
        {
            Result<IReadOnlyList<ChapterRecord>> chaptersResult = _chapters.GetAllForDocument(document.Id);
            if (chaptersResult.IsFailure)
                return chaptersResult;

            if (chaptersResult.Value.Count > 0)
                return Result.Failure(new Error(
                    ErrorKind.Conflict,
                    $"Document '{name}' contains {chaptersResult.Value.Count} chapter(s). " +
                    "Re-run with --force to delete it and everything inside it.",
                    name));
        }

        return AtomicOperation.ExecuteDelete(
            () => _documents.Delete(document.Id),
            filesystemCleanup: () => Directory.Delete(document.Path, recursive: true));
    }

    public Result Rename(string name, string newName)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(name);
        if (documentResult.IsFailure)
            return documentResult;
        DocumentRecord document = documentResult.Value;

        Result nameValidity = NameValidator.Validate(newName);
        if (nameValidity.IsFailure)
            return nameValidity;

        // Same reasoning as Create: check uniqueness before touching the filesystem, so a conflict
        // can't make Directory.Move fail (and the pre-existing destination folder stay untouched)
        // only after this call's own folder has already been moved partway.
        if (_documents.GetByFilename(newName).IsSuccess)
            return Result.Failure(
                new Error(ErrorKind.Conflict, $"A document named '{newName}' already exists.", newName));

        string outputFolder = Path.GetDirectoryName(document.Path) ?? ".";
        string newPath = DocumentLayout.DocumentFolder(outputFolder, newName);
        string oldMainTex = DocumentLayout.MainTexFile(document.Path, name);
        string newMainTex = DocumentLayout.MainTexFile(newPath, newName);
        string movedMainTex = Path.Combine(newPath, Path.GetFileName(oldMainTex));

        var filesystem = new ReversibleAction(
            Do: () =>
            {
                Directory.Move(document.Path, newPath);
                try
                {
                    if (File.Exists(movedMainTex))
                        File.Move(movedMainTex, newMainTex);
                }
                catch
                {
                    // Same reasoning as Create's Do(): if the second step throws, Undo() never runs,
                    // so the folder move from the first step must be reversed here or the DB would end
                    // up pointing at the old path after it's already been physically moved away.
                    Directory.Move(newPath, document.Path);
                    throw;
                }
            },
            Undo: () =>
            {
                if (File.Exists(newMainTex))
                    File.Move(newMainTex, movedMainTex);
                Directory.Move(newPath, document.Path);
            });

        return AtomicOperation.Execute(filesystem, () => _documents.Rename(document.Id, newName, newPath));
    }

    public Result Build(string name, SongOrdering ordering, LeafletFormat format)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(name);
        if (documentResult.IsFailure)
            return documentResult;
        DocumentRecord document = documentResult.Value;

        Result<IReadOnlyList<ChapterRecord>> chaptersResult = _chapters.GetAllForDocument(document.Id);
        if (chaptersResult.IsFailure)
            return chaptersResult;

        DocumentTemplate template = DocumentTemplate.Create(document.Title, document.Subtitle).Value;

        foreach (ChapterRecord chapter in chaptersResult.Value)
        {
            Result<IReadOnlyList<Song>> songsResult = LoadChapterSongs(document.Path, chapter);
            if (songsResult.IsFailure)
                return songsResult;

            string chapterTex = format == LeafletFormat.SingleColumn
                ? ChapterEmitter.RenderSingleColumn(chapter.Title, OrderAsListed(songsResult.Value))
                : ChapterEmitter.RenderMultiColumn(chapter.Title, ComputePages(songsResult.Value, ordering), LayoutDefaults.ColumnWidth);

            string chapterFolder = DocumentLayout.ChapterFolder(document.Path, chapter.Name);
            Directory.CreateDirectory(chapterFolder);
            File.WriteAllText(DocumentLayout.ChapterTexFile(document.Path, chapter.Name), chapterTex);

            template = template.WithChapterAdded(DocumentLayout.ChapterTexInputPath(chapter.Name));
        }

        File.WriteAllText(DocumentLayout.MainTexFile(document.Path, document.Filename), template.Render());
        return Result.Success();
    }

    private Result<IReadOnlyList<Song>> LoadChapterSongs(string documentPath, ChapterRecord chapter)
    {
        Result<IReadOnlyList<SongRecord>> songRecordsResult = _songs.GetAllForChapter(chapter.Id);
        if (songRecordsResult.IsFailure)
            return Result<IReadOnlyList<Song>>.Failure(songRecordsResult.Error!);

        var songs = new List<Song>();
        foreach (SongRecord songRecord in songRecordsResult.Value)
        {
            Result<Song> songResult = SongFileStore.Load(documentPath, chapter.Name, songRecord);
            if (songResult.IsFailure)
                return Result<IReadOnlyList<Song>>.Failure(songResult.Error!);

            songs.Add(songResult.Value);
        }

        return Result<IReadOnlyList<Song>>.Success(songs);
    }

    private static IReadOnlyList<Song> OrderAsListed(IReadOnlyList<Song> songs) =>
        songs.OrderBy(s => s.Number).ToList();

    private static IReadOnlyList<Page> ComputePages(IReadOnlyList<Song> songs, SongOrdering ordering) =>
        ordering == SongOrdering.SpaceEfficient
            ? SpaceEfficientPaginator.Paginate(songs, LayoutDefaults.LinesPerColumn, LayoutDefaults.ColumnWidth)
            : Paginator.PaginateAsListed(OrderAsListed(songs), LayoutDefaults.LinesPerColumn, LayoutDefaults.ColumnWidth);
}
