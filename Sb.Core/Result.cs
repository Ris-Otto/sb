namespace Sb.Core;

/// <summary>
/// Front-end-agnostic outcome of an Sb.Core operation (NFR-2.2.2). A front-end (CLI, GUI, API, ...)
/// inspects <see cref="IsSuccess"/>/<see cref="Error"/> and translates it into its own terms
/// (e.g. the CLI maps <see cref="Error.Kind"/> to a process exit code).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    // Invariant (success <=> no error) is enforced by construction: Success()/Failure(Error) are the
    // only call sites, and each always passes a consistent (isSuccess, error) pair.
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    private Result(bool isSuccess, T? value, Error? error) : base(isSuccess, error) => _value = value;

    public static Result<T> Success(T value) => new(true, value, null);
    public new static Result<T> Failure(Error error) => new(false, default, error);
}
