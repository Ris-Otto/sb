using NUnit.Framework;
using Sb.Core;
using Sb.Core.Storage;

namespace Sb.Tests.Storage;

[TestFixture]
public class AtomicOperationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp() => _tempDir = Path.Combine(Path.GetTempPath(), "sb-tests-" + Guid.NewGuid());

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private ReversibleAction CreateFolder() => new(
        Do: () => Directory.CreateDirectory(_tempDir),
        Undo: () => Directory.Delete(_tempDir, recursive: true));

    [Test]
    public void Execute_DatabaseActionThrows_RollsBackFilesystemChange()
    {
        Result result = AtomicOperation.Execute(
            CreateFolder(),
            databaseAction: Result () => throw new InvalidOperationException("simulated db failure"));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(Directory.Exists(_tempDir), Is.False);
    }

    [Test]
    public void Execute_DatabaseActionReturnsFailure_RollsBackFilesystemChange_PreservesErrorKind()
    {
        var conflictError = new Error(ErrorKind.Conflict, "name already taken", "my-doc");

        Result result = AtomicOperation.Execute(CreateFolder(), () => Result.Failure(conflictError));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo(conflictError));
        Assert.That(Directory.Exists(_tempDir), Is.False);
    }

    [Test]
    public void Execute_FilesystemActionThrows_DatabaseActionNeverRuns()
    {
        bool databaseActionRan = false;
        var brokenFilesystem = new ReversibleAction(
            Do: () => throw new IOException("simulated filesystem failure"),
            Undo: () => { });

        Result result = AtomicOperation.Execute(brokenFilesystem, () =>
        {
            databaseActionRan = true;
            return Result.Success();
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(databaseActionRan, Is.False);
    }

    [Test]
    public void Execute_DatabaseActionFails_AndRollbackAlsoFails_ReturnsErrorMentioningBoth()
    {
        var brokenRollback = new ReversibleAction(
            Do: () => Directory.CreateDirectory(_tempDir),
            Undo: () => throw new IOException("simulated rollback failure"));

        Result result = AtomicOperation.Execute(
            brokenRollback,
            databaseAction: Result () => throw new InvalidOperationException("simulated db failure"));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Message, Does.Contain("simulated db failure"));
        Assert.That(result.Error!.Message, Does.Contain("simulated rollback failure"));

        Directory.Delete(_tempDir, recursive: true); // the broken Undo never actually cleaned it up
    }

    [Test]
    public void Execute_Generic_BothSucceed_ReturnsValue_LeavesFilesystemAndDatabaseInMatchingState()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);

        Result<long> result = AtomicOperation.Execute(CreateFolder(), () => repository.Insert("my-doc", _tempDir, "My Doc"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.Exists(_tempDir), Is.True);

        var all = repository.GetAll();
        Assert.That(all.IsSuccess, Is.True);
        Assert.That(all.Value, Has.Count.EqualTo(1));
        Assert.That(all.Value[0].Id, Is.EqualTo(result.Value));
        Assert.That(all.Value[0].Filename, Is.EqualTo("my-doc"));
        Assert.That(all.Value[0].Path, Is.EqualTo(_tempDir));
    }

    [Test]
    public void Execute_Generic_DatabaseActionReturnsFailure_RollsBackFilesystemChange()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        repository.Insert("my-doc", "/already/exists", "My Doc");

        Result<long> result = AtomicOperation.Execute(CreateFolder(), () => repository.Insert("my-doc", _tempDir, "My Doc"));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        Assert.That(Directory.Exists(_tempDir), Is.False);
    }

    [Test]
    public void ExecuteDelete_DatabaseActionFails_FilesystemCleanupNeverRuns()
    {
        bool cleanupRan = false;
        var error = new Error(ErrorKind.NotFound, "no such document");

        Result result = AtomicOperation.ExecuteDelete(() => Result.Failure(error), () => cleanupRan = true);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo(error));
        Assert.That(cleanupRan, Is.False);
    }

    [Test]
    public void ExecuteDelete_BothSucceed_ReturnsSuccess()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("my-doc", _tempDir, "My Doc").Value;
        Directory.CreateDirectory(_tempDir);

        Result result = AtomicOperation.ExecuteDelete(
            () => repository.Delete(id), () => Directory.Delete(_tempDir, recursive: true));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.Exists(_tempDir), Is.False);
        Assert.That(repository.GetByFilename("my-doc").IsFailure, Is.True);
    }

    [Test]
    public void ExecuteDelete_FilesystemCleanupThrows_DatabaseChangeIsNotRolledBack()
    {
        using var db = Database.Open("Data Source=:memory:");
        var repository = new DocumentsRepository(db);
        long id = repository.Insert("my-doc", _tempDir, "My Doc").Value;

        Result result = AtomicOperation.ExecuteDelete(
            () => repository.Delete(id), filesystemCleanup: () => throw new IOException("simulated cleanup failure"));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
        Assert.That(result.Error!.Message, Does.Contain("simulated cleanup failure"));
        Assert.That(repository.GetByFilename("my-doc").IsFailure, Is.True, "the database delete already committed");
    }
}
