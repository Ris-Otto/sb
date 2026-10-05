using System.Text;
using sb.Documents;
using sb.Formatting;
using sb.Index;
using sb.Songs;
using YamlDotNet.Serialization;

namespace sb.Chapters;

/// <summary>
/// Adds/removes/lists chapters within a document (FR-014/FR-020). A chapter's location is
/// always derived from its parent document's indexed path — chapters carry no path of their
/// own (research.md §13).
/// </summary>
public sealed class ChapterStore
{
    private const string MetadataFileName = "chapter.yml";
    private const string ChaptersFolderName = "Chapters";

    private readonly SbIndexDatabase _index;
    private readonly DocumentStore _documents;
    private readonly ISerializer _serializer = new SerializerBuilder().Build();
    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    public ChapterStore(SbIndexDatabase index, DocumentStore documents)
    {
        _index = index;
        _documents = documents;
    }

    public string ResolveChapterPath(string documentPath, string chapterName) =>
        Path.Combine(documentPath, ChaptersFolderName, chapterName);

    public Chapter Add(string documentName, string name, string? title = null)
    {
        var document = _documents.Find(documentName);

        using (var check = _index.Connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM chapters WHERE document_id = $documentId AND name = $name;";
            check.Parameters.AddWithValue("$documentId", document.Id);
            check.Parameters.AddWithValue("$name", name);
            if ((long)check.ExecuteScalar()! > 0)
                throw new InvalidOperationException($"Document '{documentName}' already has a chapter named '{name}'.");
        }

        var chapterPath = ResolveChapterPath(document.Path, name);
        Directory.CreateDirectory(chapterPath);
        if (title is not null)
            File.WriteAllText(Path.Combine(chapterPath, MetadataFileName), _serializer.Serialize(new ChapterMetadataFile { Title = title }), TextEncoding.Utf8NoBom);

        using var insert = _index.Connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO chapters (name, document_id) VALUES ($name, $documentId); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$documentId", document.Id);
        var id = (long)insert.ExecuteScalar()!;

        return new Chapter((int)id, name, title, Array.Empty<Song>());
    }

    public IReadOnlyList<Chapter> List(string documentName)
    {
        var document = _documents.Find(documentName);
        var chapters = new List<Chapter>();

        using var command = _index.Connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM chapters WHERE document_id = $documentId;";
        command.Parameters.AddWithValue("$documentId", document.Id);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var title = ReadTitle(ResolveChapterPath(document.Path, name));
            chapters.Add(new Chapter(id, name, title, Array.Empty<Song>()));
        }

        return chapters;
    }

    public void Remove(string documentName, IEnumerable<string> names)
    {
        var document = _documents.Find(documentName);
        var requested = names.ToList();
        var existing = List(documentName).ToDictionary(c => c.Name);

        var unknown = requested.Where(n => !existing.ContainsKey(n)).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"Document '{documentName}' has no chapter(s) named: {string.Join(", ", unknown)}. Nothing was removed.");

        foreach (var name in requested)
        {
            var chapter = existing[name];

            using (var deleteSongs = _index.Connection.CreateCommand())
            {
                deleteSongs.CommandText = "DELETE FROM songs WHERE chapter_id = $chapterId;";
                deleteSongs.Parameters.AddWithValue("$chapterId", chapter.Id);
                deleteSongs.ExecuteNonQuery();
            }

            using (var deleteChapter = _index.Connection.CreateCommand())
            {
                deleteChapter.CommandText = "DELETE FROM chapters WHERE id = $id;";
                deleteChapter.Parameters.AddWithValue("$id", chapter.Id);
                deleteChapter.ExecuteNonQuery();
            }

            var chapterPath = ResolveChapterPath(document.Path, name);
            if (Directory.Exists(chapterPath))
                Directory.Delete(chapterPath, recursive: true);
        }
    }

    private string? ReadTitle(string chapterPath)
    {
        var file = Path.Combine(chapterPath, MetadataFileName);
        if (!File.Exists(file))
            return null;

        return _deserializer.Deserialize<ChapterMetadataFile>(File.ReadAllText(file, Encoding.UTF8))?.Title;
    }
}
