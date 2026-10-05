using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

/// <summary>
/// The SQLite-backed source of truth for documents/chapters/songs (spec §2.4.3). Owns a single
/// open connection for its lifetime — in particular, this lets <c>Data Source=:memory:</c> be used
/// for a self-contained, disposable database in tests.
/// </summary>
public sealed class Database : IDisposable
{
    internal SqliteConnection Connection { get; }

    private Database(SqliteConnection connection) => Connection = connection;

    public static Database Open(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            var database = new Database(connection);
            database.EnsureSchema();
            return database;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void EnsureSchema()
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                filename TEXT NOT NULL UNIQUE,
                path TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS chapters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL REFERENCES documents (id),
                name TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS songs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                chapter_id INTEGER NOT NULL REFERENCES chapters (id),
                title TEXT NOT NULL,
                number INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
