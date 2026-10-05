using System.Text;

namespace Sb.Core.LaTeX;

/// <summary>
/// Escapes LaTeX's reserved characters in user-supplied text (titles, lyrics, writer/melody names,
/// ...) before embedding it in generated source. This is what makes NFR-2.5.3's fidelity guarantee
/// actually hold at the LaTeX layer: an unescaped "_" or "&" wouldn't fail silently, it would either
/// break compilation or render as something other than the literal character the user typed.
/// </summary>
public static class LaTeXText
{
    public static string Escape(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            result.Append(c switch
            {
                '\\' => "\\textbackslash{}",
                '{' => "\\{",
                '}' => "\\}",
                '#' => "\\#",
                '$' => "\\$",
                '%' => "\\%",
                '&' => "\\&",
                '_' => "\\_",
                '^' => "\\textasciicircum{}",
                '~' => "\\textasciitilde{}",
                _ => c.ToString()
            });
        }
        return result.ToString();
    }
}
