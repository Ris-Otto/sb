using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class SongSplitterTests
{
    private const int ColumnWidth = 1000; // generous enough that no line ever wraps
    // Line 2 of every song is empty, ending the first paragraph after one line (A-2).

    private static Song MakeSong(int textLines) =>
        Song.Create("Title", Enumerable.Range(1, textLines - 1).Select(i => i == 2 ? "" : $"line {i}").ToArray(), 1).Value;

    [Test]
    public void Split_FitsEntirely_ReturnsWholeFragmentNoRest()
    {
        Song song = MakeSong(textLines: 4); // height 5
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 5, ColumnWidth);

        Assert.That(first, Is.EqualTo(fragment));
        Assert.That(remainder, Is.Null);
    }

    [Test]
    public void Split_NoRoomForTitle_ReturnsNullBoth()
    {
        Song song = MakeSong(textLines: 4);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 0, ColumnWidth);

        Assert.That(first, Is.Null);
        Assert.That(remainder, Is.Null);
    }

    [Test]
    public void Split_PartialFit_FirstFragmentIncludesTitleAndFittingLines()
    {
        Song song = MakeSong(textLines: 6); // height 7: 2 header lines (title + spacing) + 5 text lines
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 4, ColumnWidth);

        Assert.That(first, Is.Not.Null);
        Assert.That(first!.IncludesTitle, Is.True);
        Assert.That(first.Lines, Is.EqualTo(new[] { "line 1", "" }));
        Assert.That(first.Height, Is.EqualTo(4));
    }

    [Test]
    public void Split_PartialFit_RestFragmentHasRemainingLinesNoTitle()
    {
        Song song = MakeSong(textLines: 6);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (_, remainder) = SongSplitter.Split(fragment, availableLines: 4, ColumnWidth);

        Assert.That(remainder, Is.Not.Null);
        Assert.That(remainder!.IncludesTitle, Is.False);
        Assert.That(remainder.Lines, Is.EqualTo(new[] { "line 3", "line 4", "line 5" }));
        Assert.That(remainder.Height, Is.EqualTo(3));
    }

    [Test]
    public void Split_PartialFit_NoLinesLostAcrossFirstAndRest()
    {
        Song song = MakeSong(textLines: 6);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 4, ColumnWidth);

        var reassembled = first!.Lines.Concat(remainder!.Lines).ToArray();
        Assert.That(reassembled, Is.EqualTo(song.Text));
    }

    [Test]
    public void Split_PartialFit_BothFragmentsReferenceSameSong()
    {
        Song song = MakeSong(textLines: 6);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 4, ColumnWidth);

        Assert.That(first!.Song, Is.SameAs(song));
        Assert.That(remainder!.Song, Is.SameAs(song));
    }


    [Test]
    public void Split_TitleOnlyFits_RefusesToStartSong()
    {
        var fragment = SongFragment.Whole(MakeSong(textLines: 6), ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 1, ColumnWidth);

        Assert.That(first, Is.Null);
        Assert.That(remainder, Is.Null);
    }

    [Test]
    public void Split_FirstParagraphDoesNotFit_RefusesToStartSong()
    {
        // Paragraph 1 is "a","b","c" (3 lines); title + 2 lines of it fit, which is not enough (A-2).
        Song song = Song.Create("Title", ["a", "b", "c", "", "d", "e"], 1).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 4, ColumnWidth);

        Assert.That(first, Is.Null);
        Assert.That(remainder, Is.Null);
    }

    [Test]
    public void Split_FirstParagraphFits_StartsSong()
    {
        Song song = Song.Create("Title", ["a", "b", "c", "", "d", "e"], 1).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 6, ColumnWidth);

        Assert.That(first!.Lines, Is.EqualTo(new[] { "a", "b", "c", "" }));
        Assert.That(remainder!.Lines, Is.EqualTo(new[] { "d", "e" }));
    }

    [Test]
    public void Split_FreshColumn_AllowsPartialFirstParagraph()
    {
        Song song = Song.Create("Title", ["a", "b", "c", "", "d", "e"], 1).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(
            fragment, availableLines: 4, ColumnWidth, allowPartialFirstParagraph: true);

        Assert.That(first!.Lines, Is.EqualTo(new[] { "a", "b" }));
        Assert.That(remainder, Is.Not.Null);
    }

    [Test]
    public void Split_PageBoundary_HeightsIncludeFortsNoticeLines()
    {
        // The "(forts.)" notice at the end of the first part and at the start of the remainder are
        // rendered lines, so they count towards the heights (A-5).
        Song song = Song.Create("Title", ["a", "b", "", "c", "d", "e"], 1).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 7, ColumnWidth, isPageBoundary: true);

        Assert.That(first!.Lines, Is.EqualTo(new[] { "a", "b", "", "c" }));
        Assert.That(first.Height, Is.EqualTo(2 + 4 + 1), "title + its spacing + 4 lines + trailing notice");
        Assert.That(remainder!.Lines, Is.EqualTo(new[] { "d", "e" }));
        Assert.That(remainder.Height, Is.EqualTo(1 + 2), "leading notice + 2 lines");
    }

    [Test]
    public void Split_ColumnBoundary_AddsNoNoticeLines()
    {
        Song song = Song.Create("Title", ["a", "b", "", "c", "d", "e"], 1).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 6, ColumnWidth);

        Assert.That(first!.Height, Is.EqualTo(2 + 4));
        Assert.That(remainder!.Height, Is.EqualTo(2));
    }

    [Test]
    public void Split_ParagraphFitsButNotItsTrailingEmptyLine_StartsSong()
    {
        // Paragraph 1 is a,b,c (3 lines); with the title that's 4 lines, and the empty line after it
        // need not fit (A-5). The empty line at the break is dropped, not shown at the top of the rest.
        Song song = Song.Create("Title", ["a", "b", "c", "", "d"], 1).Value;
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 5, ColumnWidth);

        Assert.That(first!.Lines, Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(remainder!.Lines, Is.EqualTo(new[] { "d" }));
    }

    [Test]
    public void Split_ContinuationFragment_SplitsAgainWithoutReintroducingTitle()
    {
        // A fragment that's already a continuation (no title) can itself be split further
        // (e.g. a song spanning three pages) — it must never gain a title back.
        var continuation = new SongFragment(MakeSong(6), IncludesTitle: false, ["line 3", "line 4", "line 5", "line 6"], Height: 4);

        var (first, remainder) = SongSplitter.Split(continuation, availableLines: 2, ColumnWidth);

        Assert.That(first!.IncludesTitle, Is.False);
        Assert.That(first.Lines, Is.EqualTo(new[] { "line 3", "line 4" }));
        Assert.That(remainder!.IncludesTitle, Is.False);
        Assert.That(remainder.Lines, Is.EqualTo(new[] { "line 5", "line 6" }));
    }

    [Test]
    public void Split_ColumnBoundary_NeitherFragmentFlaggedAsPageContinuation()
    {
        Song song = MakeSong(textLines: 6);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 3, ColumnWidth, isPageBoundary: false);

        Assert.That(first!.ContinuesOnNextPage, Is.False);
        Assert.That(remainder!.ContinuesFromPreviousPage, Is.False);
    }

    [Test]
    public void Split_PageBoundary_FirstFlaggedContinuesOnNextPage_RemainderFlaggedContinuesFromPreviousPage()
    {
        Song song = MakeSong(textLines: 6);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 5, ColumnWidth, isPageBoundary: true);

        Assert.That(first!.ContinuesOnNextPage, Is.True);
        Assert.That(remainder!.ContinuesFromPreviousPage, Is.True);
        Assert.That(remainder.ContinuesOnNextPage, Is.False, "not yet known whether it will need a further split");
    }

    [Test]
    public void Split_NoActualSplitOccurs_ContinuesOnNextPageStaysFalse_EvenIfPageBoundaryRequested()
    {
        Song song = MakeSong(textLines: 4);
        var fragment = SongFragment.Whole(song, ColumnWidth);

        var (first, remainder) = SongSplitter.Split(fragment, availableLines: 5, ColumnWidth, isPageBoundary: true);

        Assert.That(first, Is.EqualTo(fragment));
        Assert.That(first!.ContinuesOnNextPage, Is.False);
        Assert.That(remainder, Is.Null);
    }

    [Test]
    public void Split_AlreadyContinuingFragment_PreservesContinuesFromPreviousPage_WhenSplitAgain()
    {
        // A fragment landing at the top of a page (already flagged) that itself needs to split
        // further (e.g. spans a third page) must keep its "continues from previous page" flag.
        Song song = MakeSong(textLines: 6);
        var landing = new SongFragment(
            song, IncludesTitle: false, ["line 3", "line 4", "line 5", "line 6"], Height: 5,
            ContinuesFromPreviousPage: true);

        var (first, remainder) = SongSplitter.Split(landing, availableLines: 4, ColumnWidth, isPageBoundary: true);

        Assert.That(first!.ContinuesFromPreviousPage, Is.True, "still resumes from the earlier page");
        Assert.That(first.ContinuesOnNextPage, Is.True, "and now also continues onto yet another page");
        Assert.That(remainder!.ContinuesFromPreviousPage, Is.True);
    }
}
