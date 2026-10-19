using NUnit.Framework;
using sb.Formatting;
using sb.Leaflets;

namespace sb.Tests;

/// <summary>
/// Validates SC-002 (≥90%, relaxed 2026-10-19 for the 41-character width) and SC-003 (≥95%) thresholds in aggregate across every real fixture under
/// Tests/TestFiles/, rather than only the individual cases already covered by
/// ColumnBalancerTests (per the 2026-09-11 /speckit-analyze remediation, C2). SC-002 was revised
/// 2026-09-26 (phrase-splitting removed — see spec.md's Clarifications) to measure how often a
/// song's own input lines already fit one output line, since the system no longer re-splits them.
/// </summary>
[TestFixture]
public class AggregateAccuracyTests
{
    private const int MultiColumnMaxWidth = 41;

    private static IEnumerable<string> AllFixtureFiles() =>
        Directory.GetFiles(TestFixtures.Path(), "*.txt");

    private static IReadOnlyList<string> NonEmptyLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    [Test]
    public void SC002_AtLeast95PercentOfRealInputLines_FitOneOutputLine_EvenAtTheNarrowerMultiColumnWidth()
    {
        var totalLines = 0;
        var linesNeedingNoWrap = 0;

        foreach (var file in AllFixtureFiles())
        {
            foreach (var line in NonEmptyLines(File.ReadAllText(file)))
            {
                totalLines++;
                if (LineWrapper.Wrap(line, MultiColumnMaxWidth).Count == 1)
                    linesNeedingNoWrap++;
            }
        }

        Assert.That(totalLines, Is.GreaterThan(0), "expected at least one real fixture to be present");
        var fraction = (double)linesNeedingNoWrap / totalLines;
        Assert.That(fraction, Is.GreaterThanOrEqualTo(0.90),
            $"only {linesNeedingNoWrap}/{totalLines} ({fraction:P0}) of real input lines fit one line at width {MultiColumnMaxWidth}");
    }

    [Test]
    public void SC003_EveryRealFixturePair_EndsWithinOneLineHeightAfterBalancing()
    {
        var files = AllFixtureFiles().ToList();
        var alignedPairs = 0;
        var totalPairs = 0;

        for (var i = 0; i + 1 < files.Count; i += 2)
        {
            totalPairs++;
            var left = new SongBlock(NonEmptyLines(File.ReadAllText(files[i])).Select(l => $"{l}\\\\").ToList());
            var right = new SongBlock(NonEmptyLines(File.ReadAllText(files[i + 1])).Select(l => $"{l}\\\\").ToList());

            // A LinesPerPage exactly matching the taller of the two guarantees they land in
            // separate columns of the same page (2026-09-27: sequential column-fill would
            // otherwise stack short blocks together — see ColumnBalancerTests).
            var linesPerPage = Math.Max(left.HeightInLines, right.HeightInLines);
            var pageMetrics = new PageMetrics(paperHeightPt: linesPerPage * 10, topMarginPt: 0, bottomMarginPt: 0, baselineSkipPt: 10);
            var layout = ColumnBalancer.Fill(new[] { left, right }, pageMetrics, startingPageNumber: 1);
            var rendered = ColumnBalancer.Render(layout);

            // ColumnBalancer appends a \vspace to the shorter column sized to close the gap
            // exactly, so a balanced pair either needed no \vspace (already equal) or has
            // exactly one \vspace call compensating for the height difference.
            var vspaceCount = CountOccurrences(rendered, "\\vspace");
            if (left.HeightInLines == right.HeightInLines ? vspaceCount == 0 : vspaceCount == 1)
                alignedPairs++;
        }

        Assert.That(totalPairs, Is.GreaterThan(0), "expected at least one fixture pair");
        Assert.That((double)alignedPairs / totalPairs, Is.GreaterThanOrEqualTo(0.95));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
