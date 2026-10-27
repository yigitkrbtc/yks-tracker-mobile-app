using System.Diagnostics.CodeAnalysis;

namespace Nirengi.BuildingBlocks.Domain.Results;

public class Result
{
    protected Result(bool isSuccess, Error error, IReadOnlyList<Error>? details = null)
    {
        if (isSuccess == (error != Error.None))
        {
            throw new ArgumentException("Success results carry no error; failures must carry one.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
        Details = details ?? [];
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    /// <summary>Field level errors, filled for validation failures.</summary>
    public IReadOnlyList<Error> Details { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    public static Result<TValue> ValidationFailure<TValue>(IReadOnlyList<Error> errors) =>
        new(default, false, ValidationError.Root, errors);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error, IReadOnlyList<Error>? details = null)
        : base(isSuccess, error, details)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue value) => new(value, true, Error.None);

    public static implicit operator Result<TValue>(Error error) => new(default, false, error);
}
