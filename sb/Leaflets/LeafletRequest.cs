using sb.Ordering;

namespace sb.Leaflets;

public sealed class LeafletRequest
{
    public string Title { get; }
    public string? Subtitle { get; }
    public IReadOnlyList<LeafletChapter> Chapters { get; }
    public LeafletFormat Format { get; }
    public OrderingMode Ordering { get; }
    public int MaxPermutations { get; }

    public LeafletRequest(
        string title,
        IReadOnlyList<LeafletChapter> chapters,
        LeafletFormat format,
        OrderingMode ordering,
        int maxPermutations = 100,
        string? subtitle = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must be non-empty.", nameof(title));
        if (!chapters.SelectMany(c => c.Songs).Any())
            throw new ArgumentException("At least one song is required (FR-012).", nameof(chapters));
        if (maxPermutations <= 0)
            throw new ArgumentException("MaxPermutations must be a positive integer.", nameof(maxPermutations));

        Title = title;
        Subtitle = subtitle;
        Chapters = chapters;
        Format = format;
        Ordering = ordering;
        MaxPermutations = maxPermutations;
    }
}
