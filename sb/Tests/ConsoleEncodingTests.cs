using NUnit.Framework;

namespace sb.Tests;

[TestFixture]
public class ConsoleEncodingTests
{
    [Test]
    public void ConfigureUtf8Console_SetsOutputAndInputEncodingToUtf8()
    {
        var originalOutput = Console.OutputEncoding;
        var originalInput = Console.InputEncoding;
        try
        {
            Program.ConfigureUtf8Console();

            Assert.That(Console.OutputEncoding.CodePage, Is.EqualTo(65001));
            Assert.That(Console.InputEncoding.CodePage, Is.EqualTo(65001));
        }
        finally
        {
            Console.OutputEncoding = originalOutput;
            Console.InputEncoding = originalInput;
        }
    }
}
