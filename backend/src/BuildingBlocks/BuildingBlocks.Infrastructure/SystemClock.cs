using Nirengi.BuildingBlocks.Application.Abstractions;

namespace Nirengi.BuildingBlocks.Infrastructure;

public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
