using NUnit.Framework;
using sb.Chapters;
using sb.Documents;
using sb.Index;

namespace sb.Tests;

[TestFixture]
public class ChapterStoreTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _documents = null!;
    private ChapterStore _chapters = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-chapter-store-tests").FullName;
        _index = new SbIndexDatabase(Path.Combine(_tempRoot, "index.sqlite"));
        _documents = new DocumentStore(_index);
        _chapters = new ChapterStore(_index, _documents);
        _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
    }

    [TearDown]
    public void TearDown()
    {
        _index.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void Add_RejectsAnUnknownParentDocument()
    {
        Assert.Throws<InvalidOperationException>(() => _chapters.Add("does-not-exist", "main"));
    }

    [Test]
    public void Add_CreatesTheChapterFolderAndIndexRow()
    {
        var chapter = _chapters.Add("doc", "main");

        Assert.That(chapter.Name, Is.EqualTo("main"));
        Assert.That(_chapters.List("doc").Select(c => c.Name), Contains.Item("main"));
    }

    [Test]
    public void Add_RejectsADuplicateNameWithinTheSameDocument()
    {
        _chapters.Add("doc", "main");

        Assert.Throws<InvalidOperationException>(() => _chapters.Add("doc", "main"));
    }

    [Test]
    public void Add_ThenList_RoundTripsANonAsciiChapterTitle()
    {
        // 003-utf8-encoding, research.md §2/§3: explicit UTF-8 read/write for chapter.yml.
        _chapters.Add("doc", "main", "Sångblad å ä ö");

        var found = _chapters.List("doc").Single(c => c.Name == "main");

        Assert.That(found.Title, Is.EqualTo("Sångblad å ä ö"));
    }

    [Test]
    public void Remove_CascadesToDeleteAllSongsInTheChapter()
    {
        _chapters.Add("doc", "main");
        var songFile = Path.Combine(_tempRoot, "doc", "Chapters", "main", "placeholder.txt");
        File.WriteAllText(songFile, "placeholder");

        _chapters.Remove("doc", new[] { "main" });

        Assert.That(_chapters.List("doc"), Is.Empty);
        Assert.That(File.Exists(songFile), Is.False);
    }

    [Test]
    public void Remove_UnknownChapterName_RejectsWithoutRemovingAnything()
    {
        _chapters.Add("doc", "main");

        Assert.Throws<InvalidOperationException>(() => _chapters.Remove("doc", new[] { "main", "does-not-exist" }));

        Assert.That(_chapters.List("doc").Select(c => c.Name), Contains.Item("main"));
    }
}
