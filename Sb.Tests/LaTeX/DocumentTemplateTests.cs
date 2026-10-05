using NUnit.Framework;
using Sb.Core;
using Sb.Core.LaTeX;

namespace Sb.Tests.LaTeX;

[TestFixture]
public class DocumentTemplateTests
{
    [Test]
    public void Create_RendersGivenTitleAndSubtitle()
    {
        var template = DocumentTemplate.Create("Studentvisor", "En samling sånger").Value;

        string rendered = template.Render();

        Assert.That(rendered, Does.Contain("Studentvisor"));
        Assert.That(rendered, Does.Contain("En samling sånger"));
    }

    [Test]
    public void Create_NoSubtitle_RendersWithoutLeavingPlaceholder()
    {
        var template = DocumentTemplate.Create("Studentvisor", null).Value;

        string rendered = template.Render();

        Assert.That(rendered, Does.Not.Contain("{subtitle}"));
    }

    [Test]
    public void Create_EscapesLatexSpecialCharactersInTitle()
    {
        var template = DocumentTemplate.Create("Rock & Roll", null).Value;

        string rendered = template.Render();

        Assert.That(rendered, Does.Contain("Rock \\& Roll"));
        Assert.That(rendered, Does.Not.Contain("Rock & Roll"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_BlankTitle_Fails(string title)
    {
        var result = DocumentTemplate.Create(title, null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void WithChapterAdded_InsertsInputLineForChapter()
    {
        var template = DocumentTemplate.Create("Studentvisor", null).Value
            .WithChapterAdded("Chapters/Forsta/Forsta.tex");

        string rendered = template.Render();

        Assert.That(rendered, Does.Contain("\\input{Chapters/Forsta/Forsta.tex}"));
    }

    [Test]
    public void WithChapterAdded_Twice_InsertsBothWithoutDisturbingTheFirst()
    {
        var template = DocumentTemplate.Create("Studentvisor", null).Value
            .WithChapterAdded("Chapters/Forsta/Forsta.tex")
            .WithChapterAdded("Chapters/Andra/Andra.tex");

        string rendered = template.Render();

        Assert.That(rendered, Does.Contain("\\input{Chapters/Forsta/Forsta.tex}"));
        Assert.That(rendered, Does.Contain("\\input{Chapters/Andra/Andra.tex}"));
        int firstIndex = rendered.IndexOf("\\input{Chapters/Forsta/Forsta.tex}", StringComparison.Ordinal);
        int secondIndex = rendered.IndexOf("\\input{Chapters/Andra/Andra.tex}", StringComparison.Ordinal);
        Assert.That(firstIndex, Is.LessThan(secondIndex), "chapters must appear in the order they were added");
    }

    [Test]
    public void Render_PreservesA4PageGeometryFromTheTemplate()
    {
        // FR-1.12.4: page metrics are fixed in template-main.tex (confirmed against PageMetrics.png
        // when it was authored); DocumentTemplate must preserve them verbatim, not recompute them.
        var template = DocumentTemplate.Create("Studentvisor", null).Value;

        string rendered = template.Render();

        Assert.That(rendered, Does.Contain("a4paper"));
        Assert.That(rendered, Does.Contain("geometry"));
    }

    [Test]
    public void Render_DoesNotRequireAnEngineSpecificPackage()
    {
        // FR-1.12.9: no hard-coded assumption of XeLaTeX/LuaLaTeX (e.g. fontspec/unicode-math),
        // which pdfLaTeX can't process.
        var template = DocumentTemplate.Create("Studentvisor", null).Value;

        string rendered = template.Render();

        Assert.That(rendered, Does.Not.Contain("fontspec"));
        Assert.That(rendered, Does.Not.Contain("unicode-math"));
        Assert.That(rendered, Does.Not.Contain("polyglossia"));
    }

    [Test]
    public void Render_DocumentsTheUtf8CompilationRequirement()
    {
        // FR-1.12.9: since SB doesn't enforce an engine, it documents the requirement instead, as a
        // comment in the generated preamble.
        var template = DocumentTemplate.Create("Studentvisor", null).Value;

        string rendered = template.Render();

        int commentIndex = rendered.IndexOf("% Compile with a UTF-8-capable", StringComparison.Ordinal);
        int documentIndex = rendered.IndexOf("\\begin{document}", StringComparison.Ordinal);
        Assert.That(commentIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(commentIndex, Is.LessThan(documentIndex));
    }
}
