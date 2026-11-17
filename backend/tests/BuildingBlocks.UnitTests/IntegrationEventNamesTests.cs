using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class IntegrationEventNamesTests
{
    [Fact]
    public void A_stable_name_resolves_to_its_event_type_and_back()
    {
        IntegrationEventNames.NameOf(typeof(NamedEvent)).ShouldBe("test.named-event");
        IntegrationEventNames.Resolve("test.named-event").ShouldBe(typeof(NamedEvent));
    }

    [Fact]
    public void A_legacy_assembly_qualified_name_is_not_resolved_here_so_the_caller_falls_back_to_the_type_lookup()
    {
        IntegrationEventNames.Resolve(typeof(NamedEvent).AssemblyQualifiedName!).ShouldBeNull();
        Type.GetType(typeof(NamedEvent).AssemblyQualifiedName!, throwOnError: true).ShouldBe(typeof(NamedEvent));
    }

    [Fact]
    public void Writing_an_event_without_a_stable_name_fails_loudly()
    {
        Should.Throw<InvalidOperationException>(() => IntegrationEventNames.NameOf(typeof(UnnamedEvent)));
    }

    [IntegrationEventName("test.named-event")]
    private sealed record NamedEvent(Guid Id, DateTimeOffset OccurredAt) : IIntegrationEvent;

    private sealed record UnnamedEvent(Guid Id, DateTimeOffset OccurredAt) : IIntegrationEvent;
}
