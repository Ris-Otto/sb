using sb.Ordering;

namespace sb.Leaflets;

public sealed class LeafletRequest
{
    public string Title { get; }
    public string? Subtitle { get; }
    public IReadOnlyList<LeafletChapter> Chapters { get; }
    public LeafletFormat Format { get; }
    public OrderingMode Ordering { get; }

    public LeafletRequest(
        string title,
        IReadOnlyList<LeafletChapter> chapters,
        LeafletFormat format,
        OrderingMode ordering,
        string? subtitle = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must be non-empty.", nameof(title));
        if (!chapters.SelectMany(c => c.Songs).Any())
            throw new ArgumentException("At least one song is required (FR-012).", nameof(chapters));

        Title = title;
        Subtitle = subtitle;
        Chapters = chapters;
        Format = format;
        Ordering = ordering;
    }
}
