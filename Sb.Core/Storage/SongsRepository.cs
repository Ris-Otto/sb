using Microsoft.Data.Sqlite;

namespace Sb.Core.Storage;

public sealed class SongsRepository(Database database)
{
    public Result<long> Insert(long chapterId, Song song)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO songs (chapter_id, title, number) VALUES ($chapterId, $title, $number);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$chapterId", chapterId);
            command.Parameters.AddWithValue("$title", song.Title);
            command.Parameters.AddWithValue("$number", song.Number);
            return Result<long>.Success((long)command.ExecuteScalar()!);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintUnique)
        {
            return Result<long>.Failure(
                new Error(
                    ErrorKind.Conflict, $"A song titled '{song.Title}' already exists in this chapter.", song.Title));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintForeignKey)
        {
            return Result<long>.Failure(new Error(ErrorKind.NotFound, $"No chapter with id {chapterId} was found."));
        }
        catch (SqliteException ex)
        {
            return Result<long>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    public Result<SongRecord> GetByTitle(long chapterId, string title)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText =
            "SELECT id, chapter_id, title, number FROM songs WHERE chapter_id = $chapterId AND title = $title;";
        command.Parameters.AddWithValue("$chapterId", chapterId);
        command.Parameters.AddWithValue("$title", title);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return Result<SongRecord>.Failure(
                new Error(ErrorKind.NotFound, $"No song titled '{title}' was found.", title));

        return Result<SongRecord>.Success(
            new SongRecord(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt32(3)));
    }

    /// <summary>All songs belonging to a chapter, in number order (FR-1.4.1, FR-1.9.2).</summary>
    public Result<IReadOnlyList<SongRecord>> GetAllForChapter(long chapterId)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                "SELECT id, chapter_id, title, number FROM songs WHERE chapter_id = $chapterId ORDER BY number;";
            command.Parameters.AddWithValue("$chapterId", chapterId);
            using var reader = command.ExecuteReader();

            var results = new List<SongRecord>();
            while (reader.Read())
                results.Add(new SongRecord(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt32(3)));
            return Result<IReadOnlyList<SongRecord>>.Success(results);
        }
        catch (SqliteException ex)
        {
            return Result<IReadOnlyList<SongRecord>>.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    public Result Delete(long id)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = "DELETE FROM songs WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            int rowsAffected = command.ExecuteNonQuery();

            return rowsAffected > 0
                ? Result.Success()
                : Result.Failure(new Error(ErrorKind.NotFound, $"No song with id {id} was found."));
        }
        catch (SqliteException ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }

    /// <summary>Updates a song's chapter, title, and number together (FR-1.11.4) — covering a plain
    /// renumber (chapter/title unchanged) up through a full move/rename/renumber in one statement, so
    /// callers never need more than one of these calls per song.</summary>
    public Result UpdateChapterTitleAndNumber(long id, long chapterId, string title, int number)
    {
        try
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText =
                "UPDATE songs SET chapter_id = $chapterId, title = $title, number = $number WHERE id = $id;";
            command.Parameters.AddWithValue("$chapterId", chapterId);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$number", number);
            command.Parameters.AddWithValue("$id", id);
            int rowsAffected = command.ExecuteNonQuery();

            return rowsAffected > 0
                ? Result.Success()
                : Result.Failure(new Error(ErrorKind.NotFound, $"No song with id {id} was found."));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintUnique)
        {
            return Result.Failure(
                new Error(ErrorKind.Conflict, $"A song titled '{title}' already exists in that chapter.", title));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrorCodes.ConstraintForeignKey)
        {
            return Result.Failure(new Error(ErrorKind.NotFound, $"No chapter with id {chapterId} was found."));
        }
        catch (SqliteException ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage, ex.Message));
        }
    }
}
