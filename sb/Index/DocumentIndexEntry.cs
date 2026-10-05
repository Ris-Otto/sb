namespace sb.Index;

/// <summary>Mirrors one row of the SQLite <c>documents</c> table (contracts/storage-format.md).</summary>
public sealed class DocumentIndexEntry
{
    public int Id { get; }
    public string Path { get; }
    public string Filename { get; }

    public DocumentIndexEntry(int id, string path, string filename)
    {
        Id = id;
        Path = path;
        Filename = filename;
    }
}
