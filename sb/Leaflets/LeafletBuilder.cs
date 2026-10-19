using sb.Formatting;
using sb.Ordering;

namespace sb.Leaflets;

/// <summary>
/// Turns a <see cref="LeafletRequest"/> into a complete, self-contained LaTeX <see cref="Leaflet"/>
/// (FR-001–FR-012, contracts/leaflet-builder-api.md).
///
/// As of 2026-09-26 (`sb/specification.md` §2.5.1/§4), each chapter is rendered independently
/// into its own `chapter.tex` fragment (a `\chapter{...}` heading followed by that chapter's own
/// songs, ordered and column-balanced among themselves only — never paired across a chapter
/// boundary, since `\chapter` forces a break in the physical layout regardless), and the main
/// document's content region becomes a sequence of `\input{Chapters/&lt;name&gt;/chapter.tex}`
/// lines instead of directly embedding song content. A chapter with zero songs is skipped
/// entirely (no heading, no `\input` line).
/// </summary>
public static class LeafletBuilder
{
    private const int SingleColumnMaxWidth = 100;
    private const int MultiColumnMaxWidth = 41;

    /// <summary>Every chapter file zeroes the title spacing itself, so a main `.tex` from an older
    /// template (which still carries its own `\titlespacing`) can't stack a second gap on top of
    /// the explicit one (FR-027). `\raggedright` is deliberately NOT set here: at chapter level it
    /// leaks into the paragraphs that hold the side-by-side minipage rows and makes the compiled
    /// pages overflow (verified against a real compile) — minipages and the single-column body
    /// set it themselves instead, so a wrapped line is never stretched (FR-004).</summary>
    private const string ChapterPreamble = "\\titlespacing*{\\paragraph}{0pt}{0pt}{0pt}\n";

    public static Leaflet Build(LeafletRequest request, string? templatePath = null)
    {
        var resolvedTemplatePath = templatePath ?? Path.Combine(AppContext.BaseDirectory, "template-main.tex");
        var template = DocumentTemplate.Load(resolvedTemplatePath);
        var pageMetrics = PageMetricsReader.Read(template.RawTemplate, resolvedTemplatePath);

        var maxWidth = request.Format == LeafletFormat.MultiColumn ? MultiColumnMaxWidth : SingleColumnMaxWidth;

        var allPlacedSongs = new List<PlacedSong>();
        var chapterFiles = new List<LeafletChapterFile>();
        var inputLines = new List<string>();
        var currentPageNumber = 1;

        foreach (var chapter in request.Chapters)
        {
            if (chapter.Songs.Count == 0)
                continue;

            var orderedSongs = SongOrderer.Order(chapter.Songs, request.Ordering, pageMetrics);
            var placedSongs = orderedSongs
                .Select((song, index) => new PlacedSong(song, index + 1))
                .ToList();
            allPlacedSongs.AddRange(placedSongs);

            var songBlocks = placedSongs.Select(placed => RenderSongBlock(placed, maxWidth)).ToList();
            string songContent;
            if (request.Format == LeafletFormat.MultiColumn)
            {
                var layout = ColumnBalancer.Fill(songBlocks, pageMetrics, currentPageNumber, pageMetrics.ChapterHeadingLines);
                songContent = ColumnBalancer.Render(layout);
                currentPageNumber += layout.PagesConsumed;
            }
            else
            {
                songContent = RenderSingleColumn(songBlocks);
            }

            var chapterHeading = LatexEscaper.Escape(chapter.Title ?? chapter.Name);
            chapterFiles.Add(new LeafletChapterFile(chapter.Name, $"{ChapterPreamble}\\chapter*{{{chapterHeading}}}\n{songContent}"));
            inputLines.Add($"\\input{{Chapters/{chapter.Name}/chapter.tex}}");
        }

        var escapedTitle = LatexEscaper.Escape(request.Title);
        var escapedSubtitle = request.Subtitle is null ? null : LatexEscaper.Escape(request.Subtitle);
        var document = template.Render(escapedTitle, escapedSubtitle, string.Join("\n", inputLines));

        return new Leaflet(request.Format, allPlacedSongs, document, chapterFiles);
    }

    /// <summary>
    /// Trusts the user's own line breaks (FR-003, 2026-09-26 Clarification): each of the song's
    /// input lines becomes one output line as-is, wrapped further only if it's too wide for
    /// <paramref name="maxWidth"/> (FR-004) — the system never merges lines or splits one at
    /// internal punctuation. A blank input line between text lines (e.g. a stanza separator) is
    /// rendered as one completely empty visual line rather than dropped (2026-09-26
    /// Clarification), emitted as `\mbox{}\\` (2026-10-19): under `\raggedright` a bare `\\`
    /// ends the paragraph and fails with "There's no line here to end". Each blank input line is
    /// a single entry, so one entry always equals one rendered line for
    /// <see cref="SongBlock.HeightInLines"/> and column packing. Blank lines at the start or end
    /// of the song's text are ignored.
    /// </summary>
    private static SongBlock RenderSongBlock(PlacedSong placedSong, int maxWidth)
    {
        var song = placedSong.Song;
        var lines = new List<string>
        {
            // The line break after the run-in title must sit outside the title argument: titlesec
            // typesets it in an `\sbox` (LR mode), where `\\` errors under `\raggedright` ("Not
            // allowed in LR mode"). `\mbox{}` starts the paragraph so the `\\` has a line to end.
            $"\\paragraph*{{{song.SingingOrderNumber}. {LatexEscaper.Escape(song.Title)}}}\\mbox{{}}\\\\"
        };

        if (!string.IsNullOrWhiteSpace(song.Melody))
            lines.Add($"\\textit{{Mel: {LatexEscaper.Escape(song.Melody)}}}\\\\");

        // Leading/trailing blank lines are ignored (FR-003, 2026-10-19): they would add rendered
        // lines the height accounting doesn't expect.
        var text = song.Text.Replace("\r\n", "\n").Trim();
        var inputLines = text.Length == 0 ? Array.Empty<string>() : text.Split('\n');

        foreach (var rawLine in inputLines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                lines.Add(SongBlock.BlankLine);
                continue;
            }

            var escaped = LatexEscaper.Escape(line);
            foreach (var wrapped in LineWrapper.Wrap(escaped, maxWidth))
                lines.Add($"{wrapped}\\\\");
        }

        return new SongBlock(lines);
    }

    private static string RenderSingleColumn(IReadOnlyList<SongBlock> blocks) =>
        $"\\begingroup\\raggedright\n{JoinSingleColumn(blocks)}\n\\par\\endgroup";

    private static string JoinSingleColumn(IReadOnlyList<SongBlock> blocks) =>
        string.Join($"\n\n{SongBlock.BlankLine}\n", blocks.Select(block => string.Join("\n", block.Lines)));
}
