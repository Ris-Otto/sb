using NUnit.Framework;
using sb.Leaflets;
using sb.Ordering;
using sb.Songs;

namespace sb.Tests;

[TestFixture]
public class LeafletBuilderTests
{
    private static Song[] TwoRealSongs() =>
        new[]
        {
            new Song(
                "Idas sommarvisa",
                File.ReadAllText(TestFixtures.Path("idas_sommar.txt")),
                singingOrderNumber: 1),
            new Song(
                "Barfotavisan",
                File.ReadAllText(TestFixtures.Path("barfotavisan.txt")),
                singingOrderNumber: 2)
        };

    private static LeafletChapter[] OneChapter(params Song[] songs) =>
        new[] { new LeafletChapter("main", null, songs) };

    [Test]
    public void Build_MultiColumn_ReproducesNonAsciiCharactersFromARealFixture_Uncorrupted()
    {
        // 003-utf8-encoding, research.md §3: writes are UTF-8, so non-ASCII source content
        // survives into the generated chapter.tex exactly.
        var fixtureText = File.ReadAllText(TestFixtures.Path("hon_var_så_liten.txt"));
        var song = new Song("Hon var så liten", fixtureText, singingOrderNumber: 1);
        var request = new LeafletRequest("Test", OneChapter(song), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);
        var chapterContent = leaflet.ChapterFiles.Single().Content;

        foreach (var line in fixtureText.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0))
            Assert.That(chapterContent, Does.Contain(line.Trim()));
    }

    [Test]
    public void Build_SingleColumn_ProducesADocumentContainingEverySongsTitleAndLyrics()
    {
        var request = new LeafletRequest("Sommarvisor", OneChapter(TwoRealSongs()), LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);

        Assert.That(leaflet.Document, Does.Contain("Sommarvisor")); // the leaflet title
        Assert.That(leaflet.Document, Does.Not.Contain("minipage"));
        Assert.That(leaflet.ChapterFiles, Has.Exactly(1).Items);
        Assert.That(leaflet.ChapterFiles[0].Content, Does.Contain("Idas sommarvisa"));
        Assert.That(leaflet.ChapterFiles[0].Content, Does.Contain("Barfotavisan"));
    }

    [Test]
    public void Build_MultiColumn_ProducesBalancedTwoColumnMinipages()
    {
        var request = new LeafletRequest("Sommarvisor", OneChapter(TwoRealSongs()), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);
        var chapterContent = leaflet.ChapterFiles.Single().Content;

        Assert.That(chapterContent, Does.Contain(".475\\textwidth"));
        Assert.That(chapterContent, Does.Contain(".45\\textwidth"));
        Assert.That(chapterContent, Does.Contain("\\hspace{0.075\\textwidth}"));
    }

    [Test]
    public void Build_MultiColumn_BreaksAnOverlongLineAcrossOutputLinesInsteadOfOverflowing()
    {
        var longLine = string.Join(" ", Enumerable.Repeat("word", 30)); // one long line, no internal line breaks
        var songs = new[] { new Song("Long Song", longLine, singingOrderNumber: 1) };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);
        var chapterContent = leaflet.ChapterFiles.Single().Content;

        Assert.That(chapterContent, Does.Not.Contain(longLine + "\\\\"));
        Assert.That(chapterContent, Does.Contain("word word"));
    }

    [Test]
    public void Build_PreservesTheUsersOwnLineBreaks_WithoutMergingOrSplittingAtPunctuation()
    {
        var songs = new[] { new Song("Song", "first line, with a comma.\nsecond line! more text.", singingOrderNumber: 1) };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);
        var chapterContent = leaflet.ChapterFiles.Single().Content;

        Assert.That(chapterContent, Does.Contain("first line, with a comma.\\\\"));
        Assert.That(chapterContent, Does.Contain("second line! more text.\\\\"));
    }

    [Test]
    public void Build_RendersAnInteriorBlankInputLine_AsOneMboxLine_AndNeverABareLineBreak()
    {
        var songs = new[] { new Song("Song", "first stanza\n\nsecond stanza", singingOrderNumber: 1) };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);
        var lines = leaflet.ChapterFiles.Single().Content.Split('\n');

        var firstStanzaIndex = Array.IndexOf(lines, "first stanza\\\\");
        Assert.That(firstStanzaIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(lines[firstStanzaIndex + 1], Is.EqualTo("\\mbox{}\\\\"));
        Assert.That(lines[firstStanzaIndex + 2], Is.EqualTo("second stanza\\\\"));
        Assert.That(lines, Has.None.EqualTo("\\\\"));
    }

    [Test]
    public void Build_IgnoresLeadingAndTrailingBlankLines_OfASongsText()
    {
        var padded = new Song("Same", "\n  \nonly line\n\n \n", singingOrderNumber: 1);
        var plain = new Song("Same", "only line", singingOrderNumber: 1);

        string Lyrics(Song song) => LeafletBuilder
            .Build(new LeafletRequest("Test", OneChapter(song), LeafletFormat.SingleColumn, OrderingMode.AsListed))
            .ChapterFiles.Single().Content;

        Assert.That(Lyrics(padded), Is.EqualTo(Lyrics(plain)));
        Assert.That(Lyrics(padded).Split('\n'), Has.None.EqualTo("\\mbox{}\\\\"));
    }

    [Test]
    public void Build_KeepsTheLineBreakOutsideTheRunInTitleArgument()
    {
        // Regression (C:\Test\loop-test.log): titlesec sets a run-in title in an \sbox (LR mode),
        // where `\\` raises "Not allowed in LR mode" once lyrics are \raggedright.
        var songs = new[] { new Song("Albins barn", "line", singingOrderNumber: 1) };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var content = LeafletBuilder.Build(request).ChapterFiles.Single().Content;

        Assert.That(content, Does.Contain("\\paragraph*{1. Albins barn}\\mbox{}\\\\"));
        Assert.That(content, Does.Not.Contain("barn\\\\}"));
    }

    [Test]
    public void Build_StartsEveryChapterFileWithZeroTitleSpacing_AndRaggedRightLyrics()
    {
        var songs = new[] { new Song("Song", "line", singingOrderNumber: 1) };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var content = LeafletBuilder.Build(request).ChapterFiles.Single().Content;

        Assert.That(content, Does.StartWith("\\titlespacing*{\\paragraph}{0pt}{0pt}{0pt}"));
        // Regression (compiled C:\Test document): a chapter-level \raggedright makes the minipage
        // rows overflow their pages, so it must only appear inside a group, after the heading.
        Assert.That(content.IndexOf("\\raggedright", StringComparison.Ordinal),
            Is.GreaterThan(content.IndexOf("\\chapter*", StringComparison.Ordinal)));
        Assert.That(content, Does.Contain("\\begingroup\\raggedright"));
        Assert.That(content, Does.EndWith("\\par\\endgroup"));
    }

    [Test]
    public void Build_MultiColumn_PutsOneBlankLineAboveANonLeadingTitle_ButNotAboveAColumnLeadingOne()
    {
        var songs = new[]
        {
            new Song("First", "a", singingOrderNumber: 1),
            new Song("Second", "b", singingOrderNumber: 2)
        };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var lines = LeafletBuilder.Build(request).ChapterFiles.Single().Content.Split('\n');

        var second = Array.FindIndex(lines, l => l.Contains("2. Second"));
        Assert.That(lines[second - 1], Is.EqualTo("\\mbox{}\\\\"));
        var first = Array.FindIndex(lines, l => l.Contains("1. First"));
        Assert.That(lines[first - 1], Does.Not.Contain("\\mbox{}"));
    }


    [Test]
    public void Build_MultiColumn_CountsABlankInputLineAsOneVisualLine_WhenPackingColumns()
    {
        // 21 text lines + 20 blank separators + title = 42 visual lines, which fits one column of
        // page 1 (53 minus the heading) — counted double it would not (62). The second song must start the RIGHT column (blank lines
        // render as one visual line each, not two). Placed second so page 1's chapter-heading
        // reservation is irrelevant to the song that is checked.
        var stanzas = string.Join("\n\n", Enumerable.Repeat("line", 21));
        var songs = new[]
        {
            new Song("Spacious", stanzas, singingOrderNumber: 1),
            new Song("Next", "tail\ntail\ntail", singingOrderNumber: 2)
        };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var content = LeafletBuilder.Build(request).ChapterFiles.Single().Content;

        var gutter = content.IndexOf("\\hspace{0.075\\textwidth}", StringComparison.Ordinal);
        Assert.That(gutter, Is.GreaterThan(0));
        Assert.That(content.IndexOf("Next", StringComparison.Ordinal), Is.GreaterThan(gutter));
        Assert.That(content.LastIndexOf("line\\\\", StringComparison.Ordinal), Is.LessThan(gutter));
    }

    [Test]
    public void Build_MultiColumn_ReservesTheChapterHeadingsHeightOnTheChaptersFirstPage()
    {
        // 49 visual lines fit a full 53-line column, but not page 1's column once the chapter
        // heading has taken its share — so the song must continue into the right column.
        var song = new Song("Tall", string.Join("\n", Enumerable.Repeat("line", 48)), singingOrderNumber: 1);
        var request = new LeafletRequest("Test", OneChapter(song), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var content = LeafletBuilder.Build(request).ChapterFiles.Single().Content;

        Assert.That(content, Does.Contain("\\hspace{0.075\\textwidth}"));
    }
    [Test]
    public void Build_RejectsAnEmptySongList()
    {
        Assert.Throws<ArgumentException>(() =>
            new LeafletRequest("Empty", Array.Empty<LeafletChapter>(), LeafletFormat.SingleColumn, OrderingMode.AsListed));
    }

    [Test]
    public void Build_RejectsChaptersThatAreAllEmpty()
    {
        var chapters = new[] { new LeafletChapter("main", null, Array.Empty<Song>()) };

        Assert.Throws<ArgumentException>(() =>
            new LeafletRequest("Empty", chapters, LeafletFormat.SingleColumn, OrderingMode.AsListed));
    }

    [Test]
    public void Build_FailsDescriptively_WhenTemplateIsMissing()
    {
        var request = new LeafletRequest("Test", OneChapter(TwoRealSongs()), LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            LeafletBuilder.Build(request, templatePath: Path.Combine(AppContext.BaseDirectory, "does-not-exist.tex")));

        Assert.That(ex!.Message, Does.Contain("does-not-exist.tex"));
    }

    [Test]
    public void Build_IsDeterministic_AcrossIdenticalCalls()
    {
        var request1 = new LeafletRequest("Sommarvisor", OneChapter(TwoRealSongs()), LeafletFormat.MultiColumn, OrderingMode.AsListed);
        var request2 = new LeafletRequest("Sommarvisor", OneChapter(TwoRealSongs()), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var first = LeafletBuilder.Build(request1);
        var second = LeafletBuilder.Build(request2);

        Assert.That(first.Document, Is.EqualTo(second.Document));
        Assert.That(first.ChapterFiles.Single().Content, Is.EqualTo(second.ChapterFiles.Single().Content));
    }

    [Test]
    public void Build_SpaceEfficient_CanProduceADifferentPhysicalOrderThanAsListed()
    {
        // Heights (title + lines): Big=52, Small=2, C=52, D=2. The real template's LinesPerPage
        // is 53, so 2 columns hold 106 — but the total (108) can't fit in 2, making 3 the true
        // minimum, achievable only by pairing the two small songs (Small, D) into one column.
        // As-listed order [Big,Small,C,D] does not achieve this (needs 4 columns) — space-efficient
        // ordering must find an arrangement that does (research.md §4/§5, `/speckit-analyze`
        // finding U1's real column-fill cost function).
        var songs = new[]
        {
            new Song("Big", string.Join("\n", Enumerable.Repeat("line", 51)), singingOrderNumber: 1),
            new Song("Small", "line", singingOrderNumber: 2),
            new Song("C", string.Join("\n", Enumerable.Repeat("line", 51)), singingOrderNumber: 3),
            new Song("D", "line", singingOrderNumber: 4)
        };

        var asListed = LeafletBuilder.Build(new LeafletRequest("T", OneChapter(songs), LeafletFormat.SingleColumn, OrderingMode.AsListed));
        var spaceEfficient = LeafletBuilder.Build(new LeafletRequest("T", OneChapter(songs), LeafletFormat.SingleColumn, OrderingMode.SpaceEfficient));

        var asListedOrder = asListed.Songs.Select(p => p.Song.Title).ToList();
        var spaceEfficientOrder = spaceEfficient.Songs.Select(p => p.Song.Title).ToList();
        Assert.That(spaceEfficientOrder, Is.Not.EqualTo(asListedOrder));
        // Whatever order was chosen, Small and D must be adjacent (the only way to reach 3 columns).
        var smallIndex = spaceEfficientOrder.IndexOf("Small");
        var dIndex = spaceEfficientOrder.IndexOf("D");
        Assert.That(Math.Abs(smallIndex - dIndex), Is.EqualTo(1));
        // Regardless of physical order, singing-order numbers must still match their songs.
        Assert.That(spaceEfficient.Songs.First(p => p.Song.Title == "Big").Song.SingingOrderNumber, Is.EqualTo(1));
    }

    [Test]
    public void Build_RegressionCheck_EverySongsSingingOrderNumberSurvivesBothOrderingModesIntact()
    {
        var songs = new[]
        {
            new Song("Big", string.Join("\n", Enumerable.Repeat("line", 10)), singingOrderNumber: 42),
            new Song("Small", "line", singingOrderNumber: 7),
            new Song("C", string.Join("\n", Enumerable.Repeat("line", 2)), singingOrderNumber: 13),
            new Song("D", string.Join("\n", Enumerable.Repeat("line", 9)), singingOrderNumber: 99)
        };
        var expectedByTitle = songs.ToDictionary(s => s.Title, s => s.SingingOrderNumber);

        var asListed = LeafletBuilder.Build(new LeafletRequest("T", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.AsListed));
        var spaceEfficient = LeafletBuilder.Build(new LeafletRequest("T", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.SpaceEfficient));

        foreach (var placed in asListed.Songs.Concat(spaceEfficient.Songs))
        {
            Assert.That(placed.Song.SingingOrderNumber, Is.EqualTo(expectedByTitle[placed.Song.Title]),
                $"{placed.Song.Title}'s singing-order number must never be derived from PhysicalPosition.");
        }
    }

    [Test]
    public void Build_DisplaysEachSongsSingingOrderNumber_AlongsideItsTitle()
    {
        var songs = new[]
        {
            new Song("First", "some lyrics", singingOrderNumber: 7),
            new Song("Second", "more lyrics", singingOrderNumber: 3)
        };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);
        var chapterContent = leaflet.ChapterFiles.Single().Content;

        Assert.That(chapterContent, Does.Contain("7. First"));
        Assert.That(chapterContent, Does.Contain("3. Second"));
    }

    [Test]
    public void Build_EachChapterBecomesItsOwnChapterFile_WithAChapterHeading_AndAnInputLineInMainTex()
    {
        var chapters = new[]
        {
            new LeafletChapter("first", "First Chapter", new[] { new Song("A", "lyrics a", singingOrderNumber: 1) }),
            new LeafletChapter("second", "Second Chapter", new[] { new Song("B", "lyrics b", singingOrderNumber: 2) })
        };
        var request = new LeafletRequest("Test", chapters, LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);

        Assert.That(leaflet.ChapterFiles, Has.Exactly(2).Items);
        Assert.That(leaflet.ChapterFiles[0].ChapterName, Is.EqualTo("first"));
        Assert.That(leaflet.ChapterFiles[0].Content, Does.Contain("\\chapter*{First Chapter}"));
        Assert.That(leaflet.ChapterFiles[0].Content, Does.Contain("lyrics a"));
        Assert.That(leaflet.ChapterFiles[1].ChapterName, Is.EqualTo("second"));
        Assert.That(leaflet.ChapterFiles[1].Content, Does.Contain("\\chapter*{Second Chapter}"));
        Assert.That(leaflet.ChapterFiles[1].Content, Does.Contain("lyrics b"));

        Assert.That(leaflet.Document, Does.Contain("\\input{Chapters/first/chapter.tex}"));
        Assert.That(leaflet.Document, Does.Contain("\\input{Chapters/second/chapter.tex}"));
        // The main document never embeds song content directly.
        Assert.That(leaflet.Document, Does.Not.Contain("lyrics a"));
        Assert.That(leaflet.Document, Does.Not.Contain("lyrics b"));
    }

    [Test]
    public void Build_FallsBackToChapterName_WhenTheChapterHasNoTitle()
    {
        var chapters = new[] { new LeafletChapter("main", null, new[] { new Song("A", "lyrics", singingOrderNumber: 1) }) };
        var request = new LeafletRequest("Test", chapters, LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);

        Assert.That(leaflet.ChapterFiles.Single().Content, Does.Contain("\\chapter*{main}"));
    }

    [Test]
    public void Build_SkipsAnEmptyChapterEntirely_WhenAnotherChapterHasSongs()
    {
        var chapters = new[]
        {
            new LeafletChapter("empty", "Empty Chapter", Array.Empty<Song>()),
            new LeafletChapter("full", "Full Chapter", new[] { new Song("A", "lyrics", singingOrderNumber: 1) })
        };
        var request = new LeafletRequest("Test", chapters, LeafletFormat.SingleColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);

        Assert.That(leaflet.ChapterFiles, Has.Exactly(1).Items);
        Assert.That(leaflet.ChapterFiles[0].ChapterName, Is.EqualTo("full"));
        Assert.That(leaflet.Document, Does.Not.Contain("Chapters/empty/"));
    }

    [Test]
    public void Build_DoesNotPairSongsAcrossAChapterBoundary_InMultiColumn()
    {
        // A single song per chapter in MultiColumn must render single-column within its own
        // chapter.tex (no minipage), since there is no second song in the SAME chapter to pair
        // with — pairing across chapters would straddle a \chapter heading, which is nonsensical.
        var chapters = new[]
        {
            new LeafletChapter("first", null, new[] { new Song("A", "lyrics a", singingOrderNumber: 1) }),
            new LeafletChapter("second", null, new[] { new Song("B", "lyrics b", singingOrderNumber: 2) })
        };
        var request = new LeafletRequest("Test", chapters, LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request);

        Assert.That(leaflet.ChapterFiles[0].Content, Does.Not.Contain("minipage"));
        Assert.That(leaflet.ChapterFiles[1].Content, Does.Not.Contain("minipage"));
    }

    private static string SmallPageTemplatePath() => TestFixtures.Path("TeX", "small-page-template.tex"); // LinesPerPage = 5

    [Test]
    public void Build_MultiColumn_StacksMultipleShortSongsIntoTheSameColumn_WhenTheyFitTogether()
    {
        // Regression check (`/speckit-analyze` finding U1): sequential column-fill, not
        // one-song-per-column-per-row — proves more than two songs can share one page's columns.
        // A and B both stack into the left column (their combined height fits); C starts the
        // right column. Every song's title and lyrics render together in the same minipage
        // (2026-09-27: title/lyrics separation reverted, direct instruction — reverting the
        // 2026-09-27 split that matched test.tex/`Kottar i karburatorn.tex`).
        var songs = new[]
        {
            new Song("A", "aline", singingOrderNumber: 1), // height 2 (1 title + 1 line)
            new Song("B", "bline", singingOrderNumber: 2), // height 2
            new Song("C", "cline", singingOrderNumber: 3)  // height 2
        };
        var request = new LeafletRequest("Test", OneChapter(songs), LeafletFormat.MultiColumn, OrderingMode.AsListed);

        var leaflet = LeafletBuilder.Build(request, templatePath: SmallPageTemplatePath());
        var content = leaflet.ChapterFiles.Single().Content;

        var leftMinipageEnd = content.IndexOf("\\end{minipage}", StringComparison.Ordinal);
        var leftMinipage = content[..leftMinipageEnd];
        var rightMinipage = content[leftMinipageEnd..];

        Assert.That(leftMinipage, Does.Contain("1. A"));
        Assert.That(leftMinipage, Does.Contain("2. B"), "A and B both stack into the same left-column minipage");
        Assert.That(rightMinipage, Does.Contain("3. C"));
    }

    [Test]
    public void Build_MultiColumn_LongSongNeverTruncated_AndForcesABreak_WhenItWouldOtherwiseStartMidAnEvenPage()
    {
        // A(5) and B(5) exactly fill page 1's two columns. C(2) then starts page 2 (EVEN),
        // partially filling its left column. D, a long song (21 > LinesPerPage 5), would next
        // continue filling page 2's left column right after C — since page 2 is EVEN, FR-025
        // forces it onto a fresh page instead, leaving C alone on page 2.
        var a = new Song("SongA", string.Join("\n", Enumerable.Repeat("aline", 4)), singingOrderNumber: 1); // height 5
        var b = new Song("SongB", string.Join("\n", Enumerable.Repeat("bline", 4)), singingOrderNumber: 2); // height 5
        var c = new Song("SongC", "cline", singingOrderNumber: 3); // height 2
        var longLines = Enumerable.Range(1, 20).Select(i => $"dline{i}").ToArray();
        var d = new Song("SongD", string.Join("\n", longLines), singingOrderNumber: 4); // height 21

        var request = new LeafletRequest("Test", OneChapter(a, b, c, d), LeafletFormat.MultiColumn, OrderingMode.AsListed);
        var leaflet = LeafletBuilder.Build(request, templatePath: SmallPageTemplatePath());
        var content = leaflet.ChapterFiles.Single().Content;

        // FR-025: never truncated/clipped, however many pages it takes.
        foreach (var line in longLines)
            Assert.That(content, Does.Contain(line));

        // FR-006 (closes `/speckit-analyze` finding G1): the forced break never reorders songs —
        // titles still appear in exactly their as-listed sequence.
        var positions = new[] { "1. SongA", "2. SongB", "3. SongC", "4. SongD" }
            .Select(title => content.IndexOf(title, StringComparison.Ordinal))
            .ToList();
        Assert.That(positions, Is.All.GreaterThanOrEqualTo(0));
        Assert.That(positions, Is.Ordered.Ascending);

        // FR-025 (finding U1): SongC, immediately before the forced break, renders alone (no
        // minipage) — proving the break excluded SongD from sharing that page with it, and that
        // the page wasn't reserved for SongD from the start (SongC still got to use it).
        var cIndex = content.IndexOf("3. SongC", StringComparison.Ordinal);
        var precedingNoindent = content.LastIndexOf("\\noindent", cIndex, StringComparison.Ordinal);
        Assert.That(content[precedingNoindent..cIndex], Does.Not.Contain("minipage"));
    }
}
