using System.Diagnostics.CodeAnalysis;

namespace ServiceScheduler.Domain.Common;

/// <summary>Factory methods for <see cref="Result{T}"/>.</summary>
public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Result<T>(value);
    }

    public static Result<T> Failure<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(error);
    }
}

/// <summary>Either a value or a <see cref="DomainError"/>; used instead of exceptions for expected failures.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    internal Result(DomainError error)
    {
        Error = error;
    }

    /// <summary>When false, <see cref="Error"/> is set, so callers never need the null-forgiving operator.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public DomainError? Error { get; }

    // Safe: _value is always assigned when IsSuccess is true.
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result failed with '{Error.Code}'; check IsSuccess before reading Value.");
}
