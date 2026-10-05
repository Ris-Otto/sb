namespace sb.Index;

/// <summary>Mirrors one row of the SQLite <c>chapters</c> table (contracts/storage-format.md).</summary>
public sealed class ChapterIndexEntry
{
    public int Id { get; }
    public string Name { get; }
    public int DocumentId { get; }

    public ChapterIndexEntry(int id, string name, int documentId)
    {
        Id = id;
        Name = name;
        DocumentId = documentId;
    }
}
