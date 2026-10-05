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
        Result titleValidity = NameValidator.Validate(title);
        if (titleValidity.IsFailure)
            return Result<Song>.Failure(titleValidity.Error!);
        if (text is null || text.Count == 0)
            return Result<Song>.Failure(new Error(ErrorKind.Validation, "A song must have text.", title));
        if (number < 1)
            return Result<Song>.Failure(new Error(ErrorKind.Validation, "A song's number must be at least 1.", title));

        return Result<Song>.Success(new Song(title, writer, melody, number, text));
    }

    public Song WithNumber(int number) => new(Title, Writer, Melody, number, Text);
}
