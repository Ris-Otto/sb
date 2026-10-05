using System.Text;
using System.Text.RegularExpressions;
using sb.Chapters;
using sb.Documents;
using sb.Formatting;
using sb.Index;
using YamlDotNet.Serialization;

namespace sb.Songs;

/// <summary>
/// Adds/removes/lists/modifies songs within a chapter (FR-015/FR-021). The filesystem/index
/// slug used for a song's single `.yml` file and its `songs.name` index column is derived from
/// its title (there is no separate `--name` flag in contracts/cli-commands.md's `songs add`) —
/// `--song-name` in the CLI contract addresses a song by this same derived slug.
///
/// As of 2026-09-26, a song's own `.yml` file (research.md §9) is the source of truth for all of
/// its data, including its singing-order number; `SongStore` mirrors that number into the
/// `songs.singing_order_number` index column on every write, purely so the uniqueness check
/// (FR-016) can run as a single indexed query instead of opening every song's file (research.md
/// §12).
/// </summary>
public sealed class SongStore
{
    private readonly SbIndexDatabase _index;
    private readonly ChapterStore _chapters;
    private readonly DocumentStore _documents;
    private readonly ISerializer _serializer = new SerializerBuilder().Build();
    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    public SongStore(SbIndexDatabase index, ChapterStore chapters, DocumentStore documents)
    {
        _index = index;
        _chapters = chapters;
        _documents = documents;
    }

    public Song Add(
        string documentName,
        string chapterName,
        string title,
        string text,
        int singingOrderNumber,
        string? writer = null,
        string? melody = null,
        IReadOnlyList<string>? score = null)
    {
        var document = _documents.Find(documentName);
        var chapter = _chapters.List(documentName).FirstOrDefault(c => c.Name == chapterName)
            ?? throw new InvalidOperationException($"Document '{documentName}' has no chapter named '{chapterName}'.");

        var conflict = FindConflictingSongTitle(document.Path, document.Id, singingOrderNumber, exclude: null);
        if (conflict is not null)
        {
            throw new InvalidOperationException(
                $"Singing-order number {singingOrderNumber} is already used by '{conflict}' in document '{documentName}' (FR-016).");
        }

        var chapterPath = _chapters.ResolveChapterPath(document.Path, chapterName);
        var slug = UniqueSlug(chapterPath, Slugify(title));

        WriteSongFile(chapterPath, slug, title, text, singingOrderNumber, writer, melody, score);

        using var insert = _index.Connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO songs (name, singing_order_number, chapter_id) VALUES ($name, $number, $chapterId);";
        insert.Parameters.AddWithValue("$name", slug);
        insert.Parameters.AddWithValue("$number", singingOrderNumber);
        insert.Parameters.AddWithValue("$chapterId", chapter.Id);
        insert.ExecuteNonQuery();

        return new Song(title, text, singingOrderNumber, writer, melody, score);
    }

    public IReadOnlyList<Song> List(string documentName, string? chapterName = null)
    {
        var document = _documents.Find(documentName);
        var chapters = _chapters.List(documentName)
            .Where(c => chapterName is null || c.Name == chapterName)
            .ToList();

        var songs = new List<Song>();
        foreach (var chapter in chapters)
        {
            var chapterPath = _chapters.ResolveChapterPath(document.Path, chapter.Name);

            using var command = _index.Connection.CreateCommand();
            command.CommandText = "SELECT name FROM songs WHERE chapter_id = $chapterId;";
            command.Parameters.AddWithValue("$chapterId", chapter.Id);
            using var reader = command.ExecuteReader();
            var slugs = new List<string>();
            while (reader.Read())
                slugs.Add(reader.GetString(0));

            foreach (var slug in slugs)
            {
                var file = ReadSongFile(chapterPath, slug);
                songs.Add(new Song(file.Title, JoinLines(file.Text), file.Number, file.Writer, file.Melody, file.Score));
            }
        }

        return songs;
    }

    public void Remove(string documentName, string songName)
    {
        var (chapter, chapterPath, slug, _) = Locate(documentName, songName);

        using var delete = _index.Connection.CreateCommand();
        delete.CommandText = "DELETE FROM songs WHERE chapter_id = $chapterId AND name = $name;";
        delete.Parameters.AddWithValue("$chapterId", chapter.Id);
        delete.Parameters.AddWithValue("$name", slug);
        delete.ExecuteNonQuery();

        File.Delete(Path.Combine(chapterPath, $"{slug}.yml"));
    }

