namespace Sb.Core.Storage;

/// <summary>
/// SQLite extended result codes (stable, public SQLite constants) used to classify a
/// <c>SQLITE_CONSTRAINT</c> (primary code 19) failure precisely, rather than treating every
/// constraint violation as a naming conflict.
/// </summary>
internal static class SqliteErrorCodes
{
    public const int ConstraintUnique = 2067; // SQLITE_CONSTRAINT_UNIQUE
    public const int ConstraintForeignKey = 787; // SQLITE_CONSTRAINT_FOREIGNKEY
}
