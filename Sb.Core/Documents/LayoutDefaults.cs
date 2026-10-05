namespace Sb.Core.Documents;

/// <summary>
/// A first-pass estimate of how many characters/lines fit a column under template-main.tex's actual
/// geometry (A4, 0.4in side margins, 12pt/14.4pt font, two ~0.48\textwidth columns) — not a typeset
/// measurement, so it may need visual tuning once a real PDF is compiled (consistent with
/// LineWrapper's column-width being a planning approximation, not real typographic width). Shared
/// between <see cref="DocumentOperations"/> and <see cref="ChapterOperations"/> so both lay out
/// content against the same assumed page geometry.
/// </summary>
internal static class LayoutDefaults
{
    public const int ColumnWidth = 40;
    public const int LinesPerColumn = 50;
}
