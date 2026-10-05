using System.Text;

namespace sb.Formatting;

/// <summary>
/// Word-wraps text that would otherwise overflow a column's width, per FR-004 — breaking across
/// multiple lines rather than truncating or overflowing.
/// </summary>
public static class LineWrapper
{
    public static IReadOnlyList<string> Wrap(string text, int maxWidth)
    {
        if (text.Length <= maxWidth)
            return new[] { text };

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in text.Split(' '))
        {
            var candidateLength = current.Length == 0 ? word.Length : current.Length + 1 + word.Length;
            if (candidateLength > maxWidth && current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
                current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }
}
