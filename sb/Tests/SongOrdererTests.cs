using NUnit.Framework;
using sb.Leaflets;
using sb.Ordering;
using sb.Songs;

namespace sb.Tests;

[TestFixture]
public class SongOrdererTests
{
    private static PageMetrics SmallPageMetrics(int linesPerPage) =>
        new(paperHeightPt: linesPerPage * 10, topMarginPt: 0, bottomMarginPt: 0, baselineSkipPt: 10);

    [Test]
    public void Order_AsListed_PreservesExactInputOrder()
    {
        var songs = new[]
        {
            new Song("First", "text one", 1),
            new Song("Second", "text two", 2),
            new Song("Third", "text three", 3)
        };

        var ordered = SongOrderer.Order(songs, OrderingMode.AsListed, SmallPageMetrics(10));

        Assert.That(ordered.Select(s => s.Title), Is.EqualTo(new[] { "First", "Second", "Third" }));
    }

    [Test]
    public void Order_AsListed_ReturnsAllSongsExactlyOnce()
    {
        var songs = new[]
        {
            new Song("A", "a", 1),
            new Song("B", "b", 2)
        };

        var ordered = SongOrderer.Order(songs, OrderingMode.AsListed, SmallPageMetrics(10));

        Assert.That(ordered, Has.Count.EqualTo(2));
        Assert.That(ordered, Is.EquivalentTo(songs));
    }

    private static Song MakeSong(string title, int lineCount) =>
        new(title, string.Join("\n", Enumerable.Repeat("line", lineCount)), singingOrderNumber: 1);

    private static List<string> SpaceEfficientTitles(Song[] songs, int linesPerPage) =>
        SongOrderer.Order(songs, OrderingMode.SpaceEfficient, SmallPageMetrics(linesPerPage))
            .Select(s => s.Title)
            .ToList();

    [Test]
    public void Order_SpaceEfficient_PlacesTheLongestSongFirst()
    {
        var songs = new[] { MakeSong("Short", 1), MakeSong("Long", 7), MakeSong("Medium", 4) };

        Assert.That(SpaceEfficientTitles(songs, 10), Is.EqualTo(new[] { "Long", "Medium", "Short" }));
    }

    [Test]
    public void Order_SpaceEfficient_StartsEachPageWithTheLongestRemainingSong_AndKeepsEqualLengthsInOriginalOrder()
    {
        // Heights (title + lines): D=2, C=4, A=6, E=10, B=10; a page holds two 10-line columns.
        // Page 1 takes the two longest (E before B — equal length keeps original order); page 2
        // restarts with the longest remaining song (A), then fills with C and D.
        var songs = new[]
        {
            MakeSong("D", 1), MakeSong("C", 3), MakeSong("A", 5), MakeSong("E", 9), MakeSong("B", 9)
        };

        Assert.That(SpaceEfficientTitles(songs, 10), Is.EqualTo(new[] { "E", "B", "A", "C", "D" }));
    }

    [Test]
    public void Order_SpaceEfficient_FillsARemainingGapWithTheLongestSongThatStillFits()
    {
        // Heights: X=6, Y=6, Z=3, W=2 (X/Y tie, so input order puts Y first). Page 1: Y (left), X
        // (right), then Z — the longest remaining song that still fits the right column's last 4
        // lines once its blank line above the title (FR-027) is counted: 6 + 1 + 3 = 10. W (2) is
        // left for page 2: nothing is left on page 1 once Z has filled the right column.
        var songs = new[] { MakeSong("W", 1), MakeSong("Z", 2), MakeSong("Y", 5), MakeSong("X", 5) };

        Assert.That(SpaceEfficientTitles(songs, 10), Is.EqualTo(new[] { "Y", "X", "Z", "W" }));
    }

    [Test]
    public void Order_SpaceEfficient_SongLongerThanAPage_IsPlacedFirstAndItsLastPageIsFilledWithSmallerSongs()
    {
        // Long = 25 lines spans 3 columns (page 1 both columns, then 5 lines of page 2's left
        // column); Small (4) fits the 5 lines left on that last page, Tiny (3) goes into the
        // next column. Input order is deliberately reversed.
        var songs = new[] { MakeSong("Tiny", 2), MakeSong("Small", 3), MakeSong("Long", 24) };

        Assert.That(SpaceEfficientTitles(songs, 10), Is.EqualTo(new[] { "Long", "Small", "Tiny" }));
    }


    [Test]
    public void Order_SpaceEfficient_CountsABlankLineAsOneLine_WhenRankingSongsByLength()
    {
        // Heights: Spaced = title + 3 lines + 1 blank = 5; Plain = title + 4 lines = 5 — a tie,
        // so the original order (Plain, Spaced) is kept. (A blank counted as two lines would
        // make Spaced longer and float it to the front.)
        var spaced = new Song("Spaced", "a\nb\n\nc", singingOrderNumber: 1);
        var plain = MakeSong("Plain", 4);

        Assert.That(SpaceEfficientTitles(new[] { plain, spaced }, 10), Is.EqualTo(new[] { "Plain", "Spaced" }));
    }

    [Test]
    public void Order_SpaceEfficient_AccountsForTheChapterHeading_OnThePageItStartsOn()
    {
        // linesPerPage=10 with a 3-line heading => page 1's columns hold 7. Heights: A=10, B=10
        // (B listed first, so it wins the tie), D=2. B spills across page 1's two columns
        // (7 + 3); A would then spill past page 1, so it cannot join B there, but D fits the
        // 4 lines left (3 + 1 blank line above its title + 2 = 6 <= 7). Without the heading A and
        // B would each fill a whole 10-line column.
        var metrics = new PageMetrics(paperHeightPt: 100, topMarginPt: 0, bottomMarginPt: 0, baselineSkipPt: 10, chapterHeadingLines: 3);
        var songs = new[] { MakeSong("D", 1), MakeSong("B", 9), MakeSong("A", 9) };

        var ordered = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics);

        Assert.That(ordered.Select(s => s.Title), Is.EqualTo(new[] { "B", "D", "A" }));
    }
    [Test]
    public void Order_SpaceEfficient_ReturnsEverySongExactlyOnce()
    {
        var songs = new[] { MakeSong("A", 9), MakeSong("B", 2), MakeSong("C", 5), MakeSong("D", 5), MakeSong("E", 1) };

        var ordered = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, SmallPageMetrics(10));

        Assert.That(ordered, Is.EquivalentTo(songs));
    }

    [Test]
    public void Order_SpaceEfficient_SingleSong_ReturnsItUnchanged()
    {
        var songs = new[] { MakeSong("Only", 3) };

        Assert.That(SpaceEfficientTitles(songs, 10), Is.EqualTo(new[] { "Only" }));
    }

    [Test]
    public void Order_SpaceEfficient_IsDeterministic_AcrossRepeatedCalls()
    {
        var songs = new[] { MakeSong("X", 3), MakeSong("Y", 3), MakeSong("Z", 3) };
        var metrics = SmallPageMetrics(10);

        var first = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics);
        var second = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics);

        Assert.That(first.Select(s => s.Title), Is.EqualTo(second.Select(s => s.Title)));
    }
}
