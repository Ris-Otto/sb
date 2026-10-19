using NUnit.Framework;
using Sb.Core;
using Sb.Core.LaTeX;

namespace Sb.Tests.LaTeX;

[TestFixture]
public class ChapterEmitterTests
{
    private const int ColumnWidth = 1000; // generous enough that no line ever wraps

    private static Song MakeSong(string title, params string[] text) => Song.Create(title, text, 1).Value;

    [Test]
    public void RenderMultiColumn_EmitsUnnumberedChapterHeading()
    {
        string rendered = ChapterEmitter.RenderMultiColumn("Studentvisor", [], ColumnWidth);

        Assert.That(rendered, Does.Contain("\\chapter*{Studentvisor}"));
        Assert.That(rendered, Does.Not.Contain("\\chapter{Studentvisor}"));
    }

    [Test]
    public void RenderMultiColumn_EscapesChapterTitle()
    {
        string rendered = ChapterEmitter.RenderMultiColumn("Rock & Roll", [], ColumnWidth);

        Assert.That(rendered, Does.Contain("\\chapter*{Rock \\& Roll}"));
    }

    [Test]
    public void RenderMultiColumn_BothColumnsRenderedAsMinipages()
    {
        var a = SongFragment.Whole(MakeSong("A", "rad 1"), ColumnWidth);
        var b = SongFragment.Whole(MakeSong("B", "rad 1"), ColumnWidth);
        var page = new Page(1, [a], [b], a.Height, b.Height);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(CountOccurrences(rendered, "\\begin{minipage}"), Is.EqualTo(2));
        Assert.That(CountOccurrences(rendered, "\\end{minipage}"), Is.EqualTo(2));
    }

