using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Sb.Core.Storage;

namespace Sb.Tests.Storage;

[TestFixture]
public class DatabaseTests
{
    [Test]
    public void Open_SchemaCreationFails_DisposesConnection_FileNotLeftLocked()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "sb-db-" + Guid.NewGuid() + ".db");
        File.WriteAllBytes(tempFile, []); // zero-length file: a valid, openable, empty SQLite database
        try
        {
            // Opening it read-only lets Open() succeed but makes EnsureSchema's CREATE TABLE fail.
            // Pooling=False so a leaked connection isn't masked by Microsoft.Data.Sqlite's connection
            // pool keeping the native file handle open regardless of what Dispose() does.
            Assert.Throws<SqliteException>(() => Database.Open($"Data Source={tempFile};Mode=ReadOnly;Pooling=False"));

            // If Open() leaked the connection instead of disposing it on failure, this delete would
            // throw on Windows because the file would still be locked by the open handle.
            Assert.DoesNotThrow(() => File.Delete(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public void Open_CreatesDocumentsTable_WithExpectedColumns()
    {
        using var db = Database.Open("Data Source=:memory:");

        AssertTableColumns(db, "documents", "id", "filename", "path", "title", "subtitle");
    }

    [Test]
    public void Open_CreatesChaptersTable_WithExpectedColumns()
    {
        using var db = Database.Open("Data Source=:memory:");

        AssertTableColumns(db, "chapters", "id", "document_id", "name", "title");
    }

    [Test]
    public void Open_CreatesSongsTable_WithExpectedColumns()
    {
        using var db = Database.Open("Data Source=:memory:");

        AssertTableColumns(db, "songs", "id", "chapter_id", "title", "number");
    }

    private static void AssertTableColumns(Database db, string table, params string[] expectedColumns)
    {
        using var command = db.Connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();

        var actualColumns = new List<string>();
        while (reader.Read())
            actualColumns.Add(reader.GetString(1));

        Assert.That(actualColumns, Is.EquivalentTo(expectedColumns));
    }

    private static int DocumentCount(Database db) => new DocumentsRepository(db).GetAll().Value.Count;

    [Test]
    public void InTransaction_Success_CommitsEverything()
    {
        using var db = Database.Open("Data Source=:memory:");
        var documents = new DocumentsRepository(db);

        var result = db.InTransaction(() =>
        {
            documents.Insert("a", "/a", "A", null);
            documents.Insert("b", "/b", "B", null);
            return Sb.Core.Result.Success();
        });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(DocumentCount(db), Is.EqualTo(2));
    }

    [Test]
    public void InTransaction_FailureResult_RollsBackEverything()
    {
        using var db = Database.Open("Data Source=:memory:");
        var documents = new DocumentsRepository(db);

        var result = db.InTransaction(() =>
        {
            documents.Insert("a", "/a", "A", null);
            return Sb.Core.Result.Failure(new Sb.Core.Error(Sb.Core.ErrorKind.Conflict, "nope"));
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(Sb.Core.ErrorKind.Conflict));
        Assert.That(DocumentCount(db), Is.EqualTo(0));
    }

    [Test]
    public void InTransaction_Exception_RollsBackAndReportsStorageError()
    {
        using var db = Database.Open("Data Source=:memory:");
        var documents = new DocumentsRepository(db);

        var result = db.InTransaction<int>(() =>
        {
            documents.Insert("a", "/a", "A", null);
            throw new InvalidOperationException("boom");
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(Sb.Core.ErrorKind.Storage));
        Assert.That(DocumentCount(db), Is.EqualTo(0));
    }
}
