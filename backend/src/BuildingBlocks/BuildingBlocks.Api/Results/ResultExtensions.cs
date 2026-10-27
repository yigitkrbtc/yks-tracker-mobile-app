using Microsoft.AspNetCore.Http;
using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.BuildingBlocks.Api.Results;

/// <summary>Maps <see cref="Result"/> failures to RFC 9457 ProblemDetails responses.</summary>
public static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result has no problem to report.");
        }

        if (result.Error.Type == ErrorType.Validation && result.Details.Count > 0)
        {
            var errors = result.Details
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return Microsoft.AspNetCore.Http.Results.ValidationProblem(errors, title: result.Error.Message, type: result.Error.Code);
        }

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: StatusCode(result.Error.Type),
            title: result.Error.Message,
            type: result.Error.Code);
    }

    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.ToProblem();

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
