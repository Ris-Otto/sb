using NUnit.Framework;
using sb.Chapters;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb.Tests;

[TestFixture]
public class DocumentStoreTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-doc-store-tests").FullName;
        _index = new SbIndexDatabase(Path.Combine(_tempRoot, "index.sqlite"));
        _store = new DocumentStore(_index);
    }

    [TearDown]
    public void TearDown()
    {
        _index.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    private string NewDocPath(string name) => Path.Combine(_tempRoot, name);

    [Test]
    public void Create_WithZeroChaptersOrSongs_IsAValidPersistentDocument()
    {
        var doc = _store.Create("empty-doc", "Empty Document", NewDocPath("empty-doc"));

        Assert.That(doc.Chapters, Is.Empty);
        Assert.That(Directory.Exists(doc.Path), Is.True);
        Assert.That(_store.List().Select(d => d.Name), Contains.Item("empty-doc"));
    }

    [Test]
    public void Create_WritesATexFileNamedAfterName_Immediately_AndAnEmptyChaptersFolder_NoYmlFile()
    {
        var doc = _store.Create("tex-doc", "Tex Doc", NewDocPath("tex-doc"));

        var mainTexPath = Path.Combine(doc.Path, "tex-doc.tex");
        var chaptersPath = Path.Combine(doc.Path, "Chapters");

        Assert.That(File.Exists(mainTexPath), Is.True);
        Assert.That(Directory.Exists(chaptersPath), Is.True);
        Assert.That(File.ReadAllText(mainTexPath), Does.Contain("Tex Doc"));
        Assert.That(Directory.GetFiles(doc.Path, "*.yml"), Is.Empty, "a document is no longer stored as a .yml file (spec.md's 2026-09-26 Clarification)");
    }

    [Test]
    public void Rename_RenamesTheTexFileToMatchTheNewName()
    {
        var doc = _store.Create("old-name", "Old", NewDocPath("old-name"));

        _store.Rename("old-name", "new-name");

        Assert.That(File.Exists(Path.Combine(doc.Path, "old-name.tex")), Is.False);
        Assert.That(File.Exists(Path.Combine(doc.Path, "new-name.tex")), Is.True);
        Assert.That(_store.Find("new-name").Title, Is.EqualTo("Old"));
    }

    [Test]
    public void Create_WritesTheDocumentsIndexRow_WithPathAndFilename()
    {
        var path = NewDocPath("sommarvisor");

        var doc = _store.Create("sommarvisor", "Sommarvisor", path);

        Assert.That(doc.Path, Is.EqualTo(path));
        Assert.That(doc.Name, Is.EqualTo("sommarvisor"));
        Assert.That(doc.Id, Is.GreaterThan(0));
    }

    [Test]
    public void Create_Rejects_ADuplicatePath()
    {
        var path = NewDocPath("dup");
        _store.Create("dup", "Dup", path);

        Assert.Throws<InvalidOperationException>(() => _store.Create("dup2", "Dup 2", path));
    }

    [Test]
    public void Delete_UnknownName_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => _store.Delete("does-not-exist"));
    }

    [Test]
    public void Rename_UnknownName_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => _store.Rename("does-not-exist", "new-name"));
    }

    [Test]
    public void Delete_RemovesTheDocumentsFolderAndIndexRow()
    {
        var doc = _store.Create("to-delete", "To Delete", NewDocPath("to-delete"));

        _store.Delete(doc.Name);

        Assert.That(Directory.Exists(doc.Path), Is.False);
        Assert.That(_store.List().Select(d => d.Name), Does.Not.Contain("to-delete"));
    }

    [Test]
    public void Delete_WithChaptersAndSongsStillInIt_DoesNotViolateForeignKeyConstraints()
    {
        var doc = _store.Create("full-doc", "Full Doc", NewDocPath("full-doc"));
        var chapterStore = new ChapterStore(_index, _store);
        var songStore = new SongStore(_index, chapterStore, _store);
        chapterStore.Add("full-doc", "main");
        songStore.Add("full-doc", "main", "A Song", "lyrics", singingOrderNumber: 1);

        Assert.DoesNotThrow(() => _store.Delete("full-doc"));
        Assert.That(Directory.Exists(doc.Path), Is.False);
    }

    [Test]
    public void Rename_UpdatesTheDocumentsFilenameInTheIndex()
    {
        _store.Create("old-name", "Old", NewDocPath("old-name"));

        _store.Rename("old-name", "new-name");

        Assert.That(_store.List().Select(d => d.Name), Does.Not.Contain("old-name"));
        Assert.That(_store.List().Select(d => d.Name), Contains.Item("new-name"));
    }

    [Test]
    public void Create_PersistsTitleAndSubtitle_AcrossAFreshStoreInstance()
    {
        _store.Create("persisted", "Persisted Title", NewDocPath("persisted"), subtitle: "A Subtitle");

        var reloaded = new DocumentStore(_index);
        var found = reloaded.List().Single(d => d.Name == "persisted");

        Assert.That(found.Title, Is.EqualTo("Persisted Title"));
        Assert.That(found.Subtitle, Is.EqualTo("A Subtitle"));
    }

    [Test]
    public void Create_WithNoSubtitle_PersistsAsNull_AcrossAFreshStoreInstance()
    {
        _store.Create("no-subtitle", "No Subtitle Doc", NewDocPath("no-subtitle"));

        var reloaded = new DocumentStore(_index);
        var found = reloaded.List().Single(d => d.Name == "no-subtitle");

        Assert.That(found.Subtitle, Is.Null);
    }

    [Test]
    public void Create_PersistsATitleWithLatexSpecialCharacters_RoundTripsExactly()
    {
        _store.Create("special", "Rock & Roll #1", NewDocPath("special"), subtitle: "100% live_show");

        var reloaded = new DocumentStore(_index);
        var found = reloaded.List().Single(d => d.Name == "special");

        Assert.That(found.Title, Is.EqualTo("Rock & Roll #1"));
        Assert.That(found.Subtitle, Is.EqualTo("100% live_show"));
    }

    [Test]
    public void Create_PersistsANonAsciiTitleAndSubtitle_RoundTripsExactly_WithNoBomInTheTexFile()
    {
        // 003-utf8-encoding, research.md §3: the main .tex write uses UTF-8 without a BOM.
        var doc = _store.Create("sangblad", "Sångblad å ä ö", NewDocPath("sangblad"), subtitle: "En visbok från Ångermanland");

        var reloaded = new DocumentStore(_index);
        var found = reloaded.List().Single(d => d.Name == "sangblad");

        Assert.That(found.Title, Is.EqualTo("Sångblad å ä ö"));
        Assert.That(found.Subtitle, Is.EqualTo("En visbok från Ångermanland"));
        var firstBytes = File.ReadAllBytes(Path.Combine(doc.Path, "sangblad.tex")).Take(3).ToArray();
        Assert.That(firstBytes, Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }
}
