namespace sb.Leaflets;

public sealed class Leaflet
{
    public LeafletFormat Format { get; }
    public IReadOnlyList<PlacedSong> Songs { get; }
    public string Document { get; }
    public IReadOnlyList<LeafletChapterFile> ChapterFiles { get; }

    public Leaflet(
        LeafletFormat format,
        IReadOnlyList<PlacedSong> songs,
        string document,
        IReadOnlyList<LeafletChapterFile> chapterFiles)
    {
        Format = format;
        Songs = songs;
        Document = document;
        ChapterFiles = chapterFiles;
    }
}
