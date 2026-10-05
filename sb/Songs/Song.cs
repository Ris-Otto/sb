namespace sb.Songs;

public sealed class Song
{
    public string Title { get; }
    public string Text { get; }
    public string? Writer { get; }
    public string? Melody { get; }
    public int SingingOrderNumber { get; }
    public IReadOnlyList<string>? Score { get; }

    public Song(
        string title,
        string text,
        int singingOrderNumber,
        string? writer = null,
        string? melody = null,
        IReadOnlyList<string>? score = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must be non-empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must be non-empty.", nameof(text));

        Title = title;
        Text = text;
        SingingOrderNumber = singingOrderNumber;
        Writer = writer;
        Melody = melody;
        Score = score;
    }
}
