namespace Sb.Core;

/// <summary>A song's total height in output lines when laid out in a column: its title line
/// (FR-1.12.7's <c>\paragraph*</c>) plus its wrapped text (FR-1.2.8, FR-1.2.9).</summary>
public static class SongLayout
{
    public static int Height(Song song, int columnWidth) => 1 + LineWrapper.CountOutputLines(song.Text, columnWidth);
}
