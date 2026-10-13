namespace Sb.Core.Documents;

/// <summary>The result of `documents list` (FR-1.9.2): a document, optionally with its chapters, each
/// optionally with its songs. <see cref="Chapters"/>/<see cref="ChapterListing.Songs"/> are empty
/// when not requested, not null, so a front-end never has to null-check them — whether to print
/// them at all is decided by the flags the caller passed in, not by inspecting these lists.
/// <see cref="MissingAt"/> (on each listing) is the path the database expects when it isn't on disk
/// (FR-1.7.5), so a front-end can tell the user about the mismatch.</summary>
public sealed record DocumentListing(
    string Name, string Title, IReadOnlyList<ChapterListing> Chapters, IReadOnlyList<SongListing>? Songs = null)
{
    /// <summary>The document's songs across all chapters in number order, when songs were requested
    /// without chapters (`documents list -s`). When chapters are listed too, songs sit under their
    /// chapters instead and this is empty.</summary>
    public IReadOnlyList<SongListing> Songs { get; init; } = Songs ?? [];

    public string? MissingAt { get; init; }
}

public sealed record ChapterListing(string Name, string Title, IReadOnlyList<SongListing> Songs)
{
    public string? MissingAt { get; init; }
}

public sealed record SongListing(string Title, int Number)
{
    public string? MissingAt { get; init; }
}
