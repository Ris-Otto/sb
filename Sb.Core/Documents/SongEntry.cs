namespace Sb.Core.Documents;

/// <summary>One song in a `songs list` result (FR-1.11.3), naming the chapter it belongs to since a
/// document-wide listing spans more than one chapter.</summary>
public sealed record SongEntry(string ChapterName, string Title, int Number);
