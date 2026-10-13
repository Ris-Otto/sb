using System.CommandLine;
using System.CommandLine.Invocation;
using NUnit.Framework;
using Sb.Cli;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Cli;

[TestFixture]
public class ChaptersCommandsTests
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
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-chapters-cli-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
        _confirmResponse = false;
        BuildRoot(_ => _confirmResponse);
        _output = new StringWriter();
        _error = new StringWriter();
        _config = new InvocationConfiguration { Output = _output, Error = _error };

        // Seed a document via the real DocumentsCommands path, same as a user would.
        var documentsRoot = new RootCommand("test");
        documentsRoot.Subcommands.Add(DocumentsCommands.Build(new DocumentOperations(_db), _db, _ => false));
        documentsRoot.Parse(["documents", "create", "my-doc", "Title", "-o", _outputFolder])
            .Invoke(new InvocationConfiguration { Output = new StringWriter(), Error = new StringWriter() });
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
        _root.Subcommands.Add(ChaptersCommands.Build(new ChapterOperations(_db), _db, confirm));
    }

    private int Run(params string[] args) => _root.Parse(args).Invoke(_config);

    private string ChapterFolder(string chapterName) => Path.Combine(_outputFolder, "my-doc", "Chapters", chapterName);

    // --- add (8.1) ---

    [Test]
    public void Add_ExplicitNameAndTitle_ExitsZero_CreatesChapter()
    {
        int exitCode = Run("chapters", "add", "my-doc", "--name", "chapter-1", "--title", "Chapter One");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(ChapterFolder("chapter-1")), Is.True);
    }

    [Test]
    public void Add_DocumentNameIsPositionalArgument_NotAFlag()
    {
        int exitCode = Run("chapters", "add", "--document-name", "my-doc");

        Assert.That(exitCode, Is.Not.EqualTo(0));
    }

    [Test]
    public void Add_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("chapters", "add", "--name", "chapter-1");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
            Assert.That(Directory.Exists(ChapterFolder("chapter-1")), Is.True);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Add_DocumentNameOmitted_CwdOutsideAnyDocument_ExitsWithValidationCode()
    {
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_outputFolder);
        try
        {
            int exitCode = Run("chapters", "add");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Add_NoNameGiven_UsesDefaultPattern()
    {
        int exitCode = Run("chapters", "add", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(ChapterFolder("my-doc_chp_1")), Is.True);
    }

    [Test]
    public void Add_DuplicateName_ExitsWithConflictCode()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");

        int exitCode = Run("chapters", "add", "my-doc", "-n", "chapter-1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Conflict)));
    }

    [Test]
    public void Add_NonexistentDocument_ExitsWithNotFoundCode()
    {
        int exitCode = Run("chapters", "add", "ghost-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- delete (8.2) ---

    [Test]
    public void Delete_MultipleCommaSeparatedChapters_RemovesAll()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        Run("chapters", "add", "my-doc", "-n", "chapter-2");

        int exitCode = Run("chapters", "delete", "my-doc", "chapter-1,chapter-2");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(ChapterFolder("chapter-1")), Is.False);
        Assert.That(Directory.Exists(ChapterFolder("chapter-2")), Is.False);
    }

    [Test]
    public void Delete_NonEmptyChapter_WithoutForce_DecliningPrompt_DeletesNothing()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        SeedSong("chapter-1", "Lille Albin");
        _confirmResponse = false;

        int exitCode = Run("chapters", "delete", "my-doc", "chapter-1");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(Directory.Exists(ChapterFolder("chapter-1")), Is.True);
    }

    [Test]
    public void Delete_NonEmptyChapter_WithForce_SkipsPromptAndCascades()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        SeedSong("chapter-1", "Lille Albin");
        bool confirmCalled = false;
        BuildRoot(_ => { confirmCalled = true; return true; });

        int exitCode = Run("chapters", "delete", "my-doc", "chapter-1", "-f");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(confirmCalled, Is.False);
        Assert.That(Directory.Exists(ChapterFolder("chapter-1")), Is.False);
    }

    [Test]
    public void Delete_ChapterThatDoesNotExist_ExitsWithNotFoundCode()
    {
        int exitCode = Run("chapters", "delete", "my-doc", "ghost-chapter");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.NotFound)));
    }

    // --- list (8.3) ---

    [Test]
    public void List_NoChapters_ExitsZero_PrintsNoChapters()
    {
        int exitCode = Run("chapters", "list", "my-doc");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(_output.ToString(), Does.Contain("No chapters"));
    }

    [Test]
    public void List_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("chapters", "list");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
            Assert.That(_output.ToString(), Does.Contain("chapter-1"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void List_WithSongsFlag_PrintsSongTitle()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        SeedSong("chapter-1", "Lille Albin");

        int exitCode = Run("chapters", "list", "my-doc", "-s");

        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        Assert.That(_output.ToString(), Does.Contain("Lille Albin"));
    }

    private void SeedSong(string chapterName, string title)
    {
        long documentId = new DocumentsRepository(_db).GetByFilename("my-doc").Value.Id;
        long chapterId = new ChaptersRepository(_db).GetByName(documentId, chapterName).Value.Id;
        var song = Song.Create(title, ["x"], 1).Value;
        new SongsRepository(_db).Insert(chapterId, song);
        File.WriteAllText(Path.Combine(ChapterFolder(chapterName), $"{title}.yml"), SongYamlSerializer.Serialize(song));
    }

    [Test]
    public void Delete_DocumentNameOmitted_InfersFromWorkingDirectory()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Combine(_outputFolder, "my-doc"));
        try
        {
            int exitCode = Run("chapters", "delete", "chapter-1");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
            Assert.That(Directory.Exists(Path.Combine(_outputFolder, "my-doc", "Chapters", "chapter-1")), Is.False);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Test]
    public void Delete_OutsideAnyDocument_WithoutDocumentName_FailsWithValidationError()
    {
        Run("chapters", "add", "my-doc", "-n", "chapter-1");
        string originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_outputFolder);
        try
        {
            int exitCode = Run("chapters", "delete", "chapter-1");

            Assert.That(exitCode, Is.EqualTo(ExitCodes.ForError(ErrorKind.Validation)));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}
