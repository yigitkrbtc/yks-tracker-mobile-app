using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Delivers a deserialized event to every in-process handler through a typed wrapper (handlers may be internal).</summary>
internal static class IntegrationEventPublisher
{
    private static readonly ConcurrentDictionary<Type, Publisher> Publishers = new();

    public static Task Publish(object integrationEvent, Type eventType, IServiceProvider services, CancellationToken cancellationToken)
    {
        var publisher = Publishers.GetOrAdd(
            eventType,
            type => (Publisher)Activator.CreateInstance(typeof(Publisher<>).MakeGenericType(type))!);
        return publisher.Publish(integrationEvent, services, cancellationToken);
    }

    private abstract class Publisher
    {
        public abstract Task Publish(object integrationEvent, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class Publisher<TEvent> : Publisher
        where TEvent : IIntegrationEvent
    {
        public override async Task Publish(object integrationEvent, IServiceProvider services, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
            {
                await handler.Handle((TEvent)integrationEvent, cancellationToken);
            }
        }
    }
}
