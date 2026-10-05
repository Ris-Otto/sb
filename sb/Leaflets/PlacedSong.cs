using sb.Songs;

namespace sb.Leaflets;

/// <summary>
/// A song after physical ordering — <see cref="PhysicalPosition"/> is an internal layout detail,
/// never displayed. The number shown to the user is always <see cref="Songs.Song.SingingOrderNumber"/>.
/// </summary>
public sealed class PlacedSong
{
    public Song Song { get; }
    public int PhysicalPosition { get; }

    public PlacedSong(Song song, int physicalPosition)
    {
        Song = song;
        PhysicalPosition = physicalPosition;
    }
}
