using Sb.Core;

namespace Sb.Cli;

/// <summary>Maps an <see cref="Sb.Core"/> <see cref="Result"/> to a process exit code (NFR-2.2.2):
/// 0 for success, a distinct non-zero code per <see cref="ErrorKind"/> otherwise (Validation 1, Conflict 2,
/// NotFound 3, Storage 4).</summary>
public static class ExitCodes
{
    public const int Success = 0;

    public static int ForError(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => 1,
        ErrorKind.Conflict => 2,
        ErrorKind.NotFound => 3,
        ErrorKind.Storage => 4,
        _ => 1
    };

    public static int ForResult(Result result) => result.IsSuccess ? Success : ForError(result.Error!.Kind);
}
