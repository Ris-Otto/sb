using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

public sealed class DocumentsRepository(Database database)
{
    public Result<long> Insert(string filename, string path)
    {
        Result nameValidity = NameValidator.Validate(filename);
        if (nameValidity.IsFailure)
            return Result<long>.Failure(nameValidity.Error!);

        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                "INSERT INTO documents (filename, path) VALUES ($filename, $path); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$filename", filename);
            command.Parameters.AddWithValue("$path", path);
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
        command.CommandText = "SELECT id, filename, path FROM documents WHERE filename = $filename;";
        command.Parameters.AddWithValue("$filename", filename);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return Result<DocumentRecord>.Failure(
                new Error(ErrorKind.NotFound, $"No document named '{filename}' was found.", filename));

        return Result<DocumentRecord>.Success(
            new DocumentRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
    }

    public Result Rename(long id, string newFilename)
    {
        Result nameValidity = NameValidator.Validate(newFilename);
        if (nameValidity.IsFailure)
            return nameValidity;

        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "UPDATE documents SET filename = $filename WHERE id = $id;";
            command.Parameters.AddWithValue("$filename", newFilename);
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
