using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class NameValidatorTests
{
    [TestCase("My Document")]
    [TestCase("Lille_albin")]
    [TestCase("1 - En bit och en sup")]
    public void Validate_PlainName_Succeeds(string name)
    {
        Assert.That(NameValidator.Validate(name).IsSuccess, Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_BlankName_Fails(string name)
    {
        var result = NameValidator.Validate(name);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
        Assert.That(result.Error!.Message, Does.Contain("empty"));
    }

    [Test]
    public void Validate_ContainsForwardSlash_Fails()
    {
        var result = NameValidator.Validate("foo/bar");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain("/"));
    }

    [Test]
    public void Validate_ContainsBackslash_Fails()
    {
        var result = NameValidator.Validate("foo\\bar");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain("\\"));
    }

    [Test]
    public void Validate_ContainsDotDot_Fails()
    {
        var result = NameValidator.Validate("..");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain(".."));
    }

    [Test]
    public void Validate_ContainsColon_Fails()
    {
        var result = NameValidator.Validate("foo:bar");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain(":"));
    }

    [Test]
    public void Validate_ContainsNullByte_Fails()
    {
        var result = NameValidator.Validate("foo\0bar");

        Assert.That(result.IsFailure, Is.True);
    }

    [TestCase("foo*bar")]
    [TestCase("foo?bar")]
    [TestCase("foo\"bar")]
    [TestCase("foo<bar")]
    [TestCase("foo>bar")]
    [TestCase("foo|bar")]
    public void Validate_OtherFilesystemIllegalCharacters_Fail(string name)
    {
        Assert.That(NameValidator.Validate(name).IsFailure, Is.True);
    }

    [Test]
    public void Validate_DifferentViolations_ProduceDistinguishableMessages()
    {
        string blank = NameValidator.Validate("").Error!.Message;
        string slash = NameValidator.Validate("a/b").Error!.Message;
        string dotDot = NameValidator.Validate("..").Error!.Message;
        string illegalChar = NameValidator.Validate("a:b").Error!.Message;

        Assert.That(new[] { blank, slash, dotDot, illegalChar }.Distinct().ToArray(), Has.Length.EqualTo(4));
    }
}
