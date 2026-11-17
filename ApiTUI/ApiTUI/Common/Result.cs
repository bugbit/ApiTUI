using System.Diagnostics.CodeAnalysis;

namespace ApiTUI.Common;

public sealed record Error
{
    public string Reason { get; }
    public string Code { get; }
    public IReadOnlyList<object> Args { get; }

    public Error(string reason, string code, params object[] args)
    {
        Reason = reason;
        Code = code;
        Args = Array.AsReadOnly(args.ToArray());
    }
}

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
    public static Result<T> Failure(string reason, string code, params object[] args) =>
        new Result<T>(error: new Error(reason, code, args));

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error: error);
    }

    public static implicit operator Result<T>(Error error) => Failure(error);
    public static implicit operator Result<T>(T value) => Success(value);
}
