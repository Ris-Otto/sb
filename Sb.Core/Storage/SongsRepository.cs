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
}
