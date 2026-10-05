using NUnit.Framework;
using sb.Formatting;
using sb.Leaflets;

namespace sb.Tests;

[TestFixture]
public class DocumentTemplateTests
{
    private static string TemplatePath => Path.Combine(AppContext.BaseDirectory, "template-main.tex");

    [Test]
    public void Render_ThenParseTitleAndSubtitle_RoundTripsExactly()
    {
        var template = DocumentTemplate.Load(TemplatePath);
        var rendered = template.Render(LatexEscaper.Escape("Rock & Roll #1"), LatexEscaper.Escape("100% live_show"), content: "");

        var (title, subtitle) = DocumentTemplate.ParseTitleAndSubtitle(rendered);

        Assert.That(title, Is.EqualTo("Rock & Roll #1"));
        Assert.That(subtitle, Is.EqualTo("100% live_show"));
    }

    [Test]
    public void Render_WithNoSubtitle_ParsesBackAsNull()
    {
        var template = DocumentTemplate.Load(TemplatePath);
        var rendered = template.Render(LatexEscaper.Escape("No Subtitle"), null, content: "");

        var (title, subtitle) = DocumentTemplate.ParseTitleAndSubtitle(rendered);

        Assert.That(title, Is.EqualTo("No Subtitle"));
        Assert.That(subtitle, Is.Null);
    }

    [Test]
    public void Load_TheRealTemplate_SetsParindentToZero()
    {
        // Regression coverage for a real-world bug found via manual testing (C:\Test,
        // testfiles.bat): LaTeX's minipage internally resets \parindent to 0 for everything
        // inside it (via \@parboxrestore), but content that ever lands outside a minipage — the
        // single-column fallback for a page/song with no sibling column, or a whole document built
        // in single-column format — sits in the main document flow, where \parindent is whatever
        // the class default is (1.5em for `book`). Without an explicit reset, a stanza-break
        // paragraph in that fallback path renders with a visible first-line indent inconsistent
        // with the rest of the leaflet (verified against a real compiled PDF).
        var template = DocumentTemplate.Load(TemplatePath);

        Assert.That(template.RawTemplate, Does.Contain("\\setlength{\\parindent}{0pt}"));
    }

    [Test]
    public void Load_ReadsTheTemplateFileAsUtf8_PreservingNonAsciiCharactersInTheFileItself()
    {
        // 003-utf8-encoding, research.md §2: explicit UTF-8 read. Uses a temp copy of the real
        // template with a non-ASCII marker line added, so this actually exercises the file-read
        // path (the real template-main.tex has no non-ASCII content of its own to round-trip).
        var tempPath = Path.Combine(Path.GetTempPath(), $"sb-template-utf8-{Guid.NewGuid():N}.tex");
        File.WriteAllText(tempPath, File.ReadAllText(TemplatePath) + "\n% Sångblad å ä ö\n");
        try
        {
            var template = DocumentTemplate.Load(tempPath);

            Assert.That(template.RawTemplate, Does.Contain("Sångblad å ä ö"));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
