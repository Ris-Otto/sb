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

        Assert.That(rendered, Does.Contain("\\paragraph*{Lille Albin}"));
    }

    [Test]
    public void RenderMultiColumn_PreservesExactTextLinesInOrder_NoReflow()
    {
        var song = MakeSong("A", "Första raden", "", "Tredje raden");
        var fragment = SongFragment.Whole(song, ColumnWidth);
        var page = new Page(1, [fragment], [], fragment.Height, 0);

        string rendered = ChapterEmitter.RenderMultiColumn("Chapter", [page], ColumnWidth);

        Assert.That(rendered, Does.Contain("Första raden\\\\"));
        Assert.That(rendered, Does.Contain("Tredje raden\\\\"));
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
        Assert.That(CountOccurrences(rendered, "\\\\"), Is.EqualTo(4));
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

        Assert.That(rendered, Does.Contain("continued"));
        Assert.That(rendered, Does.Contain("continued from previous page"));
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

        Assert.That(rendered, Does.Not.Contain("continued"));
    }

    [Test]
    public void RenderSingleColumn_EmitsSongsWithoutMinipages()
    {
        var songs = new[] { MakeSong("A", "rad 1"), MakeSong("B", "rad 1") };

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", songs);

        Assert.That(rendered, Does.Not.Contain("minipage"));
        Assert.That(rendered, Does.Contain("\\paragraph*{A}"));
        Assert.That(rendered, Does.Contain("\\paragraph*{B}"));
    }

    [Test]
    public void RenderSingleColumn_PreservesExactTextLines()
    {
        var song = MakeSong("A", "Första raden", "Andra raden");

        string rendered = ChapterEmitter.RenderSingleColumn("Chapter", [song]);

        Assert.That(rendered, Does.Contain("Första raden\\\\"));
        Assert.That(rendered, Does.Contain("Andra raden\\\\"));
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
}
