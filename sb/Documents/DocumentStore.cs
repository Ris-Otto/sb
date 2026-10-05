using sb.Chapters;
using sb.Formatting;
using sb.Index;
using sb.Leaflets;
using System.Text;

namespace sb.Documents;

/// <summary>
/// Creates/lists/deletes/renames documents (FR-013, FR-018/FR-019): the folder may live
/// anywhere on disk (recorded in the `documents` index table), so lookups always go through the
/// index rather than scanning the filesystem (FR-022).
///
/// As of 2026-09-26, a document has no separate metadata file — its single `&lt;name&gt;.tex`
/// (rendered from `sb/template-main.tex` immediately at `create` time, named after `--name`
/// rather than a fixed `main.tex`) is the only per-document file, and is also where its title/subtitle are
/// recovered from on every subsequent read (research.md §9/§14,
/// `Leaflets.DocumentTemplate.ParseTitleAndSubtitle`). An empty `Chapters/` folder is created
/// alongside it at `create` time too. `Rename` renames this file to match the new name, keeping
/// it consistent with `documents.filename`.
/// </summary>
public sealed class DocumentStore
{
    private const string ChaptersFolderName = "Chapters";

    private readonly SbIndexDatabase _index;
    private readonly string _templatePath;

    public DocumentStore(SbIndexDatabase index, string? templatePath = null)
    {
        _index = index;
        _templatePath = templatePath ?? Path.Combine(AppContext.BaseDirectory, "template-main.tex");
    }

    public Document Create(string name, string title, string path, string? subtitle = null)
    {
        using (var checkCommand = _index.Connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(*) FROM documents WHERE path = $path;";
            checkCommand.Parameters.AddWithValue("$path", path);
            var existing = (long)checkCommand.ExecuteScalar()!;
            if (existing > 0)
                throw new InvalidOperationException($"A document already exists at path '{path}'.");
        }

        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ChaptersFolderName));
        WriteMainTex(path, name, title, subtitle, content: "");

        using var insertCommand = _index.Connection.CreateCommand();
        insertCommand.CommandText =
            "INSERT INTO documents (path, filename) VALUES ($path, $filename); SELECT last_insert_rowid();";
        insertCommand.Parameters.AddWithValue("$path", path);
        insertCommand.Parameters.AddWithValue("$filename", name);
        var id = (long)insertCommand.ExecuteScalar()!;

        return new Document((int)id, name, path, title, subtitle, Array.Empty<Chapter>());
    }

    public IReadOnlyList<Document> List()
    {
        var documents = new List<Document>();

        using var command = _index.Connection.CreateCommand();
        command.CommandText = "SELECT id, path, filename FROM documents;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var path = reader.GetString(1);
            var filename = reader.GetString(2);
            var (title, subtitle) = ReadTitleAndSubtitle(path, filename);

            documents.Add(new Document(id, filename, path, title, subtitle, Array.Empty<Chapter>()));
        }

        return documents;
    }

    public Document Find(string name) =>
        List().FirstOrDefault(d => d.Name == name)
        ?? throw new InvalidOperationException($"No document named '{name}' was found.");

    public void Delete(string name)
    {
        var document = Find(name);

        // Cascade through chapters/songs first — SQLite enforces the songs.chapter_id and
        // chapters.document_id foreign keys (PRAGMA foreign_keys = ON in SbIndexDatabase), so
        // deleting the documents row before its children would fail.
        using (var deleteSongs = _index.Connection.CreateCommand())
        {
            deleteSongs.CommandText =
                "DELETE FROM songs WHERE chapter_id IN (SELECT id FROM chapters WHERE document_id = $documentId);";
            deleteSongs.Parameters.AddWithValue("$documentId", document.Id);
            deleteSongs.ExecuteNonQuery();
        }

        using (var deleteChapters = _index.Connection.CreateCommand())
        {
            deleteChapters.CommandText = "DELETE FROM chapters WHERE document_id = $documentId;";
            deleteChapters.Parameters.AddWithValue("$documentId", document.Id);
            deleteChapters.ExecuteNonQuery();
        }

        using (var deleteDocument = _index.Connection.CreateCommand())
        {
            deleteDocument.CommandText = "DELETE FROM documents WHERE id = $id;";
            deleteDocument.Parameters.AddWithValue("$id", document.Id);
            deleteDocument.ExecuteNonQuery();
        }

        if (Directory.Exists(document.Path))
            Directory.Delete(document.Path, recursive: true);
    }

    public void Rename(string name, string newName)
    {
        var document = Find(name);

        using var command = _index.Connection.CreateCommand();
        command.CommandText = "UPDATE documents SET filename = $filename WHERE id = $id;";
        command.Parameters.AddWithValue("$filename", newName);
        command.Parameters.AddWithValue("$id", document.Id);
        command.ExecuteNonQuery();

        // Keep the .tex file's name in sync with `documents.filename` (FR-022's "no scanning,
        // only the index" pattern would otherwise go stale the moment these two diverge).
        var oldFile = Path.Combine(document.Path, TexFileName(name));
        if (File.Exists(oldFile))
            File.Move(oldFile, Path.Combine(document.Path, TexFileName(newName)));
    }

    private static string TexFileName(string name) => $"{name}.tex";

    private void WriteMainTex(string documentPath, string name, string title, string? subtitle, string content)
    {
        var template = DocumentTemplate.Load(_templatePath);
        var escapedTitle = LatexEscaper.Escape(title);
        var escapedSubtitle = subtitle is null ? null : LatexEscaper.Escape(subtitle);
        var rendered = template.Render(escapedTitle, escapedSubtitle, content);
        File.WriteAllText(Path.Combine(documentPath, TexFileName(name)), rendered, TextEncoding.Utf8NoBom);
    }

    private static (string Title, string? Subtitle) ReadTitleAndSubtitle(string documentPath, string name)
    {
        var file = Path.Combine(documentPath, TexFileName(name));
        if (!File.Exists(file))
            return ("(untitled)", null);

        return DocumentTemplate.ParseTitleAndSubtitle(File.ReadAllText(file, Encoding.UTF8));
    }
}
