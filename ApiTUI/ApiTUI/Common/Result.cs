using System.Diagnostics.CodeAnalysis;

namespace ApiTUI.Common;

public sealed record Error(string Reason, string Message);

public sealed class Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    private Result(Error? error = null, T? value = default)
    {
        Error = error;
        Value = value;
        IsSuccess = error is null;
    }

    public static Result<T> Success(T value) => new Result<T>(value: value);
    public static Result<T> Failure(string reason, string message) => new Result<T>(error: new Error(reason, message));

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error: error);
    }

    public static implicit operator Result<T>(Error error) => Failure(error);
}
