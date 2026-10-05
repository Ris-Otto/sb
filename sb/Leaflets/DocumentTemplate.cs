using System.Text;
using System.Text.RegularExpressions;
using sb.Formatting;

namespace sb.Leaflets;

/// <summary>
/// Reads the fixed master-document skeleton at sb/template-main.tex (never modifies it on disk)
/// and fills in its title/subtitle placeholders and chapter-content insertion point (research.md
/// §2, §9). As of 2026-09-26, a document's own rendered `&lt;name&gt;.tex` is the sole persisted
/// file for it — there is no separate metadata file — so <see cref="ParseTitleAndSubtitle"/>
/// recovers a document's raw title/subtitle by reversing the same substitution.
/// </summary>
public sealed class DocumentTemplate
{
    private const string TitleToken = "{title}";
    private const string SubtitleToken = "{subtitle}";
    private const string ContentInsertionMarker = "%Chapters come here";

    private static readonly Regex TitleLinePattern = new(@"\\Huge\\bf (.*?)\\\\", RegexOptions.Singleline);
    private static readonly Regex SubtitleLinePattern = new(@"\\large (.*?)\r?\n", RegexOptions.Singleline);

    public string RawTemplate { get; }

    private DocumentTemplate(string rawTemplate)
    {
        RawTemplate = rawTemplate;
    }

    public static DocumentTemplate Load(string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException(
                $"Cannot build a leaflet: template file not found at '{templatePath}'.");
        }

        var raw = File.ReadAllText(templatePath, Encoding.UTF8);

        if (!raw.Contains(TitleToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{templatePath}' is missing its expected title placeholder ('{TitleToken}').");
        }

        if (!raw.Contains(SubtitleToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{templatePath}' is missing its expected subtitle placeholder ('{SubtitleToken}').");
        }

        if (!raw.Contains(ContentInsertionMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{templatePath}' is missing its expected content insertion marker ('{ContentInsertionMarker}').");
        }

        return new DocumentTemplate(raw);
    }

    /// <summary>
    /// Renders the complete document. <paramref name="escapedTitle"/>, <paramref name="escapedSubtitle"/>,
    /// and <paramref name="content"/> must already have any user-supplied text LaTeX-escaped by
    /// the caller (see <see cref="LatexEscaper"/>) — this method performs no escaping.
    /// </summary>
    public string Render(string escapedTitle, string? escapedSubtitle, string content)
    {
        var withTitle = RawTemplate.Replace(TitleToken, escapedTitle);
        var withSubtitle = withTitle.Replace(SubtitleToken, escapedSubtitle ?? "");
        return withSubtitle.Replace(ContentInsertionMarker, $"{ContentInsertionMarker}\n{content}");
    }

    /// <summary>
    /// Recovers a document's raw (unescaped) title/subtitle from its already-rendered
    /// `&lt;name&gt;.tex` content, by locating the same fixed wrapper text <see cref="Render"/> substitutes into and
    /// reversing <see cref="LatexEscaper.Escape"/>. Falls back to "(untitled)"/<c>null</c> if the
    /// expected markers aren't found (e.g. a hand-corrupted file — surfaced elsewhere via
    /// FR-023-style mismatch handling, not by throwing here).
    /// </summary>
    public static (string Title, string? Subtitle) ParseTitleAndSubtitle(string renderedMainTex)
    {
        var titleMatch = TitleLinePattern.Match(renderedMainTex);
        var title = titleMatch.Success ? LatexEscaper.Unescape(titleMatch.Groups[1].Value) : "(untitled)";

        var subtitleMatch = SubtitleLinePattern.Match(renderedMainTex);
        var rawSubtitle = subtitleMatch.Success ? subtitleMatch.Groups[1].Value : "";
        var subtitle = string.IsNullOrEmpty(rawSubtitle) ? null : LatexEscaper.Unescape(rawSubtitle);

        return (title, subtitle);
    }
}
