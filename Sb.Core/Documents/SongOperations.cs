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

        // Checked up front, before any filesystem write — same reasoning as Create/Add elsewhere.
        if (_songs.GetByTitle(chapter.Id, title).IsSuccess)
            return Result<SongRecord>.Failure(new Error(
                ErrorKind.Conflict, $"A song titled '{title}' already exists in chapter '{chapterName}'.", title));

        Result<IReadOnlyList<SongRecord>> existingRecordsResult = _songs.GetAllForChapter(chapter.Id);
        if (existingRecordsResult.IsFailure)
            return Result<SongRecord>.Failure(existingRecordsResult.Error!);

        Result<IReadOnlyList<Song>> existingSongsResult =
            LoadAll(document.Path, chapter.Name, existingRecordsResult.Value);
        if (existingSongsResult.IsFailure)
            return Result<SongRecord>.Failure(existingSongsResult.Error!);
        IReadOnlyList<Song> existingSongs = existingSongsResult.Value;

        int position = number ?? existingSongs.Count + 1;
        if (position < 1 || position > existingSongs.Count + 1)
            return Result<SongRecord>.Failure(new Error(
                ErrorKind.Validation, $"Number must be between 1 and {existingSongs.Count + 1}.", title));

        Result<Song> newSongResult = Song.Create(title, ResolveText(text), position, writer, melody);
        if (newSongResult.IsFailure)
            return Result<SongRecord>.Failure(newSongResult.Error!);

        // Renumbering is computed up front (pure — no side effects), but only *persisted* to the other
        // songs' files/DB rows after the new song itself is safely inserted below. Doing it the other
        // way around — shift first, insert second — would leave the other songs renumbered to make
        // room for a song that, if the insert then failed, was never actually added: a gap in the
        // chapter's numbering with nothing to fill it.
        IReadOnlyList<Song> renumbered = SongNumbering.InsertAt(existingSongs, position, newSongResult.Value);
        Song finalSong = renumbered.First(s => s.Title == title);

        var filesystem = new ReversibleAction(
            Do: () => SongFileStore.Save(document.Path, chapter.Name, finalSong),
            Undo: () => SongFileStore.Delete(document.Path, chapter.Name, finalSong.Title));

        Result<long> insertResult = AtomicOperation.Execute(filesystem, () => _songs.Insert(chapter.Id, finalSong));
        if (insertResult.IsFailure)
            return Result<SongRecord>.Failure(insertResult.Error!);

        Result shiftResult = PersistShiftedNumbers(document.Path, chapter, existingRecordsResult.Value, existingSongs, renumbered, title);
        if (shiftResult.IsFailure)
            return Result<SongRecord>.Failure(shiftResult.Error!);

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

        Result<IReadOnlyList<SongRecord>> existingRecordsResult = _songs.GetAllForChapter(chapter.Id);
        if (existingRecordsResult.IsFailure)
            return existingRecordsResult;

        Result<IReadOnlyList<Song>> existingSongsResult =
            LoadAll(document.Path, chapter.Name, existingRecordsResult.Value);
        if (existingSongsResult.IsFailure)
            return Result.Failure(existingSongsResult.Error!);
        IReadOnlyList<Song> existingSongs = existingSongsResult.Value;

        int position = existingSongs.First(s => s.Title == songTitle).Number;
        IReadOnlyList<Song> renumbered = SongNumbering.RemoveAt(existingSongs, position);

        // Same reasoning as Add: the song itself is deleted first (DB row, then its file — the
        // established delete-shaped ordering), and only once that succeeds are the later songs shifted
        // down to close the gap. Shifting first would risk a duplicate number if the delete then failed.
        Result deleteResult = AtomicOperation.ExecuteDelete(
            () => _songs.Delete(songRecord.Id),
            filesystemCleanup: () => SongFileStore.Delete(document.Path, chapter.Name, songTitle));
        if (deleteResult.IsFailure)
            return deleteResult;

        return PersistShiftedNumbers(document.Path, chapter, existingRecordsResult.Value, existingSongs, renumbered, exceptTitle: null);
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

        Result<Song> currentSongResult = SongFileStore.Load(document.Path, sourceChapter.Name, songRecord);
        if (currentSongResult.IsFailure)
            return currentSongResult;
        Song currentSong = currentSongResult.Value;

        ChapterRecord destinationChapter = sourceChapter;
        if (newChapterName is not null)
        {
            Result<ChapterRecord> destinationResult = _chapters.GetByName(document.Id, newChapterName);
            if (destinationResult.IsFailure)
                return destinationResult;
            destinationChapter = destinationResult.Value;
        }
        bool movingChapter = destinationChapter.Id != sourceChapter.Id;

        string finalTitle = newTitle ?? currentSong.Title;
        bool titleChanged = finalTitle != currentSong.Title;

        Result<IReadOnlyList<SongRecord>> destRecordsResult = _songs.GetAllForChapter(destinationChapter.Id);
        if (destRecordsResult.IsFailure)
            return destRecordsResult;
        IReadOnlyList<SongRecord> destRecordsExcludingSelf = movingChapter
            ? destRecordsResult.Value
            : destRecordsResult.Value.Where(r => r.Id != songRecord.Id).ToList();

        if (destRecordsExcludingSelf.Any(r => r.Title == finalTitle))
            return Result.Failure(new Error(
                ErrorKind.Conflict,
                $"A song titled '{finalTitle}' already exists in chapter '{destinationChapter.Name}'.", finalTitle));

        Result<IReadOnlyList<Song>> destExistingResult =
            LoadAll(document.Path, destinationChapter.Name, destRecordsExcludingSelf);
        if (destExistingResult.IsFailure)
            return Result.Failure(destExistingResult.Error!);
        IReadOnlyList<Song> destExistingSongs = destExistingResult.Value;

        int targetPosition = newNumber ?? (movingChapter ? destExistingSongs.Count + 1 : currentSong.Number);
        if (targetPosition < 1 || targetPosition > destExistingSongs.Count + 1)
            return Result.Failure(new Error(
                ErrorKind.Validation, $"Number must be between 1 and {destExistingSongs.Count + 1}.", finalTitle));

        Result<Song> movedSongResult = Song.Create(
            finalTitle, currentSong.Text, targetPosition, writer ?? currentSong.Writer, melody ?? currentSong.Melody);
        if (movedSongResult.IsFailure)
            return Result.Failure(movedSongResult.Error!);

        // As with Add/Delete: both renumberings are computed up front (pure), but only persisted to the
        // *other* songs after the song actually being modified has itself been safely updated below —
        // otherwise a failure there would leave other songs shifted to make room for/close a gap around
        // a move that never happened.
        IReadOnlyList<Song> renumberedDest = SongNumbering.InsertAt(destExistingSongs, targetPosition, movedSongResult.Value);
        Song finalSong = renumberedDest.First(s => s.Title == finalTitle);

        IReadOnlyList<SongRecord>? sourceRecords = null;
        IReadOnlyList<Song>? sourceSongs = null;
        IReadOnlyList<Song>? renumberedSource = null;
        if (movingChapter)
        {
            Result<IReadOnlyList<SongRecord>> sourceRecordsResult = _songs.GetAllForChapter(sourceChapter.Id);
            if (sourceRecordsResult.IsFailure)
                return sourceRecordsResult;

            Result<IReadOnlyList<Song>> sourceExistingResult =
                LoadAll(document.Path, sourceChapter.Name, sourceRecordsResult.Value);
            if (sourceExistingResult.IsFailure)
                return Result.Failure(sourceExistingResult.Error!);

            sourceRecords = sourceRecordsResult.Value;
            sourceSongs = sourceExistingResult.Value;
            renumberedSource = SongNumbering.RemoveAt(sourceSongs, currentSong.Number);
        }

        // The old file is only stale if the song actually moved folder or its filename-driving title
        // changed — otherwise Do() below overwrites the same file in place. The new file is written
        // before the old one is removed, so a failure partway never leaves neither copy on disk.
        bool fileMoves = movingChapter || titleChanged;
        var filesystem = new ReversibleAction(
            Do: () =>
            {
                SongFileStore.Save(document.Path, destinationChapter.Name, finalSong);
                if (fileMoves)
                    SongFileStore.Delete(document.Path, sourceChapter.Name, currentSong.Title);
            },
            Undo: () =>
            {
                // Always restore the original content at its original path — when !fileMoves that IS
                // the destination path, so this alone fixes the in-place overwrite Do() made. The
                // separate delete only applies when the new file is at a genuinely different path;
                // skipping it when !fileMoves is essential; deleting the path Save just restored
                // would undo the restoration itself and leave the DB row's file missing entirely.
                if (fileMoves)
                    SongFileStore.Delete(document.Path, destinationChapter.Name, finalSong.Title);
                SongFileStore.Save(document.Path, sourceChapter.Name, currentSong);
            });

        Result updateResult = AtomicOperation.Execute(
            filesystem,
            () => _songs.UpdateChapterTitleAndNumber(songRecord.Id, destinationChapter.Id, finalSong.Title, finalSong.Number));
        if (updateResult.IsFailure)
            return updateResult;

        Result destShiftResult = PersistShiftedNumbers(
            document.Path, destinationChapter, destRecordsExcludingSelf, destExistingSongs, renumberedDest, finalTitle);
        if (destShiftResult.IsFailure)
            return destShiftResult;

        if (movingChapter)
        {
            Result sourceShiftResult =
                PersistShiftedNumbers(document.Path, sourceChapter, sourceRecords!, sourceSongs!, renumberedSource!, exceptTitle: null);
            if (sourceShiftResult.IsFailure)
                return sourceShiftResult;
        }

        return Result.Success();
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

            entries.AddRange(songsResult.Value.Select(s => new SongEntry(chapter.Name, s.Title, s.Number)));
        }

        return Result<IReadOnlyList<SongEntry>>.Success(entries);
    }

    /// <summary>A literal string's lines if `text` isn't an existing file path, otherwise that file's
    /// lines (FR-1.11.1) — split the same way regardless of source, so the two inputs that resolve to
    /// the same content produce an identical stored <see cref="Song.Text"/>.</summary>
    private static IReadOnlyList<string> ResolveText(string text) =>
        File.Exists(text) ? File.ReadAllLines(text) : text.Replace("\r\n", "\n").Split('\n');

    private static Result<IReadOnlyList<Song>> LoadAll(
        string documentPath, string chapterName, IReadOnlyList<SongRecord> records)
    {
        var songs = new List<Song>();
        foreach (SongRecord record in records)
        {
            Result<Song> loaded = SongFileStore.Load(documentPath, chapterName, record);
            if (loaded.IsFailure)
                return Result<IReadOnlyList<Song>>.Failure(loaded.Error!);
            songs.Add(loaded.Value);
        }
        return Result<IReadOnlyList<Song>>.Success(songs);
    }

    /// <summary>Rewrites the `.yml` file and DB row of every song whose number changed between
    /// <paramref name="originalSongs"/> and <paramref name="renumbered"/> — the song being
    /// added/removed/moved itself is excluded via <paramref name="exceptTitle"/> (for add/modify,
    /// where it isn't present in <paramref name="originalSongs"/> at all) or is simply absent from
    /// <paramref name="renumbered"/> (for delete). Every song is attempted even if an earlier one's
    /// update fails, and any failures are reported together — by the time this runs, the add/delete/
    /// move itself has already succeeded, so this is reported as a (clearly-labeled) partial failure
    /// rather than silently dropped.</summary>
    private Result PersistShiftedNumbers(
        string documentPath, ChapterRecord chapter, IReadOnlyList<SongRecord> originalRecords,
        IReadOnlyList<Song> originalSongs, IReadOnlyList<Song> renumbered, string? exceptTitle)
    {
        var failures = new List<string>();
        foreach (Song song in renumbered)
        {
            if (song.Title == exceptTitle)
                continue;

            Song original = originalSongs.First(s => s.Title == song.Title);
            if (song.Number == original.Number)
                continue;

            SongFileStore.Save(documentPath, chapter.Name, song);
            SongRecord originalRecord = originalRecords.First(r => r.Title == song.Title);
            Result updateResult = _songs.UpdateChapterTitleAndNumber(originalRecord.Id, chapter.Id, song.Title, song.Number);
            if (updateResult.IsFailure)
                failures.Add($"'{song.Title}': {updateResult.Error!.Message}");
        }

        return failures.Count == 0
            ? Result.Success()
            : Result.Failure(new Error(
                ErrorKind.Storage,
                $"Succeeded, but renumbering failed for: {string.Join("; ", failures)}.",
                chapter.Name));
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
