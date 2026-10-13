namespace Sb.Core;

/// <summary>A song's total height in output lines when laid out in a column: its header lines
/// (title, melody, writer; FR-1.12.7, FR-1.12.11–13) plus its wrapped text (FR-1.2.8, FR-1.2.9).</summary>
public static class SongLayout
{
    /// <summary>The line of space the template puts before every song's title (<c>\titlespacing</c> in
    /// <c>template-main.tex</c>), which takes a column's room like any line does (A-12).</summary>
    public const int TitleSpacingLines = 1;

    /// <summary>Lines above the lyrics: the space before the title, the title, plus a melody and/or writer
    /// line when present.</summary>
    public static int HeaderLines(Song song) =>
        TitleSpacingLines + 1
        + (string.IsNullOrWhiteSpace(song.Melody) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(song.Writer) ? 0 : 1);

    public static int Height(Song song, int columnWidth) => HeaderLines(song) + LineWrapper.CountOutputLines(song.Text, columnWidth);
}
