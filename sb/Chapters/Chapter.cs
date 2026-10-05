using sb.Songs;

namespace sb.Chapters;

public sealed class Chapter
{
    public int Id { get; }
    public string Name { get; }
    public string? Title { get; }
    public IReadOnlyList<Song> Songs { get; }

    public Chapter(int id, string name, string? title, IReadOnlyList<Song> songs)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));

        Id = id;
        Name = name;
        Title = title;
        Songs = songs;
    }
}
