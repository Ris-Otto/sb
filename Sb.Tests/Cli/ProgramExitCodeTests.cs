using NUnit.Framework;
using Sb.Cli;
using Sb.Core;

namespace Sb.Tests.Cli;

/// <summary>Milestone 10.3 (NFR-2.2.2): a thin wrapper around the real CLI entry point
/// (<see cref="Program.Main"/>), proving the exit-code mapping holds end-to-end — not just at the
/// <see cref="CommandReporting"/>/<see cref="ExitCodes"/> unit level, but through the actual process
/// entry point, environment-variable-driven database location, and argument parsing.</summary>
[TestFixture]
public class ProgramExitCodeTests
{
    private string _dataDir = null!;
    private string? _originalDataDirEnv;

    [SetUp]
    public void SetUp()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "sb-program-tests-" + Guid.NewGuid());
        _originalDataDirEnv = Environment.GetEnvironmentVariable("SB_DATA_DIR");
        Environment.SetEnvironmentVariable("SB_DATA_DIR", _dataDir);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("SB_DATA_DIR", _originalDataDirEnv);
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    private static int RunMain(string[] args, out string output, out string error)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        var outWriter = new StringWriter();
        var errorWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errorWriter);
        try
        {
            int exitCode = Program.Main(args);
            output = outWriter.ToString();
            error = errorWriter.ToString();
            return exitCode;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Test]
    public void Success_ExitsZero()
    {
        string outputFolder = Path.Combine(_dataDir, "docs");
        Directory.CreateDirectory(outputFolder);

        int exitCode = RunMain(["documents", "create", "my-doc", "Title", "-o", outputFolder], out _, out _);

        Assert.That(exitCode, Is.EqualTo(0));
    }

    [Test]
    public void ValidationError_ExitsWithValidationCode()
    {
        int exitCode = RunMain(["documents", "create", "foo/bar", "Title"], out _, out string error);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        Assert.That(error, Does.Contain("Error"));
    }

    [Test]
    public void NotFoundError_ExitsWithNotFoundCode()
    {
        int exitCode = RunMain(["documents", "delete", "ghost-doc"], out _, out string error);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
        Assert.That(error, Does.Contain("Error"));
    }

    [Test]
    public void ConflictError_ExitsWithConflictCode()
    {
        string outputFolder = Path.Combine(_dataDir, "docs");
        Directory.CreateDirectory(outputFolder);
        RunMain(["documents", "create", "my-doc", "Title", "-o", outputFolder], out _, out _);

        int exitCode = RunMain(["documents", "create", "my-doc", "Other", "-o", outputFolder], out _, out _);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Conflict)));
    }

    [Test]
    public void DataDirEnvironmentVariable_IsolatesDatabaseBetweenRuns()
    {
        string outputFolder = Path.Combine(_dataDir, "docs");
        Directory.CreateDirectory(outputFolder);
        RunMain(["documents", "create", "my-doc", "Title", "-o", outputFolder], out _, out _);

        Assert.That(File.Exists(Path.Combine(_dataDir, "sb.db")), Is.True);
    }

    [Test]
    public void UnusableDataDirectory_ReportsStorageErrorInsteadOfCrashing()
    {
        // SB_DATA_DIR pointing at an existing *file* makes creating the data directory fail.
        string blocker = Path.Combine(Path.GetTempPath(), "sb-program-blocker-" + Guid.NewGuid());
        File.WriteAllText(blocker, "not a directory");
        Environment.SetEnvironmentVariable("SB_DATA_DIR", blocker);
        try
        {
            int exitCode = RunMain(["documents", "list"], out _, out string error);

            Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Storage)));
            Assert.That(error, Does.StartWith("Error:"));
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
