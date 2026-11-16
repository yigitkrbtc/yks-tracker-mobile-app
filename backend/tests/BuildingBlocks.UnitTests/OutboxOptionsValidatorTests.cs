using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class OutboxOptionsValidatorTests
{
    private static readonly OutboxOptionsValidator Validator = new();

    [Fact]
    public void Defaults_are_valid() => Validator.Validate(null, new OutboxOptions()).Succeeded.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Settings_that_would_spin_or_never_run_are_rejected(string setting, Action<OutboxOptions> break_)
    {
        var options = new OutboxOptions();
        break_(options);

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(setting);
    }

    public static TheoryData<string, Action<OutboxOptions>> Invalid() => new()
    {
        { "PollInterval", o => o.PollInterval = TimeSpan.Zero },
        { "BatchSize", o => o.BatchSize = 0 },
        { "BaseDelay", o => o.BaseDelay = TimeSpan.Zero },
        { "MaxDelay", o => o.MaxDelay = o.BaseDelay - TimeSpan.FromMilliseconds(1) },
        { "AlertAfterAttempts", o => o.AlertAfterAttempts = 0 },
        { "RetentionPeriod", o => o.RetentionPeriod = TimeSpan.FromSeconds(-1) },
        { "CleanupInterval", o => o.CleanupInterval = TimeSpan.Zero },
        { "StaleAfter", o => o.StaleAfter = TimeSpan.Zero },
    };
}
