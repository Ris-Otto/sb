using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class ColumnFillerTests
{
    // One title line + N text lines, with columnWidth generous enough that no wrapping occurs,
    // so each song's height is exactly (1 + N) and tests can reason about exact line counts.
    private const int ColumnWidth = 1000;

    private static SongFragment Make(string title, int textLines, int number = 1)
    {
        Song song = Song.Create(title, Enumerable.Range(1, textLines).Select(i => $"line {i}").ToArray(), number).Value;
        return SongFragment.Whole(song, ColumnWidth);
    }

    private static IEnumerable<Song> Songs(IEnumerable<SongFragment> fragments) => fragments.Select(f => f.Song);

    [Test]
    public void Fill_WholeSongsExactlyFillColumn1_NoOverflow()
    {
        SongFragment a = Make("A", textLines: 4); // height 5
        SongFragment b = Make("B", textLines: 4); // height 5

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a, b }));
        Assert.That(layout.Column1Lines, Is.EqualTo(10));
        Assert.That(layout.Column2, Is.Empty);
        Assert.That(layout.LeftoverLines, Is.EqualTo(10));
        Assert.That(layout.RemainingFragments, Is.Empty);
    }

    [Test]
    public void Fill_SongOverflowsFromColumn1ToColumn2()
    {
        SongFragment a = Make("A", textLines: 5); // height 6
        SongFragment b = Make("B", textLines: 6); // height 7 — doesn't fit in the remaining 4 lines of column 1

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(Songs(layout.Column1), Is.EqualTo(new[] { a.Song, b.Song }), "b starts in column 1");
        Assert.That(layout.Column1Lines, Is.EqualTo(10));
        Assert.That(Songs(layout.Column2), Is.EqualTo(new[] { b.Song }), "b continues into column 2");
        Assert.That(layout.Column2Lines, Is.EqualTo(3));
        Assert.That(layout.LeftoverLines, Is.EqualTo(7));
        Assert.That(layout.RemainingFragments, Is.Empty);

        // The split must be a genuine content split, not just a duplicated reference.
        SongFragment column1Part = layout.Column1[1];
        SongFragment column2Part = layout.Column2[0];
        Assert.That(column1Part.IncludesTitle, Is.True);
        Assert.That(column2Part.IncludesTitle, Is.False);
        Assert.That(column1Part.Lines.Concat(column2Part.Lines), Is.EqualTo(b.Song.Text));
    }

    [Test]
    public void Fill_LeftoverReportedWhenSongsRunOut()
    {
        SongFragment a = Make("A", textLines: 9); // height 10, fills column 1 exactly
        SongFragment b = Make("B", textLines: 3); // height 4, placed whole in column 2

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column2, Is.EqualTo(new[] { b }));
        Assert.That(layout.Column2Lines, Is.EqualTo(4));
        Assert.That(layout.LeftoverLines, Is.EqualTo(6));
    }

    [Test]
    public void Fill_NextSongAfterColumn1ExactlyFull_PlacedWholeInColumn2_NotTreatedAsOverflow()
    {
        SongFragment a = Make("A", textLines: 9); // height 10, exactly fills column 1 (remainingColumn1 becomes 0)
        SongFragment b = Make("B", textLines: 2); // height 3, should land whole in column 2, not split

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a }));
        Assert.That(layout.Column2, Is.EqualTo(new[] { b }));
        Assert.That(layout.Column2Lines, Is.EqualTo(3));
    }

    [Test]
    public void Fill_SongsLeftOverAfterPageIsFull_AreReturnedAsRemaining()
    {
        SongFragment a = Make("A", textLines: 9); // height 10, fills column 1
        SongFragment b = Make("B", textLines: 9); // height 10, fills column 2 exactly (no room to overflow into)
        SongFragment c = Make("C", textLines: 4); // height 5, doesn't fit anywhere on this page

        var layout = ColumnFiller.Fill([a, b, c], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a }));
        Assert.That(layout.Column2, Is.EqualTo(new[] { b }));
        Assert.That(layout.LeftoverLines, Is.EqualTo(0));
        Assert.That(layout.RemainingFragments, Is.EqualTo(new[] { c }));
    }

    [Test]
    public void Fill_SongTooLargeForBothColumns_SplitsAcrossBoth_TrueExcessCarriedForward()
    {
        SongFragment a = Make("A", textLines: 5); // height 6, leaves 4 lines in column 1
        SongFragment b = Make("B", textLines: 20); // height 21 — more than a full column 1 + column 2 (20)

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        // Column 1 gets filled to capacity (a, then as much of b as fits).
        Assert.That(Songs(layout.Column1), Is.EqualTo(new[] { a.Song, b.Song }));
        Assert.That(layout.Column1Lines, Is.EqualTo(10));

        // Column 2 gets filled to capacity with more of b — never left empty just because b is huge.
        Assert.That(Songs(layout.Column2), Is.EqualTo(new[] { b.Song }));
        Assert.That(layout.Column2Lines, Is.EqualTo(10));
        Assert.That(layout.LeftoverLines, Is.EqualTo(0));

        // Only the genuine excess (beyond both full columns) is deferred — not the whole song.
        Assert.That(layout.RemainingFragments, Has.Count.EqualTo(1));
        SongFragment excess = layout.RemainingFragments[0];
        Assert.That(excess.Song, Is.SameAs(b.Song));
        Assert.That(excess.IncludesTitle, Is.False);

        // No content lost or duplicated across all three pieces.
        var reassembled = layout.Column1[1].Lines.Concat(layout.Column2[0].Lines).Concat(excess.Lines).ToArray();
        Assert.That(reassembled, Is.EqualTo(b.Song.Text));

        // b's column1 piece needs no notice yet (its continuation is same-page, in column 2). b's
        // column2 piece DOES need one (what follows it — the excess — is on a later page), and the
        // excess itself must be flagged as resuming from the previous page.
        Assert.That(layout.Column1[1].ContinuesOnNextPage, Is.False);
        Assert.That(layout.Column2[0].ContinuesOnNextPage, Is.True);
        Assert.That(excess.ContinuesFromPreviousPage, Is.True);
    }

    [Test]
    public void Fill_ColumnBoundarySplit_NotFlaggedAsPageContinuation()
    {
        SongFragment a = Make("A", textLines: 5); // height 6
        SongFragment b = Make("B", textLines: 6); // height 7 — overflows column 1→2 within the same page

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1[1].ContinuesOnNextPage, Is.False);
        Assert.That(layout.Column2[0].ContinuesFromPreviousPage, Is.False);
    }

    [Test]
    public void Fill_FirstCandidateTallerThanOneColumn_Column1NeverLeftEmpty()
    {
        // Regression test: a lead candidate taller than a single column must still start in column 1,
        // never leave column 1 empty while column 2 receives content instead.
        SongFragment big = Make("Big", textLines: 10); // height 11, on a 5-line column

        var layout = ColumnFiller.Fill([big], linesPerColumn: 5, ColumnWidth);

        Assert.That(layout.Column1, Is.Not.Empty, "column 1 must never be skipped in favor of column 2");
        Assert.That(layout.Column1[0].Song, Is.SameAs(big.Song));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Fill_NonPositiveLinesPerColumn_ThrowsImmediately(int linesPerColumn)
    {
        // Regression guard: must fail fast and loud, not silently hang a caller's pagination loop.
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnFiller.Fill([Make("A", 1)], linesPerColumn, ColumnWidth));
    }
}
