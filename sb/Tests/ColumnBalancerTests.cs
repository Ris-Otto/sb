using NUnit.Framework;
using sb.Leaflets;

namespace sb.Tests;

[TestFixture]
public class ColumnBalancerTests
{
    private static PageMetrics SmallPageMetrics(int linesPerPage) =>
        new(paperHeightPt: linesPerPage * 10, topMarginPt: 0, bottomMarginPt: 0, baselineSkipPt: 10);

    private static SongBlock Block(int lineCount) =>
        new(Enumerable.Range(1, lineCount).Select(i => $"line {i}\\\\").ToList());

    [Test]
    public void Fill_UsesTheContractWidthsAndGutter_WhenAPageHasTwoColumns()
    {
        // linesPerPage=3: two 1-line blocks would both fit in the same column and never split —
        // use blocks whose combined size (1+3=4) forces the second into a fresh column.
        var layout = ColumnBalancer.Fill(new[] { Block(1), Block(3) }, SmallPageMetrics(3), startingPageNumber: 1);

        var result = ColumnBalancer.Render(layout);

        Assert.That(result, Does.Contain(".475\\textwidth"));
        Assert.That(result, Does.Contain(".45\\textwidth"));
        Assert.That(result, Does.Contain("\\hspace{0.075\\textwidth}"));
    }


    [Test]
    public void Fill_ReservesTheHeadingLines_OnTheFirstPageOnly()
    {
        // linesPerPage=10, 3 lines reserved => page 1's columns hold 7. An 8-line song no longer
        // fits page 1's left column whole, so it continues (7 + 1) into the right column; the
        // next page has the full 10 lines again.
        var layout = ColumnBalancer.Fill(new[] { Block(8), Block(10) }, SmallPageMetrics(10), startingPageNumber: 1, firstPageReservedLines: 3);

        Assert.That(layout.Pages, Has.Count.EqualTo(2));
        Assert.That(layout.Pages[0].Left.HeightInLines, Is.EqualTo(7));
        Assert.That(layout.Pages[0].Right!.HeightInLines, Is.EqualTo(1));
        Assert.That(layout.Pages[1].Left.HeightInLines, Is.EqualTo(10));
    }
    [Test]
    public void Fill_StacksMultipleShortSongsIntoTheSameColumn_UntilTheNextOneWouldNotFit()
    {
        var layout = ColumnBalancer.Fill(new[] { Block(4), Block(4), Block(4) }, SmallPageMetrics(10), startingPageNumber: 1);

        Assert.That(layout.Pages, Has.Count.EqualTo(1));
        var (left, right) = layout.Pages[0];
        // The first two 4-line songs (8 <= 10) stack together into the left column; the third
        // (would make 12 > 10) moves to the right column — proving a column is NOT capped at one
        // song (`/speckit-analyze` finding U1).
        Assert.That(left.HeightInLines, Is.EqualTo(8));
        Assert.That(right, Is.Not.Null);
        Assert.That(right!.HeightInLines, Is.EqualTo(4));
    }

    [Test]
    public void Fill_AppendsVspaceToTheShorterColumn_WhenLeftIsShorter()
    {
        var layout = ColumnBalancer.Fill(new[] { Block(1), Block(3) }, SmallPageMetrics(3), startingPageNumber: 1);

        var result = ColumnBalancer.Render(layout);
        var leftMinipage = ExtractMinipage(result, ".475\\textwidth");
        var rightMinipage = ExtractMinipage(result, ".45\\textwidth");

        Assert.That(leftMinipage, Does.Contain("\\vspace"));
        Assert.That(rightMinipage, Does.Not.Contain("\\vspace"));
    }

    [Test]
    public void Fill_AddsNoVspace_WhenColumnsAreAlreadyEqualHeight()
    {
        var layout = ColumnBalancer.Fill(new[] { Block(2), Block(2) }, SmallPageMetrics(3), startingPageNumber: 1);

        var result = ColumnBalancer.Render(layout);

        Assert.That(result, Does.Not.Contain("\\vspace"));
    }

    [Test]
    public void Fill_RendersASingleFullWidthColumn_WhenTheChaptersOnlySongFitsAlone()
    {
        var layout = ColumnBalancer.Fill(new[] { Block(1) }, SmallPageMetrics(10), startingPageNumber: 1);

        var result = ColumnBalancer.Render(layout);

        Assert.That(result, Does.Contain("line 1"));
        Assert.That(result, Does.Not.Contain(".475\\textwidth"));
    }

