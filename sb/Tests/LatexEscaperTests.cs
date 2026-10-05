using NUnit.Framework;
using sb.Formatting;

namespace sb.Tests;

[TestFixture]
public class LatexEscaperTests
{
    [TestCase("%", "\\%")]
    [TestCase("&", "\\&")]
    [TestCase("#", "\\#")]
    [TestCase("_", "\\_")]
    [TestCase("{", "\\{")]
    [TestCase("}", "\\}")]
    [TestCase("~", "\\textasciitilde{}")]
    [TestCase("^", "\\textasciicircum{}")]
    [TestCase("\\", "\\textbackslash{}")]
    public void Escape_HandlesEachReservedCharacter(string input, string expected)
    {
        Assert.That(LatexEscaper.Escape(input), Is.EqualTo(expected));
    }

    [Test]
    public void Escape_HandlesMultipleReservedCharactersInOneString()
    {
        var result = LatexEscaper.Escape("100% off & no_refunds #deal");

        Assert.That(result, Is.EqualTo("100\\% off \\& no\\_refunds \\#deal"));
    }

    [Test]
    public void Escape_DoesNotDoubleEscapeTheBackslashesItInserts()
    {
        var result = LatexEscaper.Escape("50% & 50%");

        Assert.That(result, Is.EqualTo("50\\% \\& 50\\%"));
        Assert.That(result, Does.Not.Contain("\\\\%"));
    }

    [Test]
    public void Escape_LeavesOrdinaryTextUnchanged()
    {
        Assert.That(LatexEscaper.Escape("Idas sommarvisa"), Is.EqualTo("Idas sommarvisa"));
    }

    [TestCase("%", "\\%")]
    [TestCase("&", "\\&")]
    [TestCase("#", "\\#")]
    [TestCase("_", "\\_")]
    [TestCase("{", "\\{")]
    [TestCase("}", "\\}")]
    [TestCase("~", "\\textasciitilde{}")]
    [TestCase("^", "\\textasciicircum{}")]
    [TestCase("\\", "\\textbackslash{}")]
    public void Unescape_ReversesEachReservedCharacter(string raw, string escaped)
    {
        Assert.That(LatexEscaper.Unescape(escaped), Is.EqualTo(raw));
    }

    [TestCase("Idas sommarvisa")]
    [TestCase("100% off & no_refunds #deal")]
    [TestCase("Rock & Roll #1 {feat. someone} ~ 100%")]
    public void Unescape_ReversesEscape_ForArbitraryText(string raw)
    {
        Assert.That(LatexEscaper.Unescape(LatexEscaper.Escape(raw)), Is.EqualTo(raw));
    }
}
