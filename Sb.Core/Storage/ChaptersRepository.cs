using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

public sealed class ChaptersRepository(Database database)
{
    public Result<long> Insert(long documentId, string name, string title)
    {
        Result nameValidity = NameValidator.Validate(name);
        if (nameValidity.IsFailure)
            return Result<long>.Failure(nameValidity.Error!);

        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO chapters (document_id, name, title) VALUES ($documentId, $name, $title);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$documentId", documentId);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$title", title);
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
            "SELECT id, document_id, name, title FROM chapters WHERE document_id = $documentId AND name = $name;";
        command.Parameters.AddWithValue("$documentId", documentId);
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return Result<ChapterRecord>.Failure(
                new Error(ErrorKind.NotFound, $"No chapter named '{name}' was found.", name));

        return Result<ChapterRecord>.Success(ReadRecord(reader));
    }

    /// <summary>All chapters belonging to a document, in the order they were added (FR-1.9.2).</summary>
    public Result<IReadOnlyList<ChapterRecord>> GetAllForDocument(long documentId)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                "SELECT id, document_id, name, title FROM chapters WHERE document_id = $documentId ORDER BY id;";
            command.Parameters.AddWithValue("$documentId", documentId);
            using var reader = command.ExecuteReader();

            var results = new List<ChapterRecord>();
            while (reader.Read())
                results.Add(ReadRecord(reader));
            return Result<IReadOnlyList<ChapterRecord>>.Success(results);
        }
        catch (SqliteException ex)
        {
            return Result<IReadOnlyList<ChapterRecord>>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    /// <summary>Deletes a chapter row. Cascades to its songs via the schema's `ON DELETE CASCADE`
    /// (FR-1.8.5) — the caller is responsible for the matching filesystem cleanup
    /// (<see cref="AtomicOperation.ExecuteDelete"/>).</summary>
    public Result Delete(long id)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "DELETE FROM chapters WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            int rowsAffected = command.ExecuteNonQuery();

            return rowsAffected > 0
                ? Result.Success()
                : Result.Failure(new Error(ErrorKind.NotFound, $"No chapter with id {id} was found."));
        }
        catch (SqliteException ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    private static ChapterRecord ReadRecord(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3));
}
