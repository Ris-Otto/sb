using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

public sealed class DocumentsRepository(Database database)
{
    private const int SqliteConstraintViolation = 19; // SQLITE_CONSTRAINT

    public Result<long> Insert(string filename, string path)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                "INSERT INTO documents (filename, path) VALUES ($filename, $path); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$filename", filename);
            command.Parameters.AddWithValue("$path", path);
            return Result<long>.Success((long)command.ExecuteScalar()!);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintViolation)
        {
            return Result<long>.Failure(
                new Error(ErrorKind.Conflict, $"A document named '{filename}' already exists.", filename));
        }
        catch (SqliteException ex)
        {
            return Result<long>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    public Result<IReadOnlyList<DocumentRecord>> GetAll()
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "SELECT id, filename, path FROM documents;";
            using var reader = command.ExecuteReader();

            var results = new List<DocumentRecord>();
            while (reader.Read())
                results.Add(new DocumentRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            return Result<IReadOnlyList<DocumentRecord>>.Success(results);
        }
        catch (SqliteException ex)
        {
            return Result<IReadOnlyList<DocumentRecord>>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }
}
