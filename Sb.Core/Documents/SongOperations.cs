using Sb.Core.Storage;

namespace Sb.Core.Documents;

/// <summary>
/// The CLI-independent implementation of the four `songs` subcommands (FR-1.11). Unlike documents and
/// chapters, `delete`/`modify` locate a song by (document, title) alone — per FR-1.11.2/FR-1.11.4 —
/// even though title uniqueness is only enforced within a chapter (FR-1.8.6), so the same title could
/// in principle exist in two chapters of the same document. <see cref="FindByTitle"/> treats that as
/// an ambiguous reference rather than silently picking one.
/// </summary>
public sealed class SongOperations(Database database)
{
    private readonly DocumentsRepository _documents = new(database);
    private readonly ChaptersRepository _chapters = new(database);
    private readonly SongsRepository _songs = new(database);

    /// <summary>A song together with where it lives, so a document-wide renumbering can rewrite each
    /// song's own file and row.</summary>
    private sealed record Placed(ChapterRecord Chapter, SongRecord Record, Song Song);

    public Result<SongRecord> Add(
        string documentName, string chapterName, string title, string text, string? writer, string? melody,
        int? number)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return Result<SongRecord>.Failure(documentResult.Error!);
        DocumentRecord document = documentResult.Value;

        Result<ChapterRecord> chapterResult = _chapters.GetByName(document.Id, chapterName);
        if (chapterResult.IsFailure)
            return Result<SongRecord>.Failure(chapterResult.Error!);
        ChapterRecord chapter = chapterResult.Value;

        // Songs are written into the chapter's folder; if the database's folders are not where it
        // expects them, say so rather than failing obscurely or recreating them (FR-1.7.5).
        Result folderCheck = MismatchDetector.CheckDocumentFolder(document);
        if (folderCheck.IsSuccess)
            folderCheck = MismatchDetector.CheckChapterFolder(document, chapter);
        if (folderCheck.IsFailure)
            return Result<SongRecord>.Failure(folderCheck.Error!);

        // Checked up front, before any filesystem write — same reasoning as Create/Add elsewhere.
        if (_songs.GetByTitle(chapter.Id, title).IsSuccess)
            return Result<SongRecord>.Failure(new Error(
                ErrorKind.Conflict, $"A song titled '{title}' already exists in chapter '{chapterName}'.", title));

        // The database only compares titles case-sensitively, but the file system may not (Windows), and a
        // stray file may exist: refuse rather than silently overwrite another song's file (FR-1.8.9).
        string songFilePath = DocumentLayout.SongFile(document.Path, chapter.Name, title);
        if (File.Exists(songFilePath))
            return Result<SongRecord>.Failure(OverwriteConflict(songFilePath, title));

        // Song numbers run through the whole document (FR-1.4.1), across all of its chapters.
        Result<List<Placed>> existingResult = LoadDocumentSongs(document);
        if (existingResult.IsFailure)
            return Result<SongRecord>.Failure(existingResult.Error!);
        List<Placed> existing = existingResult.Value;

        int position = number ?? existing.Count + 1;
        if (position < 1 || position > existing.Count + 1)
            return Result<SongRecord>.Failure(new Error(
                ErrorKind.Validation, $"Number must be between 1 and {existing.Count + 1}.", title));

        Result<Song> newSongResult = Song.Create(title, ResolveText(text), position, BlankToNull(writer), BlankToNull(melody));
        if (newSongResult.IsFailure)
            return Result<SongRecord>.Failure(newSongResult.Error!);

        // Renumbering is computed up front (pure — no side effects). The new song and every shifted song
        // are then written as one unit (FR-1.7.6): all the files first, each with an undo, then all the DB
        // rows in a single transaction; if anything fails the files are put back and nothing is committed,
        // so the document's numbering is never left with duplicates or gaps.
        IReadOnlyList<Song> renumbered =
            SongNumbering.InsertAt(existing.Select(p => p.Song).ToList(), position, newSongResult.Value);
        Song finalSong = renumbered[position - 1];

        // existing[i] sits at renumbered[i] before the insertion point and renumbered[i + 1] after it.
        List<(Placed Item, int NewNumber)> shifts =
            ChangedOnly(existing.Select((p, i) => (p, renumbered[i < position - 1 ? i : i + 1].Number)));

