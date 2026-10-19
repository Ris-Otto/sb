using System.Text;

namespace Sb.Core.LaTeX;

/// <summary>
/// Renders a chapter's songs to LaTeX (FR-1.12.5–FR-1.12.16): each song as a header (the
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
///
/// Every line but the last of a fragment (or single-column song) ends in <c>\\</c>. The last one must
/// not: a <c>\\</c> at the end of a paragraph makes LaTeX add an empty line after it, which the
/// planned line counts do not include (A-12).
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
        AppendChapterHeading(sb, chapterTitle);

        foreach (Page page in pages)
        {
            // The planned line count of a column that starts with a title includes the line of space
            // before that title, which is not rendered at the top of a column (see AppendColumn).
            VSpaceCorrection correction = VerticalAlignment.Compute(
                page.Column1Lines - LeadingSpaceLines(page.Column1),
                page.Column2Lines - LeadingSpaceLines(page.Column2));

            // [t] top-aligns both minipages, so they already start at the same line (FR-1.2.7); the
            // vspace correction below is what makes them also end at the same line. \raggedright keeps
            // a line that LaTeX still has to wrap from having its spaces stretched to the column width.
            sb.AppendLine($"\\noindent\\begin{{minipage}}[t]{{{MinipageWidth}}}\\raggedright");
            AppendColumn(sb, page.Column1, columnWidth);
            if (correction.Column1VSpace > 0)
                sb.AppendLine($"\\vspace{{{correction.Column1VSpace}\\baselineskip}}");
            sb.AppendLine("\\end{minipage}%");
            sb.AppendLine("\\hfill");
            sb.AppendLine($"\\begin{{minipage}}[t]{{{MinipageWidth}}}\\raggedright");
            AppendColumn(sb, page.Column2, columnWidth);
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
        AppendChapterHeading(sb, chapterTitle);

        // Grouped, so \raggedright does not leak into the chapters that follow.
        sb.AppendLine("\\begingroup\\raggedright");

        foreach (Song song in songs)
        {
            // titlesec's spacing is zeroed (see AppendChapterHeading), so the space before the title is added
            // here; \addvspace merges with the chapter heading's own space and vanishes at a page top.
            sb.AppendLine($"\\addvspace{{{SongLayout.TitleSpacingLines}\\baselineskip}}");

            // LaTeX decides where pages break here, so SB can't plan around it as it does in multi-column
            // mode. What it can do is forbid a break between the song's header and its first paragraph
            // (`\\*` is a line break that doesn't allow a page break after it), so a title is never left
            // alone at the bottom of a page. The kept-together part is capped, so a very long first
            // paragraph can't produce a page that doesn't fit.
            int firstParagraph = song.Text.TakeWhile(line => !string.IsNullOrWhiteSpace(line)).Count();
            int lyricLinesKept = Math.Clamp(firstParagraph, 1, MaxLyricLinesKeptWithHeader);

            List<string> lines = HeaderLatex(song).ToList();
            lines.AddRange(song.Text.Select(LyricLatex));
            int keptTogether = lines.Count - song.Text.Count + Math.Min(lyricLinesKept, song.Text.Count);

            AppendLines(sb, lines, keptTogether);
        }

        sb.AppendLine("\\par\\endgroup");
        return sb.ToString();
    }

    /// <summary>One lyric line. A blank one is an <c>\mbox{}</c>: under <c>\raggedright</c>, <c>\\</c> ends the
    /// paragraph, so a <c>\\</c> with nothing in front of it fails with "There's no line here to end".</summary>
    private static string LyricLatex(string line) =>
        string.IsNullOrWhiteSpace(line) ? "\\mbox{}" : LaTeXText.Escape(line);

    /// <summary>The header's lines, without their line-break terminators: <c>N. Title</c>, then the
    /// <c>Mel:</c>/<c>Text:</c> lines when present (FR-1.12.7, FR-1.12.11–15). <c>\paragraph*</c> is a
    /// run-in heading, so the <c>\mbox{}</c> and the line break after it are what put the title on a line
    /// of its own instead of in front of the first lyric line.</summary>
    private static IEnumerable<string> HeaderLatex(Song song)
    {
        yield return $"\\paragraph*{{{song.Number}. {LaTeXText.Escape(song.Title)}}}\\mbox{{}}";
        if (!string.IsNullOrWhiteSpace(song.Melody))
            yield return $"Mel: {LaTeXText.Escape(song.Melody)}";
        if (!string.IsNullOrWhiteSpace(song.Writer))
            yield return $"Text: {LaTeXText.Escape(song.Writer)}";
    }

    /// <summary>Writes the lines separated by line breaks. The first <paramref name="nonBreaking"/> lines
    /// are joined with <c>\\*</c> (no page break allowed after the line break); the last line gets no
    /// terminator at all.</summary>
    private static void AppendLines(StringBuilder sb, IReadOnlyList<string> lines, int nonBreaking)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string terminator = i == lines.Count - 1 ? "" : i < nonBreaking - 1 ? "\\\\*" : "\\\\";
            sb.AppendLine(lines[i] + terminator);
        }
    }

    /// <summary>The chapter heading, preceded by the zeroing of titlesec's space before a song title. The
    /// template already does that, but a document created from an earlier template keeps its old main
    /// <c>.tex</c>, where the space would be added a second time on top of the one emitted here.</summary>
    private static void AppendChapterHeading(StringBuilder sb, string chapterTitle)
    {
        sb.AppendLine("\\titlespacing*{\\paragraph}{0pt}{0pt}{0pt}");
        sb.AppendLine($"\\chapter*{{{LaTeXText.Escape(chapterTitle)}}}");
    }

    /// <summary>Writes a column's fragments. The line of space before a title (<see cref="SongLayout.TitleSpacingLines"/>)
    /// is an explicit <c>\vspace</c> rather than a <c>\titlespacing</c> in the template: titlesec does not
    /// apply it reliably inside a minipage, which left some songs directly after the previous one. The first
    /// fragment of a column gets none, since a column's first line sits at the top.</summary>
    private static void AppendColumn(StringBuilder sb, IReadOnlyList<SongFragment> fragments, int columnWidth)
    {
        for (int i = 0; i < fragments.Count; i++)
        {
            if (i > 0 && fragments[i].IncludesTitle)
                sb.AppendLine($"\\vspace{{{SongLayout.TitleSpacingLines}\\baselineskip}}");
            AppendFragment(sb, fragments[i], columnWidth);
        }
    }

    /// <summary>The planned lines of space before a column's first title that <see cref="AppendColumn"/> leaves out.</summary>
    private static int LeadingSpaceLines(IReadOnlyList<SongFragment> fragments) =>
        fragments.Count > 0 && fragments[0].IncludesTitle ? SongLayout.TitleSpacingLines : 0;

    private static void AppendFragment(StringBuilder sb, SongFragment fragment, int columnWidth)
    {
        var lines = new List<string>();

        if (fragment.ContinuesFromPreviousPage)
            lines.Add("\\textit{(forts.)}");

        if (fragment.IncludesTitle)
            lines.AddRange(HeaderLatex(fragment.Song));

        foreach (string line in fragment.Lines)
        {
            foreach (string wrapped in LineWrapper.Wrap(line, columnWidth))
                lines.Add(LyricLatex(wrapped));
        }

        if (fragment.ContinuesOnNextPage)
            lines.Add("\\textit{(forts.)}");

        AppendLines(sb, lines, nonBreaking: 0);

        // Ends the paragraph, so the next fragment (or a \vspace) starts in vertical mode.
        if (lines.Count > 0)
            sb.AppendLine("\\par");
    }
}
