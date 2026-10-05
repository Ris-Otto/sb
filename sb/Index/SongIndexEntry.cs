namespace sb.Index;

/// <summary>Mirrors one row of the SQLite <c>songs</c> table (contracts/storage-format.md).</summary>
public sealed class SongIndexEntry
{
    public int Id { get; }
    public string Name { get; }
    public int SingingOrderNumber { get; }
    public int ChapterId { get; }

    public SongIndexEntry(int id, string name, int singingOrderNumber, int chapterId)
    {
        Id = id;
        Name = name;
        SingingOrderNumber = singingOrderNumber;
        ChapterId = chapterId;
    }
}
