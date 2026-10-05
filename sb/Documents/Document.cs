using sb.Chapters;

namespace sb.Documents;

/// <summary>
/// A document has no persisted layout format of its own as of 2026-09-26 — `--format` moved from
/// `sb documents create` to `sb documents build`, supplied per build invocation only (spec.md's
/// 2026-09-26 Clarification).
/// </summary>
public sealed class Document
{
    public int Id { get; }
    public string Name { get; }
    public string Path { get; }
    public string Title { get; }
    public string? Subtitle { get; }
    public IReadOnlyList<Chapter> Chapters { get; }

    public Document(
        int id,
        string name,
        string path,
        string title,
        string? subtitle,
        IReadOnlyList<Chapter> chapters)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must be non-empty.", nameof(title));

        Id = id;
        Name = name;
        Path = path;
        Title = title;
        Subtitle = subtitle;
        Chapters = chapters;
    }
}
