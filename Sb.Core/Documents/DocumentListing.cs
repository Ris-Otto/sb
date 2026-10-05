namespace Sb.Core.Documents;

/// <summary>The result of `documents list` (FR-1.9.2): a document, optionally with its chapters, each
/// optionally with its songs. <see cref="Chapters"/>/<see cref="ChapterListing.Songs"/> are empty
/// when not requested, not null, so a front-end never has to null-check them — whether to print
/// them at all is decided by the flags the caller passed in, not by inspecting these lists.</summary>
public sealed record DocumentListing(string Name, string Title, IReadOnlyList<ChapterListing> Chapters);

public sealed record ChapterListing(string Name, string Title, IReadOnlyList<SongListing> Songs);

public sealed record SongListing(string Title, int Number);