    [Test]
    public void Fill_ForcesAFreshPage_WhenALongSongWouldOtherwiseStartMidAnEvenPage()
    {
        var metrics = SmallPageMetrics(3);
        // A(3) fills page 1's left column exactly; B(3) fills its right column exactly (page 1
        // fully packed, but not yet "closed" until the next block arrives). C(1) then closes
        // page 1 (advancing to page 2, EVEN) and partially fills page 2's left column. D, a long
        // song, would next continue filling page 2's left column mid-way — since page 2 is EVEN,
        // it must instead be forced onto a fresh page (page 3, ODD) as that page's first song.
        var a = Block(3);
        var b = Block(3);
        var c = Block(1);
        var d = Block(7); // > linesPerPage(3) => chunked

        var layout = ColumnBalancer.Fill(new[] { a, b, c, d }, metrics, startingPageNumber: 1);

        Assert.That(layout.Pages, Has.Count.EqualTo(4));
        Assert.That(layout.Pages[0].Left.Lines, Is.EqualTo(a.Lines));
        Assert.That(layout.Pages[0].Right!.Lines, Is.EqualTo(b.Lines));
        // Page 2: only C — D was forced away instead of continuing here.
        Assert.That(layout.Pages[1].Left.HeightInLines, Is.EqualTo(1));
        Assert.That(layout.Pages[1].Right, Is.Null);
        // Page 3 (now the first ODD page after the forced break): D starts here, filling both
        // columns (3+3), then spilling one more line onto page 4.
        Assert.That(layout.Pages[2].Left.HeightInLines, Is.EqualTo(3));
        Assert.That(layout.Pages[2].Right!.HeightInLines, Is.EqualTo(3));
        Assert.That(layout.Pages[3].Left.HeightInLines, Is.EqualTo(1));
        Assert.That(layout.Pages[3].Right, Is.Null);
    }

    [Test]
    public void Fill_AllowsALongSongToStartMidPage_WhenThatPageIsOdd_NoForcedBreak()
    {
        var metrics = SmallPageMetrics(3);
        var shortSong = Block(1);
        var longSong = Block(5);

        var layout = ColumnBalancer.Fill(new[] { shortSong, longSong }, metrics, startingPageNumber: 1);

        // Page 1 is ODD: the long song is allowed to continue right after the short song in the
        // SAME left column (1 + 2 of its lines = 3, filling it), no forced break.
        Assert.That(layout.Pages[0].Left.HeightInLines, Is.EqualTo(3));
        Assert.That(layout.Pages[0].Left.Lines[0], Is.EqualTo("line 1\\\\"));
    }

    [Test]
    public void Fill_ALongSongsOverflow_MayBeFollowedByAnotherSongInTheSameLeftoverSpace()
    {
        // Finding U1: a page/column is never reserved exclusively for one song.
        var metrics = SmallPageMetrics(5);
        var longSong = Block(6); // exceeds 5 => chunks: page1 left+right (5+1)
        var nextSong = Block(4); // should fill the remaining space right after the long song's last chunk

        var layout = ColumnBalancer.Fill(new[] { longSong, nextSong }, metrics, startingPageNumber: 1);

        Assert.That(layout.Pages, Has.Count.EqualTo(1));
        Assert.That(layout.Pages[0].Left.HeightInLines, Is.EqualTo(5));
        Assert.That(layout.Pages[0].Right!.HeightInLines, Is.EqualTo(5)); // 1 (long song's tail) + 4 (next song)
    }

    [Test]
    public void PagesConsumed_CountsAPartiallyFilledLastPage_AsOneFullPage()
    {
        var layout = ColumnBalancer.Fill(new[] { Block(1) }, SmallPageMetrics(10), startingPageNumber: 1);

        Assert.That(layout.PagesConsumed, Is.EqualTo(1));
    }

    [Test]
    public void Render_PositionsALoneRightColumn_UnderTheRightColumn_NotFlushWithTheLeftMargin()
    {
        // Regression coverage: without an `\hspace` offset matching the left column's own width
        // (plus the gutter), a lone right-side column renders flush against the page's left
        // margin instead of under the right column — the bug behind a "stray"-looking line
        // reported in real-world testing.
        var left = new Column(Array.Empty<string>());
        var right = new Column(new[] { "\\paragraph*{2. Title\\\\}", "lyric line\\\\" });
        var layout = new PageLayout(new[] { (left, (Column?)right) });

        var result = ColumnBalancer.Render(layout);

        var titleIndex = result.IndexOf("\\paragraph*{2. Title\\\\}", StringComparison.Ordinal);
        var precedingText = result[..titleIndex];
        Assert.That(precedingText, Does.Contain("\\hspace{.475\\textwidth}"));
    }

    // Regression coverage for a real-world bug found via manual testing (C:\Sangblad-test):
    // without `[t]`, LaTeX's default `[c]` (vertically centered) minipage alignment does not
    // reliably top-align two side-by-side boxes of different heights even once their total
    // heights are equalized via the trailing `\vspace` above — verified against a real compiled
    // PDF, where the shorter column's first line landed roughly one `\baselineskip` below its
    // sibling's. `[t]` anchors each box's own top edge to the current line instead.

    [Test]
    public void Render_TopAlignsBothMinipagesInAPairedRow_SoUnequalHeightsStillStartFlush()
    {
        var left = new Column(new[] { "left line\\\\" });
        var right = new Column(new[] { "right line 1\\\\", "right line 2\\\\", "right line 3\\\\" });
        var layout = new PageLayout(new[] { (left, (Column?)right) });

        var result = ColumnBalancer.Render(layout);

        Assert.That(result, Does.Contain("\\begin{minipage}[t]{.475\\textwidth}"));
        Assert.That(result, Does.Contain("\\begin{minipage}[t]{.45\\textwidth}"));
    }

    private static string ExtractMinipage(string latex, string width)
    {
        var start = latex.IndexOf($"{{{width}}}", StringComparison.Ordinal);
        var end = latex.IndexOf("\\end{minipage}", start, StringComparison.Ordinal);
        return latex[start..end];
    }
}
