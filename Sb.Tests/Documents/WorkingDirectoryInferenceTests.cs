using NUnit.Framework;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Documents;

[TestFixture]
public class WorkingDirectoryInferenceTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private DocumentOperations _documents = null!;
    private ChapterOperations _chapters = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-cwd-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _documents = new DocumentOperations(_db);
        _chapters = new ChapterOperations(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    [Test]
    public void Infer_CwdIsDocumentFolder_ReturnsDocumentName_NoChapter()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var (documentName, chapterName) = WorkingDirectoryInference.Infer(_db, Path.Combine(_outputFolder, "my-doc"));

        Assert.That(documentName, Is.EqualTo("my-doc"));
        Assert.That(chapterName, Is.Null);
    }

    [Test]
    public void Infer_CwdIsSubfolderOfDocument_ReturnsDocumentName()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        string arbitrarySubfolder = Path.Combine(_outputFolder, "my-doc", "some-other-folder");
        Directory.CreateDirectory(arbitrarySubfolder);

        var (documentName, _) = WorkingDirectoryInference.Infer(_db, arbitrarySubfolder);

        Assert.That(documentName, Is.EqualTo("my-doc"));
    }

    [Test]
    public void Infer_CwdIsChapterFolder_ReturnsBothDocumentAndChapterName()
    {
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _chapters.Add("my-doc", "chapter-1", null);

        var (documentName, chapterName) = WorkingDirectoryInference.Infer(
            _db, Path.Combine(_outputFolder, "my-doc", "Chapters", "chapter-1"));

        Assert.That(documentName, Is.EqualTo("my-doc"));
        Assert.That(chapterName, Is.EqualTo("chapter-1"));
    }

    [Test]
    public void Infer_CwdIsSiblingDocumentSharingANamePrefix_DoesNotFalselyMatch()
    {
        // "my-doc" is a literal string prefix of "my-doc-extended", but they're sibling folders, not
        // nested — a naive StartsWith(ancestorPath) without requiring the separator afterward would
        // wrongly treat cwd as being inside "my-doc".
        _documents.Create("my-doc", "Title", null, _outputFolder);
        _documents.Create("my-doc-extended", "Other Title", null, _outputFolder);

        var (documentName, _) = WorkingDirectoryInference.Infer(_db, Path.Combine(_outputFolder, "my-doc-extended"));

        Assert.That(documentName, Is.EqualTo("my-doc-extended"));
    }

    [Test]
    public void Infer_CwdOutsideAnyDocument_ReturnsNullForBoth()
    {
        string unrelatedFolder = Path.Combine(_outputFolder, "unrelated");
        Directory.CreateDirectory(unrelatedFolder);
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var (documentName, chapterName) = WorkingDirectoryInference.Infer(_db, unrelatedFolder);

        Assert.That(documentName, Is.Null);
        Assert.That(chapterName, Is.Null);
    }

    [Test]
    public void Infer_CwdIsParentOfDocumentsFolder_DoesNotMatch()
    {
        // The output folder itself CONTAINS the document folder but isn't inside it — being one level
        // too shallow must not be treated as a match.
        _documents.Create("my-doc", "Title", null, _outputFolder);

        var (documentName, _) = WorkingDirectoryInference.Infer(_db, _outputFolder);

        Assert.That(documentName, Is.Null);
    }

    [Test]
    public void Infer_NoDocumentsAtAll_ReturnsNullForBoth()
    {
        var (documentName, chapterName) = WorkingDirectoryInference.Infer(_db, _outputFolder);

        Assert.That(documentName, Is.Null);
        Assert.That(chapterName, Is.Null);
    }
}
