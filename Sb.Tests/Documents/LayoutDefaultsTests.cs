using NUnit.Framework;
using Sb.Core.Documents;
using Sb.Core.LaTeX;

namespace Sb.Tests.Documents;

/// <summary>The page geometry numbers (A-12) were derived from measurements of template-main.tex; these
/// pin the results, so a change to the constants or the formulas is a conscious one.</summary>
[TestFixture]
public class LayoutDefaultsTests
{
    [Test]
    public void LinesPerColumn_IsWorkedOutFromTheTemplateGeometry()
    {
        // (776.39pt text height + 5.05pt) / 14.4pt = 54.3 lines, less one line of safety.
        Assert.That(LayoutDefaults.LinesPerColumn, Is.EqualTo(53));
    }

    [Test]
    public void FirstPageLinesPerColumn_LeavesRoomForTheChapterHeading()
    {
        // (776.39pt - 135.72pt heading + 5.05pt) / 14.4pt = 44.8 lines, less one line of safety.
        Assert.That(LayoutDefaults.FirstPageLinesPerColumn, Is.EqualTo(43));
        Assert.That(LayoutDefaults.FirstPageLinesPerColumn, Is.LessThan(LayoutDefaults.LinesPerColumn));
    }

    [Test]
    public void ColumnWidth_IsTheCharacterCountTheMeasuredLyricsFit()
    {
        // 259.05pt column / 5.96pt widest measured per-character width = 43.5 characters.
        Assert.That(LayoutDefaults.ColumnWidth, Is.EqualTo(43));
    }

    [Test]
    public void TemplateStillHasTheGeometryTheNumbersWereMeasuredFrom()
    {
        string template = DocumentTemplate.Create("T", null).Value.Render();

        Assert.That(template, Does.Contain("a4paper, margin=0.4in, bmargin=0.65in, tmargin=0.3in"));
        Assert.That(template, Does.Contain("\\renewcommand\\normalsize{\\@setfontsize\\normalsize{12pt}{14.4pt}}"));
        Assert.That(template, Does.Contain("\\titlespacing*{\\paragraph}{0pt}{\\baselineskip}{0pt}"));
    }
}
