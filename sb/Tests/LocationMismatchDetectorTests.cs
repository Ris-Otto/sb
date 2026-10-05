using NUnit.Framework;
using sb.Chapters;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb.Tests;

[TestFixture]
public class LocationMismatchDetectorTests
{
    private string _tempRoot = null!;
    private SbIndexDatabase _index = null!;
    private DocumentStore _documents = null!;
    private ChapterStore _chapters = null!;
    private SongStore _songs = null!;
    private LocationMismatchDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("sb-mismatch-tests").FullName;
        _index = new SbIndexDatabase(Path.Combine(_tempRoot, "index.sqlite"));
        _documents = new DocumentStore(_index);
        _chapters = new ChapterStore(_index, _documents);
        _songs = new SongStore(_index, _chapters, _documents);
        _detector = new LocationMismatchDetector(_index);
    }

    [TearDown]
    public void TearDown()
    {
        _index.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void DetectAll_ReportsNothing_WhenEverythingMatchesTheFilesystem()
    {
        _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 1);

        Assert.That(_detector.DetectAll(), Is.Empty);
    }

    [Test]
    public void DetectAll_ReportsAMismatch_WhenADocumentsFolderIsDeletedOutsideSb()
    {
        var doc = _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));

        Directory.Delete(doc.Path, recursive: true);

        var mismatches = _detector.DetectAll();

        Assert.That(mismatches, Has.Exactly(1).Items);
        Assert.That(mismatches[0].EntityKind, Is.EqualTo(MismatchEntityKind.Document));
        Assert.That(mismatches[0].ExpectedPath, Is.EqualTo(doc.Path));
    }

    [Test]
    public void DetectAll_ReportsAMismatch_WhenAChaptersFolderIsMovedOutsideSb()
    {
        _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        var chapterPath = Path.Combine(_tempRoot, "doc", "Chapters", "main");

        Directory.Move(chapterPath, chapterPath + "-moved");

        var mismatches = _detector.DetectAll();

        Assert.That(mismatches.Select(m => m.EntityKind), Contains.Item(MismatchEntityKind.Chapter));
    }

    [Test]
    public void DetectAll_ReportsASongNumberMismatch_WhenTheYmlFileIsHandEditedOutsideSb()
    {
        _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
        _chapters.Add("doc", "main");
        _songs.Add("doc", "main", "A Song", "lyrics", singingOrderNumber: 5);

        var chapterPath = Path.Combine(_tempRoot, "doc", "Chapters", "main");
        var songYmlPath = Directory.GetFiles(chapterPath, "*.yml").Single();
        var content = File.ReadAllText(songYmlPath).Replace("Number: 5", "Number: 999");
        File.WriteAllText(songYmlPath, content);

        var mismatches = _detector.DetectAll();

        Assert.That(mismatches, Has.Exactly(1).Items);
        Assert.That(mismatches[0].EntityKind, Is.EqualTo(MismatchEntityKind.SongNumber));
        Assert.That(mismatches[0].Detail, Does.Contain("5").And.Contain("999"));
    }

    [Test]
    public void DetectAll_DoesNotAutoRepairOrThrow_ItOnlyReports()
    {
        var doc = _documents.Create("doc", "Doc", Path.Combine(_tempRoot, "doc"));
        Directory.Delete(doc.Path, recursive: true);

        Assert.DoesNotThrow(() => _detector.DetectAll());
        Assert.That(Directory.Exists(doc.Path), Is.False, "detection must not recreate the missing folder");
    }
}
