using System.CommandLine;
using System.CommandLine.Invocation;
using NUnit.Framework;
using Sb.Cli;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Cli;

[TestFixture]
public class SongsCommandsTests
{
    private string _outputFolder = null!;
    private Database _db = null!;
    private RootCommand _root = null!;
    private StringWriter _output = null!;
    private StringWriter _error = null!;
    private InvocationConfiguration _config = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-songs-cli-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _root = new RootCommand("test");
        _root.Subcommands.Add(SongsCommands.Build(new SongOperations(_db), _db));
        _output = new StringWriter();
        _error = new StringWriter();
        _config = new InvocationConfiguration { Output = _output, Error = _error };

        // Seed a document + chapter via their real operation classes, same as a user would.
        new DocumentOperations(_db).Create("my-doc", "Title", null, _outputFolder);
        new ChapterOperations(_db).Add("my-doc", "chapter-1", null);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    private int Run(params string[] args) => _root.Parse(args).Invoke(_config);

    private string ChapterFolder => Path.Combine(_outputFolder, "my-doc", "Chapters", "chapter-1");

    // --- add (9.1) ---

    [Test]
    public void Add_HappyPath_ExitsZero_CreatesSongFile()
    {
        int exitCode = Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Lille Albin.yml")), Is.True);
    }

    [Test]
    public void Add_RequiredFieldsArePositionalArguments_NotFlags()
    {
        int exitCode = Run("songs", "add", "--document-name", "my-doc", "--title", "Lille Albin");

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void Add_WithOptionalFlags_StoresThem()
    {
        int exitCode = Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1", "-w", "Okänd", "-m", "Trad.");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        var song = SongYamlSerializer.Deserialize(File.ReadAllText(Path.Combine(ChapterFolder, "Lille Albin.yml"))).Value;
        Assert.That(song.Writer, Is.EqualTo("Okänd"));
        Assert.That(song.Melody, Is.EqualTo("Trad."));
    }

    [Test]
    public void Add_DuplicateTitle_ExitsWithConflictCode()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        int exitCode = Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 2");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Conflict)));
    }

    [Test]
    public void Add_NonexistentChapter_ExitsWithNotFoundCode()
    {
        int exitCode = Run("songs", "add", "my-doc", "ghost-chapter", "Title", "text");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- delete (9.2) ---

    [Test]
    public void Delete_ExistingSong_ExitsZero_RemovesFile()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        int exitCode = Run("songs", "delete", "my-doc", "Lille Albin");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Lille Albin.yml")), Is.False);
    }

    [Test]
    public void Delete_NonexistentSong_ExitsWithNotFoundCode()
    {
        int exitCode = Run("songs", "delete", "my-doc", "Ghost Song");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- list (9.3) ---

    [Test]
    public void List_NoSongs_ExitsZero_PrintsNoSongs()
    {
        int exitCode = Run("songs", "list", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(_output.ToString(), Does.Contain("No songs"));
    }

    [Test]
    public void List_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("songs", "list");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
            Assert.That(_output.ToString(), Does.Contain("Lille Albin"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void List_DocumentNameGivenExplicitly_OverridesWorkingDirectoryInference()
    {
        var documentsRoot = new RootCommand("test");
        documentsRoot.Subcommands.Add(DocumentsCommands.Build(new DocumentOperations(_db), _db, _ => false));
        documentsRoot.Parse(["documents", "create", "other-doc", "Other", "-o", _outputFolder])
            .Invoke(new InvocationConfiguration { Output = new StringWriter(), Error = new StringWriter() });
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "other-doc"));
        try
        {
            // cwd is inside "other-doc", but "my-doc" is given explicitly, so it must win.
            int exitCode = Run("songs", "list", "my-doc");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
            Assert.That(_output.ToString(), Does.Contain("Lille Albin"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void List_DocumentNameOmitted_CwdOutsideAnyDocument_ExitsWithValidationCode()
    {
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_outputFolder); // the output folder itself, not inside "my-doc"
        try
        {
            int exitCode = Run("songs", "list");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void List_WithChapterFilter_PrintsOnlyThatChaptersSongs()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        int exitCode = Run("songs", "list", "my-doc", "-c", "chapter-1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(_output.ToString(), Does.Contain("Lille Albin"));
    }

    // --- modify (9.4) ---

    [Test]
    public void Modify_Title_ExitsZero_RenamesFile()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Old Title", "rad 1");

        int exitCode = Run("songs", "modify", "my-doc", "Old Title", "-t", "New Title");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "New Title.yml")), Is.True);
    }

    [Test]
    public void Modify_NoFlagsGiven_ExitsWithValidationCode()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        int exitCode = Run("songs", "modify", "my-doc", "Lille Albin");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
    }

    [Test]
    public void Modify_NonexistentSong_ExitsWithNotFoundCode()
    {
        int exitCode = Run("songs", "modify", "my-doc", "Ghost", "-t", "New");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- working-directory inference for the multi-positional commands (FR-1.8.4) ---

    private int RunIn(string directory, params string[] args)
    {
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);
        try
        {
            return Run(args);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Add_FromInsideChapterFolder_InfersDocumentAndChapter()
    {
        int exitCode = RunIn(ChapterFolder, "songs", "add", "Lille Albin", "rad 1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Lille Albin.yml")), Is.True);
    }

    [Test]
    public void Add_FromInsideDocumentFolder_ChapterGiven_InfersDocument()
    {
        int exitCode = RunIn(Path.Combine(_outputFolder, "my-doc"), "songs", "add", "chapter-1", "Lille Albin", "rad 1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Lille Albin.yml")), Is.True);
    }

    [Test]
    public void Add_OutsideAnyDocument_WithoutDocumentName_FailsWithValidationError()
    {
        int exitCode = RunIn(_outputFolder, "songs", "add", "chapter-1", "Lille Albin", "rad 1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        Assert.That(_error.ToString(), Does.Contain("document"));
    }

    [Test]
    public void Add_FromDocumentFolder_WithoutChapter_FailsWithValidationError()
    {
        int exitCode = RunIn(Path.Combine(_outputFolder, "my-doc"), "songs", "add", "Lille Albin", "rad 1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        Assert.That(_error.ToString(), Does.Contain("chapter"));
    }

    [Test]
    public void Add_ExplicitDocumentAndChapter_OverrideInference()
    {
        new ChapterOperations(_db).Add("my-doc", "chapter-2", null);

        int exitCode = RunIn(ChapterFolder, "songs", "add", "my-doc", "chapter-2", "Lille Albin", "rad 1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(_outputFolder, "my-doc", "Chapters", "chapter-2", "Lille Albin.yml")), Is.True);
    }

    [Test]
    public void Delete_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        int exitCode = RunIn(Path.Combine(_outputFolder, "my-doc"), "songs", "delete", "Lille Albin");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.Exists(Path.Combine(ChapterFolder, "Lille Albin.yml")), Is.False);
    }

    [Test]
    public void Modify_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("songs", "add", "my-doc", "chapter-1", "Lille Albin", "rad 1");

        int exitCode = RunIn(Path.Combine(_outputFolder, "my-doc"), "songs", "modify", "Lille Albin", "-w", "Nobody");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(File.ReadAllText(Path.Combine(ChapterFolder, "Lille Albin.yml")), Does.Contain("Nobody"));
    }

    [Test]
    public void Modify_OutsideAnyDocument_WithoutDocumentName_FailsWithValidationError()
    {
        int exitCode = RunIn(_outputFolder, "songs", "modify", "Lille Albin", "-w", "Nobody");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
    }
}
