using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

public sealed class DocumentsRepository(Database database)
{
    public Result<long> Insert(string filename, string path, string title, string? subtitle = null)
    {
        Result nameValidity = NameValidator.Validate(filename);
        if (nameValidity.IsFailure)
            return Result<long>.Failure(nameValidity.Error!);

        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO documents (filename, path, title, subtitle) VALUES ($filename, $path, $title, $subtitle);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$filename", filename);
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$subtitle", (object?)subtitle ?? DBNull.Value);
            return Result<long>.Success((long)command.ExecuteScalar()!);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintUnique)
        {
            return Result<long>.Failure(
                new Error(ErrorKind.Conflict, $"A document named '{filename}' already exists.", filename));
        }
        catch (SqliteException ex)
        {
            return Result<long>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    public Result<DocumentRecord> GetByFilename(string filename)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT id, filename, path, title, subtitle FROM documents WHERE filename = $filename;";
        command.Parameters.AddWithValue("$filename", filename);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return Result<DocumentRecord>.Failure(
                new Error(ErrorKind.NotFound, $"No document named '{filename}' was found.", filename));

        return Result<DocumentRecord>.Success(ReadRecord(reader));
    }

    public Result Rename(long id, string newFilename, string newPath)
    {
        Result nameValidity = NameValidator.Validate(newFilename);
        if (nameValidity.IsFailure)
            return nameValidity;

        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "UPDATE documents SET filename = $filename, path = $path WHERE id = $id;";
            command.Parameters.AddWithValue("$filename", newFilename);
            command.Parameters.AddWithValue("$path", newPath);
            command.Parameters.AddWithValue("$id", id);
            int rowsAffected = command.ExecuteNonQuery();

            return rowsAffected > 0
                ? Result.Success()
                : Result.Failure(new Error(ErrorKind.NotFound, $"No document with id {id} was found."));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintUnique)
        {
            return Result.Failure(
                new Error(ErrorKind.Conflict, $"A document named '{newFilename}' already exists.", newFilename));
        }
        catch (SqliteException ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    /// <summary>Deletes a document row. Cascades to its chapters/songs via the schema's `ON DELETE
    /// CASCADE` (FR-1.8.5) — the caller is responsible for the matching filesystem cleanup
    /// (<see cref="AtomicOperation.ExecuteDelete"/>).</summary>
    public Result Delete(long id)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "DELETE FROM documents WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            int rowsAffected = command.ExecuteNonQuery();

            return rowsAffected > 0
                ? Result.Success()
                : Result.Failure(new Error(ErrorKind.NotFound, $"No document with id {id} was found."));
        }
        catch (SqliteException ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    public Result<IReadOnlyList<DocumentRecord>> GetAll()
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "SELECT id, filename, path, title, subtitle FROM documents;";
            using var reader = command.ExecuteReader();

            var results = new List<DocumentRecord>();
            while (reader.Read())
                results.Add(ReadRecord(reader));
            return Result<IReadOnlyList<DocumentRecord>>.Success(results);
        }
        catch (SqliteException ex)
        {
            return Result<IReadOnlyList<DocumentRecord>>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    private static DocumentRecord ReadRecord(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4));
}
