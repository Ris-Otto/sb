namespace Sb.Core;

/// <summary>A song's total height in output lines when laid out in a column: its header lines
/// (title, melody, writer; FR-1.12.7, FR-1.12.11–13) plus its wrapped text (FR-1.2.8, FR-1.2.9).</summary>
public static class SongLayout
{
    /// <summary>Lines above the lyrics: the title, plus a melody and/or writer line when present.</summary>
    public static int HeaderLines(Song song) =>
        1 + (string.IsNullOrWhiteSpace(song.Melody) ? 0 : 1) + (string.IsNullOrWhiteSpace(song.Writer) ? 0 : 1);

    public static int Height(Song song, int columnWidth) => HeaderLines(song) + LineWrapper.CountOutputLines(song.Text, columnWidth);
}
