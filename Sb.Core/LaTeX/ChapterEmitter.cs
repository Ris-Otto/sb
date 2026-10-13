using System.Text;

namespace Sb.Core.LaTeX;

/// <summary>
/// Renders a chapter's songs to LaTeX (FR-1.12.5–FR-1.12.10). Two independent rendering paths
/// mirror the two layout modes from earlier milestones:
///
/// <see cref="RenderMultiColumn"/> consumes the <see cref="Page"/>/<see cref="SongFragment"/> data
/// Milestone 5 already computed — each fragment's line count was decided there, so wrapping here
/// must reuse the same <c>columnWidth</c> budget (via <see cref="LineWrapper"/>) rather than
/// trusting LaTeX's own text flow, or the rendered output could overflow the minipage it was
/// planned to fit. Continuation notices (FR-1.3.16) come straight from the fragment's
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
            AppendHeader(sb, song);
            foreach (string line in song.Text)
                sb.AppendLine($"{LaTeXText.Escape(line)}\\\\");
        }

        return sb.ToString();
    }

    // \paragraph* is a run-in heading, so \mbox{}\\ is needed to end its line; otherwise the first
    // lyric line would follow the title on the same line. Melody and writer get their own lines.
    private static void AppendHeader(StringBuilder sb, Song song)
    {
        sb.AppendLine($"\\paragraph*{{{song.Number}. {LaTeXText.Escape(song.Title)}}}\\mbox{{}}\\\\");
        if (!string.IsNullOrWhiteSpace(song.Melody))
            sb.AppendLine($"Mel: {LaTeXText.Escape(song.Melody)}\\\\");
        if (!string.IsNullOrWhiteSpace(song.Writer))
            sb.AppendLine($"Text: {LaTeXText.Escape(song.Writer)}\\\\");
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