    /// <summary>
    /// Updates only the specified field(s). Moving <paramref name="chapter"/> never changes the
    /// singing-order number (FR-024); changing <paramref name="singingOrderNumber"/> re-runs the
    /// same uniqueness check as <see cref="Add"/> (FR-016).
    /// </summary>
    public void Modify(
        string documentName,
        string songName,
        string? title = null,
        string? writer = null,
        string? melody = null,
        string? chapter = null,
        int? singingOrderNumber = null)
    {
        var document = _documents.Find(documentName);
        var (currentChapter, currentChapterPath, slug, currentNumber) = Locate(documentName, songName);

        if (singingOrderNumber.HasValue && singingOrderNumber.Value != currentNumber)
        {
            var conflict = FindConflictingSongTitle(
                document.Path, document.Id, singingOrderNumber.Value, exclude: (currentChapter.Id, slug));
            if (conflict is not null)
            {
                throw new InvalidOperationException(
                    $"Singing-order number {singingOrderNumber} is already used by '{conflict}' in document '{documentName}' (FR-016).");
            }
        }

        var file = ReadSongFile(currentChapterPath, slug);
        var targetChapter = currentChapter;
        var targetChapterPath = currentChapterPath;

        if (chapter is not null && chapter != currentChapter.Name)
        {
            targetChapter = _chapters.List(documentName).FirstOrDefault(c => c.Name == chapter)
                ?? throw new InvalidOperationException($"Document '{documentName}' has no chapter named '{chapter}'.");
            targetChapterPath = _chapters.ResolveChapterPath(document.Path, chapter);

            File.Move(Path.Combine(currentChapterPath, $"{slug}.yml"), Path.Combine(targetChapterPath, $"{slug}.yml"));
        }

        var newNumber = singingOrderNumber ?? currentNumber;
        WriteSongFile(
            targetChapterPath,
            slug,
            title ?? file.Title,
            JoinLines(file.Text),
            newNumber,
            writer ?? file.Writer,
            melody ?? file.Melody,
            file.Score);

        using var update = _index.Connection.CreateCommand();
        update.CommandText =
            "UPDATE songs SET chapter_id = $chapterId, singing_order_number = $number WHERE chapter_id = $oldChapterId AND name = $name;";
        update.Parameters.AddWithValue("$chapterId", targetChapter.Id);
        update.Parameters.AddWithValue("$number", newNumber);
        update.Parameters.AddWithValue("$oldChapterId", currentChapter.Id);
        update.Parameters.AddWithValue("$name", slug);
        update.ExecuteNonQuery();
    }

    /// <summary>
    /// Returns a song's singing-order number, independent of its physical position (FR-017),
    /// read from its own `.yml` file (the source of truth, research.md §12).
    /// </summary>
    public int GetSingingOrderNumber(string documentName, string songName) => Locate(documentName, songName).currentNumber;

    private (Chapter chapter, string chapterPath, string slug, int currentNumber) Locate(string documentName, string songName)
    {
        var document = _documents.Find(documentName);
        var slug = Slugify(songName);

        foreach (var chapter in _chapters.List(documentName))
        {
            var chapterPath = _chapters.ResolveChapterPath(document.Path, chapter.Name);
            if (!File.Exists(Path.Combine(chapterPath, $"{slug}.yml")))
                continue;

            using var command = _index.Connection.CreateCommand();
            command.CommandText = "SELECT id FROM songs WHERE chapter_id = $chapterId AND name = $name;";
            command.Parameters.AddWithValue("$chapterId", chapter.Id);
            command.Parameters.AddWithValue("$name", slug);
            _ = command.ExecuteScalar()
                ?? throw new InvalidOperationException($"Song file '{slug}' exists but has no index row.");

            var number = ReadSongFile(chapterPath, slug).Number;
            return (chapter, chapterPath, slug, number);
        }

        throw new InvalidOperationException($"Document '{documentName}' has no song named '{songName}'.");
    }

    private string? FindConflictingSongTitle(
        string documentPath, int documentId, int singingOrderNumber, (int chapterId, string slug)? exclude)
    {
        using var command = _index.Connection.CreateCommand();
        command.CommandText =
            "SELECT songs.name, songs.chapter_id, chapters.name FROM songs " +
            "JOIN chapters ON songs.chapter_id = chapters.id " +
            "WHERE chapters.document_id = $documentId AND songs.singing_order_number = $number;";
        command.Parameters.AddWithValue("$documentId", documentId);
        command.Parameters.AddWithValue("$number", singingOrderNumber);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var songSlug = reader.GetString(0);
            var chapterId = reader.GetInt32(1);
            var chapterName = reader.GetString(2);
            if (exclude is { } excluded && songSlug == excluded.slug && chapterId == excluded.chapterId)
                continue; // this is the song being modified, not a real conflict

            var chapterPath = _chapters.ResolveChapterPath(documentPath, chapterName);
            return ReadSongFile(chapterPath, songSlug).Title;
        }

        return null;
    }

    private void WriteSongFile(
        string chapterPath,
        string slug,
        string title,
        string text,
        int number,
        string? writer,
        string? melody,
        IReadOnlyList<string>? score) =>
        File.WriteAllText(
            Path.Combine(chapterPath, $"{slug}.yml"),
            _serializer.Serialize(new SongFile
            {
                Title = title,
                Writer = writer,
                Melody = melody,
                Number = number,
                Text = SplitLines(text),
                Score = score?.ToList()
            }),
            TextEncoding.Utf8NoBom);

    private SongFile ReadSongFile(string chapterPath, string slug) =>
        _deserializer.Deserialize<SongFile>(File.ReadAllText(Path.Combine(chapterPath, $"{slug}.yml"), Encoding.UTF8))
        ?? new SongFile();

    private static List<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n').ToList();

    private static string JoinLines(List<string> lines) => string.Join("\n", lines);

    private static string UniqueSlug(string chapterPath, string baseSlug)
    {
        var candidate = baseSlug;
        var suffix = 2;
        while (File.Exists(Path.Combine(chapterPath, $"{candidate}.yml")))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string title)
    {
        var lowered = title.Trim().ToLowerInvariant();
        var withUnderscores = Regex.Replace(lowered, @"\s+", "_");
        var stripped = Regex.Replace(withUnderscores, @"[^a-z0-9_\-]", "");
        return stripped.Length > 0 ? stripped : "song";
    }
}