    [Test]
    public void RenderMultiColumn_SongTitleEmittedAsParagraphCommand()
    {
        var fragment = SongFragment.Whole(MakeSong("Lille Albin", "rad 1"), ColumnWidth);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Contain("\\paragraph*{1. Lille Albin}"));
    }

    [Test]
    public void RenderMultiColumn_PreservesExactTextLinesInOrder_NoReflow()
    {
        var song = MakeSong("A", "Första raden", "", "Tredje raden");
        var fragment = SongFragment.Whole(song, ColumnWidth);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Contain("Första raden\\\\"));
        // The last line has no line break after it (it would add an empty line, A-12); the paragraph just ends.
        Assert.That(rendered, Does.Contain("Tredje raden" + Environment.NewLine + "\\par"));
        Assert.That(rendered, Does.Not.Contain("Tredje raden\\\\"));
        int firstIndex = rendered.IndexOf("Första raden", StringComparison.Ordinal);
        int thirdIndex = rendered.IndexOf("Tredje raden", StringComparison.Ordinal);
        Assert.That(firstIndex, Is.LessThan(thirdIndex));
    }

    [Test]
    public void RenderMultiColumn_WrapsLineTooWideForColumn()
    {
        var song = MakeSong("A", "Där byggdes en maskot i Norden");
        var fragment = SongFragment.Whole(song, columnWidth: 10);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], columnWidth: 10);

        // LineWrapper would wrap this into 4 sub-lines at columnWidth 10 (verified in LineWrapperTests).
        // plus the title line, which makes 5 lines; a line break goes between lines, none after the last.
        Assert.That(CountOccurrences(rendered, "\\\\"), Is.EqualTo(4));
    }

    [Test]
    public void RenderMultiColumn_PrefixesTitleWithSongNumber()
    {
        Song song = Song.Create("Titel", ["rad"], 12).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Contain("\\paragraph*{12. Titel}"));
    }

    [Test]
    public void RenderMultiColumn_PutsTitleMelodyAndWriterOnSeparateLinesAboveLyrics()
    {
        Song song = Song.Create("Titel", ["Första raden"], 1, writer: "Skribent", melody: "Tonsättare").Value!;
        var fragment = SongFragment.Whole(song, ColumnWidth);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        string expected = string.Join(
            Environment.NewLine,
            "\\paragraph*{1. Titel}\\mbox{}\\\\", "Mel: Tonsättare\\\\", "Text: Skribent\\\\", "Första raden", "\\par");
        Assert.That(rendered, Does.Contain(expected));
        Assert.That(fragment.Height, Is.EqualTo(SongLayout.HeaderLines(song) + 1));
    }

    [Test]
    public void RenderMultiColumn_MinipagesAreTopAligned()
    {
        // [t] makes both columns start at the same line (FR-1.2.7); the vspace correction (tested
        // below) is what then makes them also end at the same line.
        var a = SongFragment.Whole(MakeSong("A", "rad 1"), ColumnWidth);
        var b = SongFragment.Whole(MakeSong("B", "rad 1"), ColumnWidth);
        var page = new Page(1, [a], [b], a.Height, b.Height);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(CountOccurrences(rendered, "\\begin{minipage}[t]"), Is.EqualTo(2));
    }

    [Test]
    public void RenderMultiColumn_AppliesVSpaceToShorterColumn()
    {
        var a = SongFragment.Whole(MakeSong("A", "rad 1", "rad 2"), ColumnWidth); // height 3
        var b = SongFragment.Whole(MakeSong("B", "rad 1"), ColumnWidth); // height 2
        var page = new Page(1, [a], [b], Column1Lines: 3, Column2Lines: 2);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Contain("\\vspace{1\\baselineskip}"));
    }

    [Test]
    public void RenderMultiColumn_EqualColumns_NoVSpaceEmitted()
    {
        var a = SongFragment.Whole(MakeSong("A", "rad 1"), ColumnWidth);
        var b = SongFragment.Whole(MakeSong("B", "rad 1"), ColumnWidth);
        var page = new Page(1, [a], [b], a.Height, b.Height);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Not.Contain("\\vspace"));
    }

    [Test]
    public void RenderMultiColumn_PageBoundarySplit_RendersBothContinuationMarkers()
    {
        var endingFragment = SongFragment.Whole(MakeSong("A", "rad 1"), ColumnWidth) with { ContinuesOnNextPage = true };
        var resumingFragment = new SongFragment(
            MakeSong("B", "rad 2"), IncludesTitle: false, ["rad 2"], Height: 1, ContinuesFromPreviousPage: true);
        var page1 = new Page(1, [], [endingFragment], 0, endingFragment.Height);
        var page2 = new Page(2, [resumingFragment], [], resumingFragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page1, page2], ColumnWidth);

        Assert.That(CountOccurrences(rendered, "(forts.)"), Is.EqualTo(2));
    }

    [Test]
    public void RenderMultiColumn_ColumnBoundarySplit_RendersNoContinuationMarkers()
    {
        var a = SongFragment.Whole(MakeSong("A", "rad 1", "rad 2"), ColumnWidth);
        // A plain column 1→2 split: neither flag set (verified by ColumnFiller itself; constructed
        // directly here to isolate the rendering behavior).
        var column1Part = a with { ContinuesOnNextPage = false };
        var column2Part = new SongFragment(a.Song, IncludesTitle: false, ["rad 2"], Height: 1);
        var page = new Page(1, [column1Part], [column2Part], column1Part.Height, column2Part.Height);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Not.Contain("(forts.)"));
    }

    [Test]
    public void RenderSingleColumn_EmitsSongsWithoutMinipages()
    {
        var songs = new[] { MakeSong("A", "rad 1"), MakeSong("B", "rad 1") };

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", songs);

        Assert.That(rendered, Does.Not.Contain("minipage"));
        Assert.That(rendered, Does.Contain("\\paragraph*{1. A}"));
        Assert.That(rendered, Does.Contain("\\paragraph*{1. B}"));
    }

    [Test]
    public void RenderSingleColumn_PreservesExactTextLines()
    {
        var song = MakeSong("A", "Första raden", "Andra raden");

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", [song]);

        Assert.That(rendered, Does.Contain("Första raden\\\\"));
        Assert.That(rendered, Does.Contain("Andra raden" + Environment.NewLine));
        Assert.That(rendered, Does.Not.Contain("Andra raden\\\\"), "no line break after the last line (A-12)");
    }

    private static int CountOccurrences(string text, string substring)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    [Test]
    public void RenderSingleColumn_KeepsHeaderAndFirstParagraphTogether()
    {
        Song song = Song.Create("T", ["a", "b", "", "c"], 1, writer: "W").Value;

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", [song]);

        // `\\*` forbids a page break after the line; the last kept line and everything after allow one.
        string expected = string.Join(
            Environment.NewLine,
            "\\paragraph*{1. T}\\mbox{}\\\\*", "Text: W\\\\*", "a\\\\*", "b\\\\", "\\mbox{}\\\\", "c");
        Assert.That(rendered, Does.Contain(expected));
    }

    [Test]
    public void RenderSingleColumn_FirstParagraphLongerThanTheCap_OnlyKeepsTheCappedPartTogether()
    {
        Song song = Song.Create("T", Enumerable.Range(1, 15).Select(i => $"line {i}").ToArray(), 1).Value;

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", [song]);

        // Title + the first 10 lyric lines are bound together: 10 non-breaking line ends (title + 9 lines).
        Assert.That(CountOccurrences(rendered, "\\\\*"), Is.EqualTo(10));
        Assert.That(rendered, Does.Contain("line 10\\\\" + Environment.NewLine + "line 11\\\\"));
    }

    [Test]
    public void RenderSingleColumn_BlankLinesAroundTheLyrics_AreDropped()
    {
        Song song = Song.Create("T", ["", "a", "b", "", " "], 1).Value;

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", [song]);

        Assert.That(rendered, Does.Contain("\\paragraph*{1. T}\\mbox{}\\\\*" + Environment.NewLine + "a\\\\*" + Environment.NewLine + "b" + Environment.NewLine));
    }

    [Test]
    public void RenderSingleColumn_PutsALineOfSpaceBeforeEachTitle()
    {
        Song song = Song.Create("T", ["a"], 1).Value;

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", [song]);

        Assert.That(rendered, Does.Contain("\\addvspace{1\\baselineskip}" + Environment.NewLine + "\\paragraph*{1. T}"));
    }

    [Test]
    public void RenderMultiColumn_PutsALineOfSpaceBeforeEveryTitleButTheFirstInAColumn()
    {
        var a = SongFragment.Whole(MakeSong("A", "rad 1"), ColumnWidth);
        var b = SongFragment.Whole(MakeSong("B", "rad 1"), ColumnWidth);
        var page = new Page(1, [a, b], [], a.Height + b.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(CountOccurrences(rendered, "\\vspace{1\\baselineskip}" + Environment.NewLine + "\\paragraph*"), Is.EqualTo(1));
        Assert.That(rendered, Does.Contain("\\begin{minipage}[t]{0.48\\textwidth}\\raggedright" + Environment.NewLine + "\\paragraph*{"));
    }

    [Test]
    public void RenderMultiColumn_StillUsesPlainLineBreaks_NoNonBreakingOnes()
    {
        var fragment = SongFragment.Whole(MakeSong("A", "rad 1", "rad 2"), ColumnWidth);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Not.Contain("\\\\*"));
    }
}
