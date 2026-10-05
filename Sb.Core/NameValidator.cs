namespace Sb.Core;

/// <summary>
/// Validates a document/chapter/song name before it's used as a folder/file name on disk
/// (FR-1.8.8). The banned set is a platform-independent superset of Windows' reserved filename
/// characters, so a name accepted here is safe on every platform SB runs on (NFR-2.4.1), not just
/// the one it happened to be validated on.
/// </summary>
public static class NameValidator
{
    private static readonly char[] IllegalCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public static Result Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new Error(ErrorKind.Validation, "A name cannot be empty."));

        if (name.Contains(".."))
            return Result.Failure(new Error(ErrorKind.Validation, "A name cannot contain '..'.", name));

        foreach (char c in name)
        {
            if (c < 32 || IllegalCharacters.Contains(c))
                return Result.Failure(
                    new Error(ErrorKind.Validation, $"A name cannot contain the character '{c}'.", name));
        }

        return Result.Success();
    }
}
