namespace Sb.Core;

/// <summary>
/// Keeps song numbers contiguous (FR-1.4.3): a song's number is always its current 1-based
/// sequential position, so inserting/removing a song renumbers everything from that point on.
/// Callers are expected to have already validated <c>position</c> against the list's current
/// bounds (e.g. a CLI command validating a user-supplied <c>--number</c>); an out-of-range
/// position throws <see cref="ArgumentOutOfRangeException"/> rather than failing silently.
/// </summary>
public static class SongNumbering
{
    /// <param name="position">1-based target position; must be in [1, songs.Count + 1].</param>
    public static IReadOnlyList<Song> InsertAt(IReadOnlyList<Song> songs, int position, Song newSong)
    {
        List<Song> list = songs.ToList();
        list.Insert(position - 1, newSong);
        return Renumber(list);
    }

    public static IReadOnlyList<Song> Append(IReadOnlyList<Song> songs, Song newSong) =>
        InsertAt(songs, songs.Count + 1, newSong);

    /// <param name="position">1-based target position; must be in [1, songs.Count].</param>
    public static IReadOnlyList<Song> RemoveAt(IReadOnlyList<Song> songs, int position)
    {
        List<Song> list = songs.ToList();
        list.RemoveAt(position - 1);
        return Renumber(list);
    }

    private static IReadOnlyList<Song> Renumber(List<Song> songs)
    {
        for (int i = 0; i < songs.Count; i++)
            songs[i] = songs[i].WithNumber(i + 1);
        return songs;
    }
}
