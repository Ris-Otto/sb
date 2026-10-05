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
    private const int MultiColumnMaxWidth = 50;

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

            var orderedSongs = SongOrderer.Order(chapter.Songs, request.Ordering, pageMetrics, request.MaxPermutations);
            var placedSongs = orderedSongs
                .Select((song, index) => new PlacedSong(song, index + 1))
                .ToList();
            allPlacedSongs.AddRange(placedSongs);

            var songBlocks = placedSongs.Select(placed => RenderSongBlock(placed, maxWidth)).ToList();
            string songContent;
            if (request.Format == LeafletFormat.MultiColumn)
            {
                var layout = ColumnBalancer.Fill(songBlocks, pageMetrics, currentPageNumber);
                songContent = ColumnBalancer.Render(layout);
                currentPageNumber += layout.PagesConsumed;
            }
            else
            {
                songContent = RenderSingleColumn(songBlocks);
            }

            var chapterHeading = LatexEscaper.Escape(chapter.Title ?? chapter.Name);
            chapterFiles.Add(new LeafletChapterFile(chapter.Name, $"\\chapter{{{chapterHeading}}}\n{songContent}"));
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
    /// internal punctuation. A blank input line (e.g. a stanza separator) is rendered as a
    /// completely empty visual line rather than dropped (2026-09-26 Clarification): in LaTeX, a
    /// line break (`\\`) immediately followed by only one blank source line doesn't produce a
    /// visible empty line (there's no paragraph gap to see yet) — it takes *two* blank lines
    /// after the `\\` to actually show one empty line, so each blank input line contributes two
    /// blank output lines.
    /// </summary>
    private static SongBlock RenderSongBlock(PlacedSong placedSong, int maxWidth)
    {
        var song = placedSong.Song;
        var lines = new List<string>
        {
            $"\\paragraph*{{{song.SingingOrderNumber}. {LatexEscaper.Escape(song.Title)}\\\\}}"
        };

        if (!string.IsNullOrWhiteSpace(song.Melody))
            lines.Add($"\\textit{{Mel: {LatexEscaper.Escape(song.Melody)}}}\\\\");

        foreach (var rawLine in song.Text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                lines.Add("");
                lines.Add("");
                continue;
            }

            var escaped = LatexEscaper.Escape(line);
            foreach (var wrapped in LineWrapper.Wrap(escaped, maxWidth))
                lines.Add($"{wrapped}\\\\");
        }

        return new SongBlock(lines);
    }

    private static string RenderSingleColumn(IReadOnlyList<SongBlock> blocks) =>
        string.Join("\n\n", blocks.Select(block => string.Join("\n", block.Lines)));
}
