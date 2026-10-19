using NUnit.Framework;
using sb.Leaflets;

namespace sb.Tests;

[TestFixture]
public class PageMetricsReaderTests
{
    private const double PtPerInch = 72.27;

    [Test]
    public void Read_ParsesTheRealTemplate_IntoTheExpectedMetrics()
    {
        var rawTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "template-main.tex"));

        var metrics = PageMetricsReader.Read(rawTemplate, "template-main.tex");

        Assert.That(metrics.BaselineSkipPt, Is.EqualTo(14.4));
        Assert.That(metrics.TopMarginPt, Is.EqualTo(0.3 * PtPerInch).Within(0.001));
        Assert.That(metrics.BottomMarginPt, Is.EqualTo(0.65 * PtPerInch).Within(0.001));
        Assert.That(metrics.PaperHeightPt, Is.EqualTo(845.05).Within(0.01));
        Assert.That(metrics.LinesPerPage, Is.EqualTo((int)Math.Floor(metrics.UsableHeightPt / metrics.BaselineSkipPt)));
        Assert.That(metrics.LinesPerPage, Is.GreaterThan(0));
    }


    [Test]
    public void Read_ReservesALeadingChapterHeadingHeight_FromTheRealTemplate()
    {
        var rawTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "template-main.tex"));

        var metrics = PageMetricsReader.Read(rawTemplate, "template-main.tex");

        Assert.That(metrics.ChapterHeadingLines, Is.GreaterThan(0));
        Assert.That(metrics.ChapterHeadingLines, Is.LessThan(metrics.LinesPerPage));
    }

    [Test]
    public void TheRealTemplate_AppliesItsFontsizeViaSelectfont_SoTheAssumedBaselineIsReal()
    {
        // `\fontsize{..}{..}` alone is ignored by LaTeX until `\selectfont` follows it.
        var rawTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "template-main.tex"));

        Assert.That(rawTemplate, Does.Contain("\\fontsize{12pt}{14.4pt}\\selectfont"));
    }

    [Test]
    public void Read_ReservesNoChapterHeadingLines_WhenTheTemplateDoesNotDeclareAny()
    {
        var rawTemplate = "\\usepackage[a4paper, margin=0.4in]{geometry}\n\\fontsize{12pt}{14.4pt}";

        Assert.That(PageMetricsReader.Read(rawTemplate, "t.tex").ChapterHeadingLines, Is.EqualTo(0));
    }

    [Test]
    public void Read_FallsBackToTheSingleMarginOption_WhenTmarginAndBmarginAreAbsent()
    {
        var rawTemplate = "\\usepackage[a4paper, margin=0.4in]{geometry}\n\\fontsize{12pt}{14.4pt}";

        var metrics = PageMetricsReader.Read(rawTemplate, "test.tex");

        Assert.That(metrics.TopMarginPt, Is.EqualTo(0.4 * PtPerInch).Within(0.001));
        Assert.That(metrics.BottomMarginPt, Is.EqualTo(0.4 * PtPerInch).Within(0.001));
    }

    [Test]
    public void Read_FailsDescriptively_WhenTheGeometryPackageIsMissing()
    {
        var rawTemplate = "\\fontsize{12pt}{14.4pt}";

        var ex = Assert.Throws<InvalidOperationException>(() => PageMetricsReader.Read(rawTemplate, "bad.tex"));

        Assert.That(ex!.Message, Does.Contain("bad.tex"));
        Assert.That(ex.Message, Does.Contain("geometry"));
    }

    [Test]
    public void Read_FailsDescriptively_WhenFontSizeIsMissing()
    {
        var rawTemplate = "\\usepackage[a4paper, margin=0.4in]{geometry}";

        var ex = Assert.Throws<InvalidOperationException>(() => PageMetricsReader.Read(rawTemplate, "bad.tex"));

        Assert.That(ex!.Message, Does.Contain("bad.tex"));
        Assert.That(ex.Message, Does.Contain("fontsize"));
    }
}
