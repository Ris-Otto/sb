namespace Sb.Core.Documents;

/// <summary>
/// Reads/writes a chapter's song `.yml` files (FR-1.6.1, FR-1.6.2), shared by
/// <see cref="DocumentOperations"/> (reading for `build`) and <see cref="SongOperations"/> (reading
/// and writing for `add`/`delete`/`modify`, which may need to rewrite another song's file when a
/// renumber shifts it).
/// </summary>
internal static class SongFileStore
{
    public static Result<Song> Load(string documentPath, string chapterName, Storage.SongRecord record)
    {
        string path = DocumentLayout.SongFile(documentPath, chapterName, record.Title);
        if (!File.Exists(path))
            return Result<Song>.Failure(new Error(
                ErrorKind.Storage,
                $"Song '{record.Title}' is recorded in the database but its file is missing at '{path}'.",
                record.Title));

        return SongYamlSerializer.Deserialize(File.ReadAllText(path));
    }

    public static void Save(string documentPath, string chapterName, Song song) =>
        File.WriteAllText(
            DocumentLayout.SongFile(documentPath, chapterName, song.Title), SongYamlSerializer.Serialize(song));

    public static void Delete(string documentPath, string chapterName, string title)
    {
        string path = DocumentLayout.SongFile(documentPath, chapterName, title);
        if (File.Exists(path))
            File.Delete(path);
    }
}
