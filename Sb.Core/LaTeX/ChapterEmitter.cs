using System.Text;

namespace Sb.Core.LaTeX;

/// <summary>
/// Renders a chapter's songs to LaTeX (FR-1.12.5–FR-1.12.15): each song as a header (the
/// <c>N. Title</c> line, then <c>Mel:</c>/<c>Text:</c> lines when present) followed by its lyrics.
/// Two independent rendering paths mirror the two layout modes:
///
/// <see cref="RenderMultiColumn"/> consumes the <see cref="Page"/>/<see cref="SongFragment"/> data
/// the paginators already computed — each fragment's line count (including its header and
/// <c>(forts.)</c> notice lines) was decided there, so wrapping here must reuse the same
/// <c>columnWidth</c> budget (via <see cref="LineWrapper"/>) rather than trusting LaTeX's own text
/// flow, or the rendered output could overflow the minipage it was planned to fit. Continuation
/// notices (FR-1.3.16) come straight from the fragment's
/// <see cref="SongFragment.ContinuesOnNextPage"/>/<see cref="SongFragment.ContinuesFromPreviousPage"/>
/// flags.
///
/// <see cref="RenderSingleColumn"/> bypasses all of that: single-column mode is plain LaTeX text
/// flow (§2.1), so there's no column budget to plan against and no page boundary SB itself knows
/// about — LaTeX decides where pages break, invisibly to us, so no continuation notice can be
/// placed correctly in this mode.
/// </summary>
public static class ChapterEmitter
{
    private const string MinipageWidth = "0.48\\textwidth";

    /// <summary>In single-column mode, at most this many lyric lines are kept on the same page as the
    /// song's header.</summary>
    private const int MaxLyricLinesKeptWithHeader = 10;

    public static string RenderMultiColumn(string chapterTitle, IReadOnlyList<Page> pages, int columnWidth)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\\chapter*{{{LaTeXText.Escape(chapterTitle)}}}");

        foreach (Page page in pages)
        {
            VSpaceCorrection correction = VerticalAlignment.Compute(page.Column1Lines, page.Column2Lines);

            // [t] top-aligns both minipages, so they already start at the same line (FR-1.2.7); the
            // vspace correction below is what makes them also end at the same line.
            sb.AppendLine($"\\noindent\\begin{{minipage}}[t]{{{MinipageWidth}}}");
            foreach (SongFragment fragment in page.Column1)
                AppendFragment(sb, fragment, columnWidth);
            if (correction.Column1VSpace > 0)
                sb.AppendLine($"\\vspace{{{correction.Column1VSpace}\\baselineskip}}");
            sb.AppendLine("\\end{minipage}%");
            sb.AppendLine("\\hfill");
            sb.AppendLine($"\\begin{{minipage}}[t]{{{MinipageWidth}}}");
            foreach (SongFragment fragment in page.Column2)
                AppendFragment(sb, fragment, columnWidth);
            if (correction.Column2VSpace > 0)
                sb.AppendLine($"\\vspace{{{correction.Column2VSpace}\\baselineskip}}");
            sb.AppendLine("\\end{minipage}");
            sb.AppendLine("\\clearpage");
        }

        return sb.ToString();
    }

    public static string RenderSingleColumn(string chapterTitle, IReadOnlyList<Song> songs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\\chapter*{{{LaTeXText.Escape(chapterTitle)}}}");

        foreach (Song song in songs)
        {
            // LaTeX decides where pages break here, so SB can't plan around it as it does in multi-column
            // mode. What it can do is forbid a break between the song's header and its first paragraph
            // (`\\*` is a line break that doesn't allow a page break after it), so a title is never left
            // alone at the bottom of a page. The kept-together part is capped, so a very long first
            // paragraph can't produce a page that doesn't fit.
            int firstParagraph = song.Text.TakeWhile(line => !string.IsNullOrWhiteSpace(line)).Count();
            int lyricLinesKept = Math.Clamp(firstParagraph, 1, MaxLyricLinesKeptWithHeader);

            List<string> lines = HeaderLatex(song).ToList();
            lines.AddRange(song.Text.Select(LaTeXText.Escape));
            int keptTogether = lines.Count - song.Text.Count + Math.Min(lyricLinesKept, song.Text.Count);

            for (int i = 0; i < lines.Count; i++)
                sb.AppendLine(lines[i] + (i < keptTogether - 1 ? "\\\\*" : "\\\\"));
        }

        return sb.ToString();
    }

    // \paragraph* is a run-in heading, so \mbox{}\\ is needed to end its line; otherwise the first
    // lyric line would follow the title on the same line. Melody and writer get their own lines.
    private static void AppendHeader(StringBuilder sb, Song song)
    {
        foreach (string line in HeaderLatex(song))
            sb.AppendLine(line + "\\\\");
    }

    /// <summary>The header's lines, without their line-break terminators: <c>N. Title</c>, then the
    /// <c>Mel:</c>/<c>Text:</c> lines when present (FR-1.12.7, FR-1.12.11–15).</summary>
    private static IEnumerable<string> HeaderLatex(Song song)
    {
        yield return $"\\paragraph*{{{song.Number}. {LaTeXText.Escape(song.Title)}}}\\mbox{{}}";
        if (!string.IsNullOrWhiteSpace(song.Melody))
            yield return $"Mel: {LaTeXText.Escape(song.Melody)}";
        if (!string.IsNullOrWhiteSpace(song.Writer))
            yield return $"Text: {LaTeXText.Escape(song.Writer)}";
    }

    private static void AppendFragment(StringBuilder sb, SongFragment fragment, int columnWidth)
    {
        if (fragment.ContinuesFromPreviousPage)
            sb.AppendLine("\\textit{(forts.)}\\\\");

        if (fragment.IncludesTitle)
            AppendHeader(sb, fragment.Song);

        foreach (string line in fragment.Lines)
        {
            foreach (string wrapped in LineWrapper.Wrap(line, columnWidth))
                sb.AppendLine($"{LaTeXText.Escape(wrapped)}\\\\");
        }

        if (fragment.ContinuesOnNextPage)
            sb.AppendLine("\\textit{(forts.)}\\\\");
    }
}
