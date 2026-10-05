using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

public sealed class ChaptersRepository(Database database)
{
    public Result<long> Insert(long documentId, string name)
    {
        Result nameValidity = NameValidator.Validate(name);
        if (nameValidity.IsFailure)
            return Result<long>.Failure(nameValidity.Error!);

        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                "INSERT INTO chapters (document_id, name) VALUES ($documentId, $name); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$documentId", documentId);
            command.Parameters.AddWithValue("$name", name);
            return Result<long>.Success((long)command.ExecuteScalar()!);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintUnique)
        {
            return Result<long>.Failure(
                new Error(ErrorKind.Conflict, $"A chapter named '{name}' already exists in this document.", name));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintForeignKey)
        {
            return Result<long>.Failure(
                new Error(ErrorKind.NotFound, $"No document with id {documentId} was found."));
        }
        catch (SqliteException ex)
        {
            return Result<long>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    public Result<ChapterRecord> GetByName(long documentId, string name)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText =
            "SELECT id, document_id, name FROM chapters WHERE document_id = $documentId AND name = $name;";
        command.Parameters.AddWithValue("$documentId", documentId);
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return Result<ChapterRecord>.Failure(
                new Error(ErrorKind.NotFound, $"No chapter named '{name}' was found.", name));

        return Result<ChapterRecord>.Success(
            new ChapterRecord(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)));
    }
}
