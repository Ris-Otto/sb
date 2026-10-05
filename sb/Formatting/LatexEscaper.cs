using System.Text;
using System.Text.RegularExpressions;

namespace sb.Formatting;

/// <summary>Escapes LaTeX's reserved characters (research.md §6, FR-010).</summary>
public static class LatexEscaper
{
    private static readonly (string Escaped, string Raw)[] Mappings =
    {
        ("\\textbackslash{}", "\\"),
        ("\\{", "{"),
        ("\\}", "}"),
        ("\\%", "%"),
        ("\\&", "&"),
        ("\\#", "#"),
        ("\\_", "_"),
        ("\\textasciitilde{}", "~"),
        ("\\textasciicircum{}", "^")
    };

    private static readonly Regex UnescapePattern = new(
        string.Join("|", Mappings.Select(m => Regex.Escape(m.Escaped))));

    public static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '\\' => "\\textbackslash{}",
                '{' => "\\{",
                '}' => "\\}",
                '%' => "\\%",
                '&' => "\\&",
                '#' => "\\#",
                '_' => "\\_",
                '~' => "\\textasciitilde{}",
                '^' => "\\textasciicircum{}",
                _ => c.ToString()
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reverses <see cref="Escape"/> exactly — used to recover a document's raw title/subtitle
    /// from its rendered `&lt;name&gt;.tex` (research.md §9's 2026-09-26 update: it is the only
    /// per-document file, so title/subtitle round-trip through their escaped display text).
    /// </summary>
    public static string Unescape(string text) =>
        UnescapePattern.Replace(text, match => Mappings.First(m => m.Escaped == match.Value).Raw);
}
