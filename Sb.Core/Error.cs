namespace Sb.Core;

public enum ErrorKind
{
    Validation,
    Conflict,
    NotFound,
    Storage
}

public sealed record Error(ErrorKind Kind, string Message, string? SubjectName = null);
