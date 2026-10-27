using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Domain.Results;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
    public void Error_type_maps_to_status_code(ErrorType type, int expected)
    {
        var problem = Result.Failure(new Error("x", "y", type)).ToProblem();

        problem.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(expected);
    }

    [Fact]
    public void Validation_failure_maps_to_validation_problem()
    {
        var problem = Result.ValidationFailure<int>([Error.Validation("Wrong", "Too many.")]).ToProblem();

        var details = problem.ShouldBeOfType<ProblemHttpResult>().ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        details.Errors["Wrong"].ShouldBe(["Too many."]);
    }
}
