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
            database.EnforceForeignKeys();
            database.EnsureSchema();
            return database;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void EnforceForeignKeys()
    {
        // Microsoft.Data.Sqlite already defaults this to on, unlike raw SQLite; set explicitly so
        // the referential-integrity guarantee (FR-1.7.x) doesn't depend on an undocumented default.
        using var command = Connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                filename TEXT NOT NULL UNIQUE,
                path TEXT NOT NULL,
                title TEXT NOT NULL,
                subtitle TEXT
            );
            CREATE TABLE IF NOT EXISTS chapters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL REFERENCES documents (id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                title TEXT NOT NULL,
                UNIQUE (document_id, name)
            );
            CREATE TABLE IF NOT EXISTS songs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                chapter_id INTEGER NOT NULL REFERENCES chapters (id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                number INTEGER NOT NULL,
                UNIQUE (chapter_id, title)
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Runs <paramref name="work"/> inside one SQLite transaction: committed if it returns success, rolled
    /// back if it returns a failure or throws (an exception becomes a <see cref="ErrorKind.Storage"/>
    /// failure). Lets a multi-row change such as a document-wide song renumbering (FR-1.4.3) be all or
    /// nothing in the database (FR-1.7.6).
    /// </summary>
    public Result<T> InTransaction<T>(Func<Result<T>> work)
    {
        try
        {
            Execute("BEGIN IMMEDIATE;");
        }
        catch (SqliteException ex)
        {
            return Result<T>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }

        Result<T> result;
        try
        {
            result = work();
        }
        catch (Exception ex)
        {
            TryRollback();
            return Result<T>.Failure(new Error(ErrorKind.Storage, $"Database write failed: {ex.Message}"));
        }

        if (result.IsFailure)
        {
            TryRollback();
            return result;
        }

        try
        {
            Execute("COMMIT;");
        }
        catch (SqliteException ex)
        {
            TryRollback();
            return Result<T>.Failure(new Error(ErrorKind.Storage, $"Database commit failed: {ex.Message}"));
        }

        return result;
    }

    /// <inheritdoc cref="InTransaction{T}"/>
    public Result InTransaction(Func<Result> work)
    {
        Result<object?> result = InTransaction<object?>(() =>
        {
            Result inner = work();
            return inner.IsSuccess ? Result<object?>.Success(null) : Result<object?>.Failure(inner.Error!);
        });

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
    }

    private void Execute(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void TryRollback()
    {
        try
        {
            Execute("ROLLBACK;");
        }
        catch (SqliteException)
        {
            // No transaction left to roll back (e.g. SQLite already rolled it back after a hard error).
        }
    }

    public void Dispose() => Connection.Dispose();
}
