using sb.Songs;

namespace sb.Leaflets;

/// <summary>
/// One chapter's worth of input to a leaflet build: its own songs, laid out and ordered
/// independently of every other chapter (2026-09-26 — a chapter is rendered into its own
/// `chapter.tex`, `\input`'d from the main document, per `sb/specification.md` §2.5.1/§4).
/// </summary>
public sealed class LeafletChapter
{
    public string Name { get; }
    public string? Title { get; }
    public IReadOnlyList<Song> Songs { get; }

    public LeafletChapter(string name, string? title, IReadOnlyList<Song> songs)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));

        Name = name;
        Title = title;
        Songs = songs;
    }
}
