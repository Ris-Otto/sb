using NUnit.Framework;
using Sb.Core.Storage;

namespace Sb.Tests.Storage;

[TestFixture]
public class MismatchDetectorTests
{
    [Test]
    public void FindDocumentMismatches_PathDoesNotExist_ReportsMismatchWithNameAndPath()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "sb-ghost-" + Guid.NewGuid());
        var record = new DocumentRecord(1, "ghost", missingPath, "Ghost", null);

        var mismatches = MismatchDetector.FindDocumentMismatches([record]);

        Assert.That(mismatches, Has.Count.EqualTo(1));
        Assert.That(mismatches[0].Name, Is.EqualTo("ghost"));
        Assert.That(mismatches[0].ExpectedPath, Is.EqualTo(missingPath));
    }

    [Test]
    public void FindDocumentMismatches_PathExists_ReportsNoMismatch()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sb-real-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var record = new DocumentRecord(1, "real", tempDir, "Real", null);

            var mismatches = MismatchDetector.FindDocumentMismatches([record]);

            Assert.That(mismatches, Is.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void FindChapterMismatches_ReportsOnlyChaptersWhoseFolderIsGone()
    {
        string documentPath = Path.Combine(Path.GetTempPath(), "sb-chapters-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(documentPath, "Chapters", "present"));
        try
        {
            var document = new DocumentRecord(1, "doc", documentPath, "Doc", null);
            var present = new ChapterRecord(1, 1, "present", "Present");
            var gone = new ChapterRecord(2, 1, "gone", "Gone");

            var mismatches = MismatchDetector.FindChapterMismatches(document, [present, gone]);

            Assert.That(mismatches, Has.Count.EqualTo(1));
            Assert.That(mismatches[0].Name, Is.EqualTo("gone"));
            Assert.That(mismatches[0].ExpectedPath, Is.EqualTo(Path.Combine(documentPath, "Chapters", "gone")));
        }
        finally
        {
            Directory.Delete(documentPath, recursive: true);
        }
    }

    [Test]
    public void FindSongMismatches_ReportsOnlySongsWhoseFileIsGone()
    {
        string documentPath = Path.Combine(Path.GetTempPath(), "sb-songs-" + Guid.NewGuid());
        string chapterFolder = Path.Combine(documentPath, "Chapters", "c");
        Directory.CreateDirectory(chapterFolder);
        File.WriteAllText(Path.Combine(chapterFolder, "Here.yml"), "x");
        try
        {
            var document = new DocumentRecord(1, "doc", documentPath, "Doc", null);
            var chapter = new ChapterRecord(1, 1, "c", "C");

            var mismatches = MismatchDetector.FindSongMismatches(
                document, chapter, [new SongRecord(1, 1, "Here", 1), new SongRecord(2, 1, "Gone", 2)]);

            Assert.That(mismatches.Select(m => m.Name), Is.EqualTo(new[] { "Gone" }));
        }
        finally
        {
            Directory.Delete(documentPath, recursive: true);
        }
    }

    [Test]
    public void CheckDocumentFolder_Missing_FailsWithStorageErrorNamingThePath()
    {
        string missing = Path.Combine(Path.GetTempPath(), "sb-ghost-" + Guid.NewGuid());

        var result = MismatchDetector.CheckDocumentFolder(new DocumentRecord(1, "ghost", missing, "Ghost", null));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(Sb.Core.ErrorKind.Storage));
        Assert.That(result.Error.Message, Does.Contain(missing));
    }
}
