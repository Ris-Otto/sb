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
        var record = new DocumentRecord(1, "ghost", missingPath);

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
            var record = new DocumentRecord(1, "real", tempDir);

            var mismatches = MismatchDetector.FindDocumentMismatches([record]);

            Assert.That(mismatches, Is.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
