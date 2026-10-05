using Microsoft.Data.Sqlite;

namespace sb.Index;

/// <summary>
/// Opens (creating if absent) the SQLite index file and applies its schema.
/// Callers query <see cref="Connection"/> directly (contracts/storage-format.md) — there is
/// intentionally no ORM layered on top for three small tables with simple CRUD.
/// </summary>
public sealed class SbIndexDatabase : IDisposable
{
    public SqliteConnection Connection { get; }

    public SbIndexDatabase(string databasePath)
    {
        // Pooling=False: this is a short-lived CLI process, not a server — disabling pooling
        // means Dispose() actually releases the native file handle immediately, which matters
        // for tests that delete the containing temp directory right after disposing.
        Connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        Connection.Open();

        using (var pragma = Connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        ApplySchema();
    }

    private void ApplySchema()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY,
                path TEXT NOT NULL UNIQUE,
                filename TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS chapters (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                document_id INTEGER NOT NULL REFERENCES documents(id),
                UNIQUE (document_id, name)
            );
            CREATE TABLE IF NOT EXISTS songs (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                singing_order_number INTEGER NOT NULL,
                chapter_id INTEGER NOT NULL REFERENCES chapters(id)
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
