namespace Sb.Core;

public sealed class Song
{
    public string Title { get; }
    public string? Writer { get; }
    public string? Melody { get; }
    public int Number { get; }
    public IReadOnlyList<string> Text { get; }

    private Song(string title, string? writer, string? melody, int number, IReadOnlyList<string> text)
    {
        Title = title;
        Writer = writer;
        Melody = melody;
        Number = number;
        Text = text;
    }

    public static Result<Song> Create(
        string title, IReadOnlyList<string> text, int number, string? writer = null, string? melody = null)
    {
        Result titleValidity = NameValidator.Validate(title, allowTrailingDot: true);
        if (titleValidity.IsFailure)
            return Result<Song>.Failure(titleValidity.Error!);
        // Blank lines around the lyrics would only add empty lines between songs.
        text = TrimBlankLines(text);
        if (text.Count == 0)
            return Result<Song>.Failure(new Error(ErrorKind.Validation, "A song must have text.", title));
        if (number < 1)
            return Result<Song>.Failure(new Error(ErrorKind.Validation, "A song's number must be at least 1.", title));

        return Result<Song>.Success(new Song(title, writer, melody, number, text));
    }

    private static IReadOnlyList<string> TrimBlankLines(IReadOnlyList<string>? text)
    {
        if (text is null)
            return [];
        int start = 0;
        int end = text.Count;
        while (start < end && string.IsNullOrWhiteSpace(text[start]))
            start++;
        while (end > start && string.IsNullOrWhiteSpace(text[end - 1]))
            end--;
        return start == 0 && end == text.Count ? text : text.Skip(start).Take(end - start).ToList();
    }

    public Song WithNumber(int number) => new(Title, Writer, Melody, number, Text);
}
