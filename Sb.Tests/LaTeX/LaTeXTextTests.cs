using NUnit.Framework;
using Sb.Core.LaTeX;

namespace Sb.Tests.LaTeX;

[TestFixture]
public class LaTeXTextTests
{
    [Test]
    public void Escape_PlainText_Unchanged()
    {
        Assert.That(LaTeXText.Escape("Lille Albin"), Is.EqualTo("Lille Albin"));
    }

    [TestCase("#", "\\#")]
    [TestCase("$", "\\$")]
    [TestCase("%", "\\%")]
    [TestCase("&", "\\&")]
    [TestCase("_", "\\_")]
    [TestCase("{", "\\{")]
    [TestCase("}", "\\}")]
    [TestCase("~", "\\textasciitilde{}")]
    [TestCase("^", "\\textasciicircum{}")]
    [TestCase("\\", "\\textbackslash{}")]
    public void Escape_SpecialCharacter_IsEscaped(string input, string expected)
    {
        Assert.That(LaTeXText.Escape(input), Is.EqualTo(expected));
    }

    [Test]
    public void Escape_RealisticTextWithMultipleSpecialCharacters()
    {
        string result = LaTeXText.Escape("Rock & Roll ~ 100% love_song");

        Assert.That(result, Is.EqualTo(
            "Rock \\& Roll \\textasciitilde{} 100\\% love\\_song"));
    }

    [Test]
    public void Escape_BackslashIsNotDoubleEscaped()
    {
        // A naive sequence of string.Replace calls would re-escape the backslash it just inserted
        // for an earlier character (e.g. escaping '&' to '\&' and then escaping that '\' again).
        string result = LaTeXText.Escape("a & b");

        Assert.That(result, Is.EqualTo("a \\& b"));
    }

    [Test]
    public void Escape_EmptyString_ReturnsEmptyString()
    {
        Assert.That(LaTeXText.Escape(""), Is.EqualTo(""));
    }

    [Test]
    public void Escape_SwedishCharacters_PassedThroughUnescaped()
    {
        // å/ä/ö are not LaTeX-special; they pass through for the UTF-8-capable engine to render.
        Assert.That(LaTeXText.Escape("Sångblad är bäst"), Is.EqualTo("Sångblad är bäst"));
    }
}
