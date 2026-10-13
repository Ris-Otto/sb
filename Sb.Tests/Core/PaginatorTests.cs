using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class PaginatorTests
{
    private const int ColumnWidth = 1000; // generous enough that no line ever wraps
    // Line 2 of every song is empty, ending the first paragraph after one line (A-2).
    private const int LinesPerColumn = 10;

    private static Song Make(string title, int textLines) =>
        Song.Create(title, Enumerable.Range(1, textLines).Select(i => i == 2 ? "" : $"line {i}").ToArray(), 1).Value;

    [Test]
    public void PaginateAsListed_NonPositiveLinesPerColumn_ThrowsImmediately_NeverHangs()
    {
        // Regression guard: must fail fast via ColumnFiller's own guard, not loop forever because
        // `remaining` never shrinks.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Paginator.PaginateAsListed([Make("A", 1)], linesPerColumn: 0, ColumnWidth));
    }

    [Test]
    public void PaginateAsListed_SongsFitOnePageExactly_ProducesOnePage()
    {
        Song a = Make("A", 4); // height 5
        Song b = Make("B", 4); // height 5
        Song c = Make("C", 4); // height 5
        Song d = Make("D", 4); // height 5

        var pages = Paginator.PaginateAsListed([a, b, c, d], LinesPerColumn, ColumnWidth);

        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].Column1.Select(f => f.Song), Is.EqualTo(new[] { a, b }));
        Assert.That(pages[0].Column2.Select(f => f.Song), Is.EqualTo(new[] { c, d }));
    }

    [Test]
    public void PaginateAsListed_OddPage_NonFittingSongSplitsAcrossPageBoundary()
    {
        Song a = Make("A", 5);  // height 6
        Song b = Make("B", 6);  // height 7 — overflows column 1→2 within page 1 (4 + 3)
        Song c = Make("C", 10); // height 11 — doesn't fit column 2's remaining 7 lines

        var pages = Paginator.PaginateAsListed([a, b, c], LinesPerColumn, ColumnWidth);

        Assert.That(pages, Has.Count.EqualTo(2));

        var page1 = pages[0];
        Assert.That(page1.Number, Is.EqualTo(1));
        Assert.That(page1.Column1Lines, Is.EqualTo(10));
        Assert.That(page1.Column2Lines, Is.EqualTo(10), "page 1's column 2 should be topped up with c's first part");

        SongFragment cFirstPart = page1.Column2[^1];
        Assert.That(cFirstPart.Song, Is.SameAs(c));
        Assert.That(cFirstPart.IncludesTitle, Is.True);

        var page2 = pages[1];
        Assert.That(page2.Number, Is.EqualTo(2));
        SongFragment cRemainder = page2.Column1[0];
        Assert.That(cRemainder.Song, Is.SameAs(c));
        Assert.That(cRemainder.IncludesTitle, Is.False);

        // No lines of c lost or duplicated across the split.
        Assert.That(cFirstPart.Lines.Concat(cRemainder.Lines), Is.EqualTo(c.Text));

        // A page-boundary split needs a continuation notice (FR-1.3.16) — flagged on both sides.
        Assert.That(cFirstPart.ContinuesOnNextPage, Is.True);
        Assert.That(cRemainder.ContinuesFromPreviousPage, Is.True);
    }

    [Test]
    public void PaginateAsListed_EvenPage_NonFittingSongMovesWholeToNextPage_NoSplit()
    {
        // Page 1: two songs that exactly fill both columns, so page 1 contributes no leftover.
        Song a = Make("A", 9); // height 10
        Song b = Make("B", 9); // height 10

        // Page 2 (even): same shape as the odd-page test above, but splitting must NOT happen here.
        Song d = Make("D", 5); // height 6
        Song e = Make("E", 6); // height 7 — overflows column 1→2 within page 2 (4 + 3); always allowed
        Song f = Make("F", 7); // height 8 — doesn't fit column 2's remaining 7 lines on page 2

        var pages = Paginator.PaginateAsListed([a, b, d, e, f], LinesPerColumn, ColumnWidth);

        Assert.That(pages, Has.Count.EqualTo(3));

        var page2 = pages[1];
        Assert.That(page2.Number, Is.EqualTo(2));
        Assert.That(page2.Column2.Select(fr => fr.Song), Is.EqualTo(new[] { e }), "f must not appear on page 2 at all");
        Assert.That(page2.Column2Lines, Is.EqualTo(3), "page 2's leftover must stay unfilled, not topped up with part of f");

        var page3 = pages[2];
        Assert.That(page3.Number, Is.EqualTo(3));
        Assert.That(page3.Column1, Has.Count.EqualTo(1));
        SongFragment fWhole = page3.Column1[0];
        Assert.That(fWhole.Song, Is.SameAs(f));
        Assert.That(fWhole.IncludesTitle, Is.True);
        Assert.That(fWhole.Lines, Is.EqualTo(f.Text), "f must move entirely undivided, not split");
    }

    [Test]
    public void PaginateAsListed_RealLyricsFixtures_EveryLineAppearsExactlyOnceAcrossAllPages()
    {
        string[] fixtureFiles = ["Lille_albin.txt", "Svettvisan.txt", "bankvisan.txt"];
        var songs = fixtureFiles
            .Select((file, i) => Song.Create(
                Path.GetFileNameWithoutExtension(file),
                File.ReadAllLines(Path.Combine("TestFiles", file)),
                i + 1).Value)
            .ToList();

        var pages = Paginator.PaginateAsListed(songs, linesPerColumn: 40, columnWidth: 30);

        Assert.That(pages, Is.Not.Empty);

        foreach (Song song in songs)
        {
            var fragmentsForSong = pages
                .SelectMany(p => p.Column1.Concat(p.Column2))
                .Where(f => f.Song == song)
                .ToList();

            Assert.That(fragmentsForSong, Is.Not.Empty, $"no fragments found for '{song.Title}'");
            Assert.That(fragmentsForSong.Count(f => f.IncludesTitle), Is.EqualTo(1), $"'{song.Title}' title must appear exactly once");

            // An empty line falling exactly on a break is dropped rather than shown as a gap (A-5).
            var reassembledLines = fragmentsForSong.SelectMany(f => f.Lines).Where(l => l != "").ToArray();
            Assert.That(reassembledLines, Is.EqualTo(song.Text.Where(l => l != "")), $"lines lost/duplicated for '{song.Title}'");
        }
    }
}
