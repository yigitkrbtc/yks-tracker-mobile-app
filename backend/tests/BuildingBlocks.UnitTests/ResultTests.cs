using Nirengi.BuildingBlocks.Domain.Results;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Success_carries_value()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_hides_value()
    {
        Result<int> result = Error.NotFound("exam.not_found", "Exam not found.");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Validation_failure_keeps_field_errors()
    {
        var result = Result.ValidationFailure<int>([Error.Validation("Correct", "Too many.")]);

        result.Error.ShouldBe(ValidationError.Root);
        result.Details.Single().Code.ShouldBe("Correct");
    }
}
