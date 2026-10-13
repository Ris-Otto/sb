using System.CommandLine;
using System.CommandLine.Invocation;
using NUnit.Framework;
using Sb.Cli;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Cli;

[TestFixture]
public class DocumentsCommandsTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private RootCommand _root = null!;
    private StringWriter _output = null!;
    private StringWriter _error = null!;
    private InvocationConfiguration _config = null!;
    private bool _confirmResponse;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-cli-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _confirmResponse = false;
        BuildRoot(_ => _confirmResponse);
        _output = new StringWriter();
        _error = new StringWriter();
        _config = new InvocationConfiguration { Output = _output, Error = _error };
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    private void BuildRoot(Func<string, bool> confirm)
    {
        _root = new RootCommand("test");
        _root.Subcommands.Add(DocumentsCommands.Build(new DocumentOperations(_db), _db, confirm));
    }

    private int Run(params string[] args) => _root.Parse(args).Invoke(_config);

    // --- create (7.1) ---

    [Test]
    public void Create_HappyPath_ExitsZero_CreatesDocument()
    {
        int exitCode = Run("documents", "create", "my-doc", "My Doc", "-o", _outputFolder);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.True);
    }

    [Test]
    public void Create_NameAndTitle_ArePositionalArguments_NotFlags()
    {
        // FR-1.8.2: required fields are command arguments, not flags. `--name`/`--title` aren't
        // registered options, so System.CommandLine absorbs "--name" itself as the `name` argument's
        // literal value and then rejects the excess "--title"/"My Doc" tokens as unrecognized.
        int exitCode = Run("documents", "create", "--name", "my-doc", "--title", "My Doc");

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(_error.ToString(), Does.Contain("Unrecognized").IgnoreCase);
    }

    [Test]
    public void Create_OptionalSubtitleAndOutputFolder_StillWorkAsFlags()
    {
        int exitCode = Run("documents", "create", "my-doc", "My Doc", "--subtitle", "A subtitle", "--output-folder", _outputFolder);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        string mainTex = File.ReadAllText(Path.Combine(_outputFolder, "my-doc", "my-doc.tex"));
        Assert.That(mainTex, Does.Contain("A subtitle"));
    }

    [Test]
    public void Create_DuplicateName_ExitsWithConflictCode()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "create", "my-doc", "Other", "-o", _outputFolder);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Conflict)));
    }

    [Test]
    public void Create_IllegalName_ExitsWithValidationCode()
    {
        int exitCode = Run("documents", "create", "foo/bar", "Title", "-o", _outputFolder);

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
    }

    [Test]
    public void Create_MissingRequiredArgument_ExitsNonZero_WithoutThrowing()
    {
        int exitCode = Run("documents", "create", "my-doc", "-o", _outputFolder);

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    // --- list (7.2) ---

    [Test]
    public void List_EmptyDatabase_ExitsZero_PrintsNoDocuments()
    {
        int exitCode = Run("documents", "list");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(_output.ToString(), Does.Contain("No documents"));
    }

    [Test]
    public void List_WithChaptersFlag_PrintsChapterName()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        SeedChapter("my-doc");

        int exitCode = Run("documents", "list", "-c");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(_output.ToString(), Does.Contain("chapter-1"));
    }

    [Test]
    public void List_DocumentFolderMissingOnDisk_FlagsTheMismatch()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        Directory.Delete(Path.Combine(_outputFolder, "my-doc"), recursive: true);

        int exitCode = Run("documents", "list");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success), "listing is informational, not an error");
        Assert.That(_output.ToString(), Does.Contain("my-doc").And.Contain("NOT FOUND on disk"));
    }

    [Test]
    public void List_WithSongsFlagOnly_ListsSongsDirectlyUnderTheDocument_InNumberOrder()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        long chapterId = SeedChapter("my-doc");
        var songs = new SongsRepository(_db);
        songs.Insert(chapterId, Song.Create("Second", ["x"], 2).Value);
        songs.Insert(chapterId, Song.Create("First", ["x"], 1).Value);

        int exitCode = Run("documents", "list", "-s");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        string output = _output.ToString();
        Assert.That(output, Does.Not.Contain("chapter-1 ("), "chapters were not requested (no chapter heading)");
        Assert.That(output.IndexOf("1. First", StringComparison.Ordinal), Is.InRange(0, output.IndexOf("2. Second", StringComparison.Ordinal)));
    }

    [Test]
    public void List_WithChaptersAndSongsFlags_GroupsSongUnderChapter()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        long chapterId = SeedChapter("my-doc");
        new SongsRepository(_db).Insert(chapterId, Song.Create("Lille Albin", ["x"], 1).Value);

        int exitCode = Run("documents", "list", "-c", "-s");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        string output = _output.ToString();
        Assert.That(output, Does.Contain("chapter-1"));
        Assert.That(output, Does.Contain("Lille Albin"));
    }

    // --- delete (7.3) ---

    [Test]
    public void Delete_NameIsPositionalArgument_NotAFlag()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "delete", "--document-name", "my-doc");

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.True);
    }

    [Test]
    public void Delete_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        // Deliberately doesn't use --force and declines the prompt: actually deleting the document
        // would delete the process's own current directory out from under it, which Windows refuses
        // (an OS-level constraint unrelated to inference — the same thing happens typing the document
        // name explicitly from inside its own folder). Reaching the confirmation prompt at all already
        // proves inference resolved "my-doc" correctly, since a failed resolution would exit with a
        // Validation error before ever getting there.
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        SeedChapter("my-doc");
        _confirmResponse = false;
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("documents", "delete");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
            Assert.That(_output.ToString(), Does.Contain("Aborted"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Delete_DocumentNameOmitted_CwdOutsideAnyDocument_ExitsWithValidationCode()
    {
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_outputFolder);
        try
        {
            int exitCode = Run("documents", "delete");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Build_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("documents", "build");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Delete_NonEmptyDocument_WithoutForce_DecliningPrompt_DeletesNothing()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        SeedChapter("my-doc");
        _confirmResponse = false;

        int exitCode = Run("documents", "delete", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.True);
    }

    [Test]
    public void Delete_NonEmptyDocument_WithoutForce_AcceptingPrompt_CascadesFully()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        SeedChapter("my-doc");
        _confirmResponse = true;

        int exitCode = Run("documents", "delete", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.False);
    }

    [Test]
    public void Delete_WithForceFlag_SkipsPromptEntirely()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        SeedChapter("my-doc");
        bool confirmCalled = false;
        BuildRoot(_ => { confirmCalled = true; return true; });

        int exitCode = Run("documents", "delete", "my-doc", "-f");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(confirmCalled, Is.False);
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.False);
    }

    [Test]
    public void Delete_EmptyDocument_NeverPrompts()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        bool confirmCalled = false;
        BuildRoot(_ => { confirmCalled = true; return false; });

        int exitCode = Run("documents", "delete", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(confirmCalled, Is.False);
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc")), Is.False);
    }

    [Test]
    public void Delete_NonexistentDocument_ExitsWithNotFoundCode_NotACrash()
    {
        int exitCode = Run("documents", "delete", "ghost");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- rename (7.4) ---

    [Test]
    public void Rename_HappyPath_ExitsZero_MovesFolder()
    {
        Run("documents", "create", "old-name", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "rename", "old-name", "new-name");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "new-name")), Is.True);
    }

    [Test]
    public void Rename_NamesArePositionalArguments_NotFlags()
    {
        Run("documents", "create", "old-name", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "rename", "--document-name", "old-name", "--new-name", "new-name");

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(Directory.Exists(Path.Combine(_outputFolder, "old-name")), Is.True);
    }

    [Test]
    public void Rename_ToTakenName_ExitsWithConflictCode()
    {
        Run("documents", "create", "doc-a", "A", "-o", _outputFolder);
        Run("documents", "create", "doc-b", "B", "-o", _outputFolder);

        int exitCode = Run("documents", "rename", "doc-b", "doc-a");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Conflict)));
    }

    [Test]
    public void Rename_NonexistentDocument_ExitsWithNotFoundCode()
    {
        int exitCode = Run("documents", "rename", "ghost", "new-name");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- build (7.5) ---

    [Test]
    public void Build_DefaultOrderingAndFormat_ExitsZero_WritesMainTex()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "build", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(_outputFolder, "my-doc", "my-doc.tex")), Is.True);
    }

    [Test]
    public void Build_DocumentNameIsPositionalArgument_NotAFlag()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "build", "--document-name", "my-doc");

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void Build_SingleColumnFormat_ExitsZero()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "build", "my-doc", "-f", "single-column");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
    }

    [Test]
    public void Build_InvalidOrderingValue_RejectedByParser_ExitsNonZero()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);

        int exitCode = Run("documents", "build", "my-doc", "-o", "bogus");

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void Build_NonexistentDocument_ExitsWithNotFoundCode()
    {
        int exitCode = Run("documents", "build", "ghost");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    private long SeedChapter(string documentName)
    {
        var documents = new DocumentsRepository(_db);
        var chapters = new ChaptersRepository(_db);
        long documentId = documents.GetByFilename(documentName).Value.Id;
        return chapters.Insert(documentId, "chapter-1", "Chapter One").Value;
    }

    [Test]
    public void Rename_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("documents", "create", "my-doc", "Title", "-o", _outputFolder);
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("documents", "rename", "renamed-doc");

            // On Windows the folder can't be moved while it is this process's own working directory,
            // which is an operating-system limit; what matters here is that the name was inferred, so
            // the command got as far as renaming rather than failing validation.
            Assert.That(_error.ToString(), Does.Not.Contain("No document name"));
            if (!OperatingSystem.IsWindows())
            {
                Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
                Assert.That(Directory.Exists(Path.Combine(_outputFolder, "renamed-doc")), Is.True);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Rename_OutsideAnyDocument_WithoutDocumentName_FailsWithValidationError()
    {
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_outputFolder);
        try
        {
            int exitCode = Run("documents", "rename", "renamed-doc");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}