        List<FileChange> files =
        [
            new(() => SongFileStore.Save(document.Path, chapter.Name, finalSong),
                () => SongFileStore.Delete(document.Path, chapter.Name, finalSong.Title)),
            .. ShiftFileChanges(document.Path, shifts)
        ];

        Result<long> insertResult = AtomicOperation.Execute(
            ToReversibleAction(files),
            () => database.InTransaction(() =>
            {
                Result<long> insert = _songs.Insert(chapter.Id, finalSong);
                if (insert.IsFailure)
                    return insert;
                Result shifted = UpdateShiftedRows(shifts);
                return shifted.IsFailure ? Result<long>.Failure(shifted.Error!) : insert;
            }));
        if (insertResult.IsFailure)
            return Result<SongRecord>.Failure(insertResult.Error!);

        return Result<SongRecord>.Success(new SongRecord(insertResult.Value, chapter.Id, finalSong.Title, finalSong.Number));
    }

    public Result Delete(string documentName, string songTitle)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return documentResult;
        DocumentRecord document = documentResult.Value;

        Result<(ChapterRecord Chapter, SongRecord Song)> foundResult = FindByTitle(document.Id, songTitle);
        if (foundResult.IsFailure)
            return foundResult;
        (ChapterRecord chapter, SongRecord songRecord) = foundResult.Value;

        Result<List<Placed>> existingResult = LoadDocumentSongs(document);
        if (existingResult.IsFailure)
            return existingResult;
        List<Placed> existing = existingResult.Value;

        int index = existing.FindIndex(p => p.Record.Id == songRecord.Id);
        IReadOnlyList<Song> renumbered = SongNumbering.RemoveAt(existing.Select(p => p.Song).ToList(), index + 1);

        // Same all-or-nothing shape as Add: the shifted songs' files are rewritten (with undo), then the
        // song's row is deleted and the later songs' rows renumbered in one transaction. Only after that
        // has committed is the deleted song's own file removed — a file delete has no cheap undo, so, as
        // for every delete (FR-1.7.6), that last step is best-effort and reported if it fails.
        // existing[i] (i != index) sits at renumbered[i] before the removed song and renumbered[i - 1] after.
        List<(Placed Item, int NewNumber)> shifts = ChangedOnly(
            existing.Select((p, i) => (p, i < index ? renumbered[i].Number : i > index ? renumbered[i - 1].Number : 0))
                .Where(shift => shift.Item2 > 0));

        Result committed = AtomicOperation.Execute(
            ToReversibleAction(ShiftFileChanges(document.Path, shifts).ToList()),
            () => database.InTransaction(() =>
            {
                Result deleted = _songs.Delete(songRecord.Id);
                return deleted.IsFailure ? deleted : UpdateShiftedRows(shifts);
            }));
        if (committed.IsFailure)
            return committed;

        try
        {
            SongFileStore.Delete(document.Path, chapter.Name, songTitle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(new Error(
                ErrorKind.Storage,
                $"Removed from the database, but deleting the file failed: {ex.Message} — manual cleanup may be required."));
        }

        return Result.Success();
    }

    public Result Modify(
        string documentName, string songTitle, string? newTitle, string? writer, string? melody,
        string? newChapterName, int? newNumber)
    {
        if (newTitle is null && writer is null && melody is null && newChapterName is null && newNumber is null)
            return Result.Failure(new Error(
                ErrorKind.Validation, "At least one of title/writer/melody/chapter/number must be given.", songTitle));

        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return documentResult;
        DocumentRecord document = documentResult.Value;

        Result<(ChapterRecord Chapter, SongRecord Song)> foundResult = FindByTitle(document.Id, songTitle);
        if (foundResult.IsFailure)
            return foundResult;
        (ChapterRecord sourceChapter, SongRecord songRecord) = foundResult.Value;

        Result<List<Placed>> existingResult = LoadDocumentSongs(document);
        if (existingResult.IsFailure)
            return existingResult;
        List<Placed> existing = existingResult.Value;
        int selfIndex = existing.FindIndex(p => p.Record.Id == songRecord.Id);
        Song currentSong = existing[selfIndex].Song;
        List<Placed> others = existing.Where(p => p.Record.Id != songRecord.Id).ToList();

        ChapterRecord destinationChapter = sourceChapter;
        if (newChapterName is not null)
        {
            Result<ChapterRecord> destinationResult = _chapters.GetByName(document.Id, newChapterName);
            if (destinationResult.IsFailure)
                return destinationResult;
            destinationChapter = destinationResult.Value;
        }
        bool movingChapter = destinationChapter.Id != sourceChapter.Id;

        Result folderCheck = MismatchDetector.CheckDocumentFolder(document);
        if (folderCheck.IsSuccess)
            folderCheck = MismatchDetector.CheckChapterFolder(document, destinationChapter);
        if (folderCheck.IsFailure)
            return folderCheck;

        string finalTitle = newTitle ?? currentSong.Title;
        bool titleChanged = finalTitle != currentSong.Title;

        if (others.Any(p => p.Chapter.Id == destinationChapter.Id && p.Record.Title == finalTitle))
            return Result.Failure(new Error(
                ErrorKind.Conflict,
                $"A song titled '{finalTitle}' already exists in chapter '{destinationChapter.Name}'.", finalTitle));

        // The number is the song's position in the whole document, so moving it to another chapter
        // keeps its number unless a new one is given.
        int targetPosition = newNumber ?? selfIndex + 1;
        if (targetPosition < 1 || targetPosition > others.Count + 1)
            return Result.Failure(new Error(
                ErrorKind.Validation, $"Number must be between 1 and {others.Count + 1}.", finalTitle));

        Result<Song> movedSongResult = Song.Create(
            finalTitle, currentSong.Text, targetPosition,
            writer is null ? currentSong.Writer : BlankToNull(writer),
            melody is null ? currentSong.Melody : BlankToNull(melody));
        if (movedSongResult.IsFailure)
            return Result.Failure(movedSongResult.Error!);

        // As with Add/Delete: the renumbering is computed up front (pure), but only persisted to the
        // *other* songs after the song actually being modified has itself been safely updated below —
        // otherwise a failure there would leave other songs shifted around a move that never happened.
        IReadOnlyList<Song> renumbered =
            SongNumbering.InsertAt(others.Select(p => p.Song).ToList(), targetPosition, movedSongResult.Value);
        Song finalSong = renumbered[targetPosition - 1];

        // The old file is only stale if the song actually moved folder or its filename-driving title
        // changed — otherwise Do() below overwrites the same file in place. The new file is written
        // before the old one is removed, so a failure partway never leaves neither copy on disk.
        bool fileMoves = movingChapter || titleChanged;
        // Same check as Add, for the file this modification would create. When only the case of the title
        // changes on a case-insensitive file system the "other" file is the song's own: that is fine, but
        // the old file must then not be deleted afterwards (it is the file just written).
        string oldPath = DocumentLayout.SongFile(document.Path, sourceChapter.Name, currentSong.Title);
        string newPath = DocumentLayout.SongFile(document.Path, destinationChapter.Name, finalSong.Title);
        StringComparison pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool sameFile = string.Equals(oldPath, newPath, pathComparison);
        if (fileMoves && !sameFile && File.Exists(newPath))
            return Result.Failure(OverwriteConflict(newPath, finalTitle));
        bool oldFileGoesStale = fileMoves && !sameFile;

        // others[i] sits at renumbered[i] before the insertion point and renumbered[i + 1] after it.
        List<(Placed Item, int NewNumber)> shifts =
            ChangedOnly(others.Select((p, i) => (p, renumbered[i < targetPosition - 1 ? i : i + 1].Number)));

        // The song's own file change and every shifted song's file are one unit with the DB update (see
        // Add): files first, each undoable, then all rows in one transaction.
        List<FileChange> files =
        [
            new(() => SongFileStore.Save(document.Path, destinationChapter.Name, finalSong),
                () =>
                {
                    // Put the original content back at its original path — when !oldFileGoesStale that IS
                    // the destination path (an in-place overwrite). Otherwise the new file is at a different
                    // path and has to be removed; the old one is restored by the next change's undo.
                    if (oldFileGoesStale)
                        SongFileStore.Delete(document.Path, destinationChapter.Name, finalSong.Title);
                    else
                        SongFileStore.Save(document.Path, sourceChapter.Name, currentSong);
                }),
            .. oldFileGoesStale
                ? [new FileChange(
                    () => SongFileStore.Delete(document.Path, sourceChapter.Name, currentSong.Title),
                    () => SongFileStore.Save(document.Path, sourceChapter.Name, currentSong))]
                : Array.Empty<FileChange>(),
            .. ShiftFileChanges(document.Path, shifts)
        ];

        return AtomicOperation.Execute(
            ToReversibleAction(files),
            () => database.InTransaction(() =>
            {
                Result updated = _songs.UpdateChapterTitleAndNumber(
                    songRecord.Id, destinationChapter.Id, finalSong.Title, finalSong.Number);
                return updated.IsFailure ? updated : UpdateShiftedRows(shifts);
            }));
    }

    public Result<IReadOnlyList<SongEntry>> List(string documentName, string? chapterName)
    {
        Result<DocumentRecord> documentResult = _documents.GetByFilename(documentName);
        if (documentResult.IsFailure)
            return Result<IReadOnlyList<SongEntry>>.Failure(documentResult.Error!);
        DocumentRecord document = documentResult.Value;

        IReadOnlyList<ChapterRecord> chaptersToList;
        if (chapterName is not null)
        {
            Result<ChapterRecord> chapterResult = _chapters.GetByName(document.Id, chapterName);
            if (chapterResult.IsFailure)
                return Result<IReadOnlyList<SongEntry>>.Failure(chapterResult.Error!);
            chaptersToList = [chapterResult.Value];
        }
        else
        {
            Result<IReadOnlyList<ChapterRecord>> chaptersResult = _chapters.GetAllForDocument(document.Id);
            if (chaptersResult.IsFailure)
                return Result<IReadOnlyList<SongEntry>>.Failure(chaptersResult.Error!);
            chaptersToList = chaptersResult.Value;
        }

        var entries = new List<SongEntry>();
        foreach (ChapterRecord chapter in chaptersToList)
        {
            Result<IReadOnlyList<SongRecord>> songsResult = _songs.GetAllForChapter(chapter.Id);
            if (songsResult.IsFailure)
                return Result<IReadOnlyList<SongEntry>>.Failure(songsResult.Error!);

            // Files the database expects but that aren't on disk are flagged, not hidden (FR-1.7.5).
            Dictionary<string, string> missing = MismatchDetector
                .FindSongMismatches(document, chapter, songsResult.Value)
                .ToDictionary(m => m.Name, m => m.ExpectedPath);
            entries.AddRange(songsResult.Value.Select(
                s => new SongEntry(chapter.Name, s.Title, s.Number) { MissingAt = missing.GetValueOrDefault(s.Title) }));
        }

        return Result<IReadOnlyList<SongEntry>>.Success(entries);
    }

    /// <summary>An empty or whitespace-only writer/melody means "none": it is stored as absent, so passing
    /// an empty value to `songs modify` clears the field (FR-1.11.4) and an empty one on `songs add` is the
    /// same as leaving it out.</summary>
    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>A literal string's lines if `text` isn't an existing file path, otherwise that file's
    /// lines (FR-1.11.1) — split the same way regardless of source, so the two inputs that resolve to
    /// the same content produce an identical stored <see cref="Song.Text"/>.</summary>
    private static IReadOnlyList<string> ResolveText(string text) =>
        File.Exists(text) ? File.ReadAllLines(text) : text.Replace("\r\n", "\n").Split('\n');

    private static Error OverwriteConflict(string path, string title) => new(
        ErrorKind.Conflict,
        $"Saving song '{title}' would overwrite the existing file '{path}'. This usually means another song's " +
        "title differs only in case (file names are not case-sensitive on this file system), or a stray file is " +
        "in the way. Nothing was changed.",
        title);

    /// <summary>Every song of a document across all its chapters, in number order (FR-1.4.1).</summary>
    private Result<List<Placed>> LoadDocumentSongs(DocumentRecord document)
    {
        Result<IReadOnlyList<ChapterRecord>> chaptersResult = _chapters.GetAllForDocument(document.Id);
        if (chaptersResult.IsFailure)
            return Result<List<Placed>>.Failure(chaptersResult.Error!);

        var placed = new List<Placed>();
        foreach (ChapterRecord chapter in chaptersResult.Value)
        {
            Result<IReadOnlyList<SongRecord>> recordsResult = _songs.GetAllForChapter(chapter.Id);
            if (recordsResult.IsFailure)
                return Result<List<Placed>>.Failure(recordsResult.Error!);

            foreach (SongRecord record in recordsResult.Value)
            {
                Result<Song> loaded = SongFileStore.Load(document.Path, chapter.Name, record);
                if (loaded.IsFailure)
                    return Result<List<Placed>>.Failure(loaded.Error!);
                placed.Add(new Placed(chapter, record, loaded.Value));
            }
        }

        // Stable, so songs with a duplicate number (a document numbered per chapter before this was
        // document-wide) keep chapter order, and the renumbering that follows repairs them.
        return Result<List<Placed>>.Success(placed.OrderBy(p => p.Song.Number).ToList());
    }

    /// <summary>One file-system step of a song operation together with how to undo it.</summary>
    private sealed record FileChange(Action Apply, Action Undo);

    /// <summary>Runs the changes in order; if one throws, the ones already applied are undone (in reverse)
    /// and the exception propagates, so a failure part-way leaves the files as they were. The returned
    /// action's undo reverts all of them, for when the database step that follows fails.</summary>
    private static ReversibleAction ToReversibleAction(IReadOnlyList<FileChange> changes) => new(
        Do: () =>
        {
            int applied = 0;
            try
            {
                foreach (FileChange change in changes)
                {
                    change.Apply();
                    applied++;
                }
            }
            catch
            {
                UndoLast(changes, applied);
                throw;
            }
        },
        Undo: () => UndoLast(changes, changes.Count));

    private static void UndoLast(IReadOnlyList<FileChange> changes, int count)
    {
        // Every undo is attempted even if an earlier one fails, so as much as possible is restored.
        List<Exception>? failures = null;
        for (int i = count - 1; i >= 0; i--)
        {
            try
            {
                changes[i].Undo();
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
            throw new AggregateException(failures);
    }

    /// <summary>Only the songs whose number actually changes need rewriting.</summary>
    private static List<(Placed Item, int NewNumber)> ChangedOnly(IEnumerable<(Placed Item, int NewNumber)> shifts) =>
        shifts.Where(s => s.Item.Song.Number != s.NewNumber || s.Item.Record.Number != s.NewNumber).ToList();

    /// <summary>Rewrites each shifted song's `.yml` with its new number; undoing restores the old content.</summary>
    private static IEnumerable<FileChange> ShiftFileChanges(
        string documentPath, IEnumerable<(Placed Item, int NewNumber)> shifts) =>
        shifts.Select(s => new FileChange(
            () => SongFileStore.Save(documentPath, s.Item.Chapter.Name, s.Item.Song.WithNumber(s.NewNumber)),
            () => SongFileStore.Save(documentPath, s.Item.Chapter.Name, s.Item.Song)));

    /// <summary>Updates the DB row of each shifted song; meant to run inside one transaction.</summary>
    private Result UpdateShiftedRows(IEnumerable<(Placed Item, int NewNumber)> shifts)
    {
        foreach ((Placed item, int newNumber) in shifts)
        {
            Result updated = _songs.UpdateChapterTitleAndNumber(
                item.Record.Id, item.Chapter.Id, item.Song.Title, newNumber);
            if (updated.IsFailure)
                return updated;
        }

        return Result.Success();
    }

    /// <summary>Finds a song by title across every chapter of a document (FR-1.11.2, FR-1.11.4): a
    /// Conflict, not an arbitrary pick, when the title exists in more than one chapter (see class
    /// remarks).</summary>
    private Result<(ChapterRecord Chapter, SongRecord Song)> FindByTitle(long documentId, string title)
    {
        Result<IReadOnlyList<ChapterRecord>> chaptersResult = _chapters.GetAllForDocument(documentId);
        if (chaptersResult.IsFailure)
            return Result<(ChapterRecord, SongRecord)>.Failure(chaptersResult.Error!);

        var matches = new List<(ChapterRecord Chapter, SongRecord Song)>();
        foreach (ChapterRecord chapter in chaptersResult.Value)
        {
            Result<SongRecord> songResult = _songs.GetByTitle(chapter.Id, title);
            if (songResult.IsSuccess)
                matches.Add((chapter, songResult.Value));
        }

        return matches.Count switch
        {
            0 => Result<(ChapterRecord, SongRecord)>.Failure(
                new Error(ErrorKind.NotFound, $"No song titled '{title}' was found.", title)),
            1 => Result<(ChapterRecord, SongRecord)>.Success(matches[0]),
            _ => Result<(ChapterRecord, SongRecord)>.Failure(new Error(
                ErrorKind.Conflict,
                $"A song titled '{title}' exists in more than one chapter " +
                $"({string.Join(", ", matches.Select(m => m.Chapter.Name))}) and can't be uniquely identified by " +
                "title alone.",
                title))
        };
    }
}
