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
}
