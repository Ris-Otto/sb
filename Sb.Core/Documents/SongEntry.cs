namespace Sb.Core.Documents;

/// <summary>One song in a `songs list` result (FR-1.11.3), naming the chapter it belongs to since a
/// document-wide listing spans more than one chapter. <see cref="MissingAt"/> is the path of the song's
/// file when the database expects it but it isn't on disk (FR-1.7.5).</summary>
public sealed record SongEntry(string ChapterName, string Title, int Number)
{
    public string? MissingAt { get; init; }
}
