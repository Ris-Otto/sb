namespace Sb.Core.Storage;

/// <summary>
/// Runs a filesystem change and a database write as a single unit (FR-1.7.6, NFR-2.5.6): if the
/// database write fails after the filesystem change succeeded, the filesystem change is undone, so
/// an SB-driven operation never leaves the filesystem and database diverging.
/// </summary>
/// <remarks>
/// <paramref name="databaseAction"/> (on both overloads) should classify its own expected failures
/// (e.g. <see cref="ErrorKind.Conflict"/> for a duplicate name) by returning a failed <see cref="Result"/>
/// rather than throwing, so that classification survives the rollback. A thrown exception is still
/// caught and reported, but only as a generic <see cref="ErrorKind.Storage"/> error.
/// </remarks>
public static class AtomicOperation
{
    public static Result Execute(ReversibleAction filesystem, Func<Result> databaseAction)
    {
        Result<object?> result = Execute<object?>(filesystem, () =>
        {
            Result dbResult = databaseAction();
            return dbResult.IsSuccess ? Result<object?>.Success(null) : Result<object?>.Failure(dbResult.Error!);
        });

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
    }

    public static Result<T> Execute<T>(ReversibleAction filesystem, Func<Result<T>> databaseAction)
    {
        if (!TryRunFilesystemAction(filesystem, out Error? filesystemError))
            return Result<T>.Failure(filesystemError!);

        Result<T> result;
        try
        {
            result = databaseAction();
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(RollBack(filesystem, new Error(ErrorKind.Storage, $"Database write failed: {ex.Message}")));
        }

        return result.IsSuccess ? result : Result<T>.Failure(RollBack(filesystem, result.Error!));
    }

    private static bool TryRunFilesystemAction(ReversibleAction filesystem, out Error? error)
    {
        try
        {
            filesystem.Do();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = new Error(ErrorKind.Storage, $"Filesystem operation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// The delete-shaped counterpart to <see cref="Execute"/> (FR-1.7.6, FR-1.8.5): the database row
    /// is removed first, since a transactional DB delete is cheaply "undone" by simply not having
    /// happened (nothing runs after it on failure); the filesystem is cleaned up last, since a
    /// recursive folder delete has no cheap undo. If that last step throws, the database is already
    /// consistent on its own — the failure is reported as a best-effort cleanup problem rather than
    /// rolled back.
    /// </summary>
    public static Result ExecuteDelete(Func<Result> databaseAction, Action filesystemCleanup)
    {
        Result dbResult;
        try
        {
            dbResult = databaseAction();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage, $"Database delete failed: {ex.Message}"));
        }

        if (dbResult.IsFailure)
            return dbResult;

        try
        {
            filesystemCleanup();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error(ErrorKind.Storage,
                $"Removed from the database, but the filesystem cleanup failed: {ex.Message} — manual cleanup " +
                "may be required."));
        }

        return Result.Success();
    }

    private static Error RollBack(ReversibleAction filesystem, Error originalError)
    {
        try
        {
            filesystem.Undo();
            return originalError;
        }
        catch (Exception undoEx)
        {
            return originalError with
            {
                Message = $"{originalError.Message} Additionally, rolling back the filesystem change failed: " +
                          $"{undoEx.Message} — manual cleanup may be required."
            };
        }
    }
}
