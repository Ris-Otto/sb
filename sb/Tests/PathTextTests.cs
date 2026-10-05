using NUnit.Framework;
using sb.Formatting;

namespace sb.Tests;

[TestFixture]
public class PathTextTests
{
    [Test]
    public void NormalizeForFileSystem_LeavesAnAlreadyNfcNormalizedPathUnchanged()
    {
        // "å" as a single precomposed code point (U+00E5) — NFC, matching this project's own
        // Swedish fixture filenames.
        var nfc = "hon_var_så_liten.txt";

        var result = PathText.NormalizeForFileSystem(nfc);

        Assert.That(result, Is.EqualTo(nfc));
    }

    [Test]
    public void NormalizeForFileSystem_NormalizesAnNfdDecomposedPath_ToMatchTheNfcForm()
    {
        // "å" as a base letter "a" (U+0061) plus a combining ring above (U+030A) — NFD.
        var nfd = "hon_var_så_liten.txt";
        var nfc = "hon_var_så_liten.txt";

        var result = PathText.NormalizeForFileSystem(nfd);

        Assert.That(result, Is.EqualTo(nfc));
    }
}
