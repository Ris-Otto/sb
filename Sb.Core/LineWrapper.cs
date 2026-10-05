using System.Text;

namespace Sb.Core;

/// <summary>
/// Approximates line wrapping for layout planning (FR-1.2.8, FR-1.2.9): the user's own line breaks
/// are trusted as-is — a line only becomes more than one output line when it's too wide for the
/// column. Column width is a plain character-count budget; real typographic width is a Milestone 6
/// (LaTeX generation) concern, not this domain-level approximation. Wrapping is word-based so no
/// word is ever split, dropped, or truncated.
/// </summary>
public static class LineWrapper
{
    public static IReadOnlyList<string> Wrap(string line, int columnWidth)
    {
        if (line.Length == 0)
            return [line];

        string[] words = line.Split(' ');
        var outputLines = new List<string>();
        var current = new StringBuilder();
        bool currentHasWord = false;

        foreach (string word in words)
        {
            if (!currentHasWord)
            {
                current.Append(word);
                currentHasWord = true;
            }
            else if (current.Length + 1 + word.Length <= columnWidth)
            {
                current.Append(' ').Append(word);
            }
            else
            {
                outputLines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        outputLines.Add(current.ToString());
        return outputLines;
    }

    public static int CountOutputLines(string line, int columnWidth) => Wrap(line, columnWidth).Count;

    public static int CountOutputLines(IEnumerable<string> lines, int columnWidth) =>
        lines.Sum(line => CountOutputLines(line, columnWidth));
}
