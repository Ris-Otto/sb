using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class ResultTests
{
    [Test]
    public void Success_IsSuccessTrue_ErrorNull()
    {
        var result = Result.Success();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.IsFailure, Is.False);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public void Failure_IsSuccessFalse_ErrorPopulated()
    {
        var error = new Error(ErrorKind.Validation, "bad input");

        var result = Result.Failure(error);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo(error));
    }

    [Test]
    public void GenericSuccess_CarriesValue()
    {
        var result = Result<int>.Success(42);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
    }

    [Test]
    public void GenericFailure_AccessingValue_Throws()
    {
        var result = Result<int>.Failure(new Error(ErrorKind.NotFound, "not found", "Foo"));

        Assert.That(result.IsSuccess, Is.False);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Test]
    public void Error_CarriesKindMessageAndSubjectName()
    {
        var error = new Error(ErrorKind.Conflict, "name taken", "Psalm 134");

        Assert.That(error.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(error.Message, Is.EqualTo("name taken"));
        Assert.That(error.SubjectName, Is.EqualTo("Psalm 134"));
    }

    [Test]
    public void Error_SubjectName_IsOptional()
    {
        var error = new Error(ErrorKind.Storage, "disk full");

        Assert.That(error.SubjectName, Is.Null);
    }
}
