using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class SpaceEfficientPaginatorTests
{
    private const int ColumnWidth = 1000; // generous enough that no line ever wraps
    private const int LinesPerColumn = 10;

    private static Song Make(string title, int height) =>
        Song.Create(title, Enumerable.Range(1, height - 1).Select(i => $"line {i}").ToArray(), 1).Value;

    [Test]
    public void Paginate_NonPositiveLinesPerColumn_ThrowsImmediately_NeverHangsOrGrowsUnbounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SpaceEfficientPaginator.Paginate([Make("A", 2)], linesPerColumn: 0, ColumnWidth));
    }

    [Test]
    public void Paginate_SongSpanningThreePlusPages_ContinuesAcrossEvenPageToo_FlaggedCorrectly()
    {
        // FR-1.3.15: a song too long for one page continues regardless of page parity — even onto
        // an even page, where the normal odd-page-only overflow rule (FR-1.3.6) would otherwise
        // forbid a page-boundary split. 31 lines on a 5-line column (page capacity 10) forces the
        // song across (at least) pages 1, 2, and 3.
        Song big = Make("Big", height: 31);

        var pages = SpaceEfficientPaginator.Paginate([big], linesPerColumn: 5, ColumnWidth);

        Assert.That(pages.Count, Is.GreaterThanOrEqualTo(3));

        Page page2 = pages[1];
        Assert.That(page2.Number, Is.EqualTo(2), "page 2 must be even");
        SongFragment landingOnEvenPage = page2.Column1[0];
        Assert.That(landingOnEvenPage.Song, Is.SameAs(big));
        Assert.That(landingOnEvenPage.ContinuesFromPreviousPage, Is.True,
            "must resume from page 1 despite landing on an even page");

        // Every page but the very first must start with a continuation (nothing starts fresh mid-song).
        for (int i = 1; i < pages.Count; i++)
        {
            Assert.That(pages[i].Column1[0].ContinuesFromPreviousPage, Is.True,
                $"page {pages[i].Number} should open with a continuation of the same oversized song");
        }

        // All content survives, in order, regardless of how many pages it took.
        var allFragments = pages.SelectMany(p => p.Column1.Concat(p.Column2)).ToList();
        Assert.That(allFragments.SelectMany(f => f.Lines).ToArray(), Is.EqualTo(big.Text));
        Assert.That(allFragments.Count(f => f.IncludesTitle), Is.EqualTo(1));
    }

    [Test]
    public void Paginate_PlacesLongestRemainingSongFirstOnEachPage()
    {
        Song small = Make("Small", 3);
        Song medium = Make("Medium", 5);
        Song longest = Make("Longest", 8);

        // Given out of order on purpose.
        var pages = SpaceEfficientPaginator.Paginate([small, medium, longest], LinesPerColumn, ColumnWidth);

        Assert.That(pages[0].Column1[0].Song, Is.SameAs(longest));
    }

    [Test]
    public void Paginate_UsesExactSearch_WhenFfdWouldBeSuboptimal()
    {
        // Longest reserved (height 10, leaving exactly a 10-line budget for "others" —
        // 2*linesPerColumn(20) - 10). The others mirror the classic FFD bin-covering failure: FFD
        // takes 6 first and then can't fit either 5, while the exact search finds {5, 5} = 10, a
        // strictly better (perfect) fill of that 10-line sub-budget.
        Song longest = Make("Longest", 10);
        Song a = Make("A", 6);
        Song b = Make("B", 5);
        Song c = Make("C", 5);

        var pages = SpaceEfficientPaginator.Paginate([longest, a, b, c], LinesPerColumn, ColumnWidth);

        // All four songs must appear (b and c should be selected via the exact-search fallback,
        // not left behind the way plain FFD would leave them).
        var allSongs = pages.SelectMany(p => p.Column1.Concat(p.Column2)).Select(f => f.Song).Distinct();
        Assert.That(allSongs, Is.EquivalentTo(new[] { longest, a, b, c }));
    }

    [Test]
    public void Paginate_OversizedCarryOver_NeverLost_AllLinesSurvive()
    {
        // Regression test: a single song far longer than one page must not silently vanish when
        // its carry-over fragment from a previous page still doesn't fit a fresh page.
        Song big = Make("Big", height: 61); // 60 text lines, on a 5-line column (page capacity 10)
        Song small = Make("Small", height: 2);

        var pages = SpaceEfficientPaginator.Paginate([big, small], linesPerColumn: 5, ColumnWidth);

        var allFragments = pages.SelectMany(p => p.Column1.Concat(p.Column2)).ToList();
        int totalLinesAcrossPages = allFragments.Sum(f => f.Lines.Count);
        Assert.That(totalLinesAcrossPages, Is.EqualTo(big.Text.Count + small.Text.Count));

        var bigFragments = allFragments.Where(f => f.Song == big).ToList();
        Assert.That(bigFragments.Count(f => f.IncludesTitle), Is.EqualTo(1));
        Assert.That(bigFragments.SelectMany(f => f.Lines).ToArray(), Is.EqualTo(big.Text));

        var smallFragments = allFragments.Where(f => f.Song == small).ToList();
        Assert.That(smallFragments.SelectMany(f => f.Lines).ToArray(), Is.EqualTo(small.Text));
    }

    [Test]
    public void Paginate_RealLyricsFixtures_EveryLineAppearsExactlyOnce()
    {
        // Real song lengths are arbitrary, and this is a greedy page-by-page algorithm with no
        // cross-page lookahead — FR-1.3.13 explicitly allows any page's best achievable fill to
        // fall short of 95% when the remaining pool just doesn't combine well, so this test checks
        // fidelity only. Paginate_SyntheticFlexibleSongs_AllNonLastPagesMeetFillTarget below checks
        // the 95% target is actually honored whenever the pool has good combinations available.
        string[] fixtureFiles = Directory.GetFiles("TestFiles", "*.txt");
        var songs = fixtureFiles
            .Select((file, i) => Song.Create(
                Path.GetFileNameWithoutExtension(file), File.ReadAllLines(file), i + 1).Value)
            .ToList();

        var pages = SpaceEfficientPaginator.Paginate(songs, linesPerColumn: 40, columnWidth: 30);

        Assert.That(pages, Is.Not.Empty);

        foreach (Song song in songs)
        {
            var fragments = pages.SelectMany(p => p.Column1.Concat(p.Column2)).Where(f => f.Song == song).ToList();
            Assert.That(fragments, Is.Not.Empty, $"no fragments found for '{song.Title}'");
            Assert.That(fragments.Count(f => f.IncludesTitle), Is.EqualTo(1), $"'{song.Title}' title must appear exactly once");
            Assert.That(fragments.SelectMany(f => f.Lines).ToArray(), Is.EqualTo(song.Text), $"lines lost/duplicated for '{song.Title}'");
        }
    }

    [Test]
    public void Paginate_SyntheticFlexibleSongs_AllNonLastPagesMeetFillTarget()
    {
        // Many songs with varied, small-relative-to-capacity heights give the exact search ample
        // combinations on every page, so the 95% target should be reliably reachable until the pool
        // finally runs low near the very end.
        const int linesPerColumn = 20;
        var songs = Enumerable.Range(0, 30)
            .Select(i => Make($"Song {i}", height: 3 + (i % 10)))
            .ToList();

        var pages = SpaceEfficientPaginator.Paginate(songs, linesPerColumn, ColumnWidth);

        Assert.That(pages.Count, Is.GreaterThan(1));
        for (int i = 0; i < pages.Count - 1; i++)
        {
            int totalLines = pages[i].Column1Lines + pages[i].Column2Lines;
            double fillFraction = (double)totalLines / (2 * linesPerColumn);
            Assert.That(fillFraction, Is.GreaterThanOrEqualTo(SpaceEfficiency.FillTarget),
                $"page {pages[i].Number} (not the last) only reached {fillFraction:P0} fill");
        }
    }
}
