using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class VerticalAlignmentTests
{
    [Test]
    public void Compute_EqualColumns_NoVSpaceNeeded()
    {
        var correction = VerticalAlignment.Compute(column1Lines: 10, column2Lines: 10);

        Assert.That(correction.Column1VSpace, Is.EqualTo(0));
        Assert.That(correction.Column2VSpace, Is.EqualTo(0));
    }

    [Test]
    public void Compute_Column1Shorter_VSpaceAddedToColumn1()
    {
        var correction = VerticalAlignment.Compute(column1Lines: 7, column2Lines: 10);

        Assert.That(correction.Column1VSpace, Is.EqualTo(3));
        Assert.That(correction.Column2VSpace, Is.EqualTo(0));
    }

    [Test]
    public void Compute_Column2Shorter_VSpaceAddedToColumn2()
    {
        var correction = VerticalAlignment.Compute(column1Lines: 10, column2Lines: 6);

        Assert.That(correction.Column1VSpace, Is.EqualTo(0));
        Assert.That(correction.Column2VSpace, Is.EqualTo(4));
    }
}
