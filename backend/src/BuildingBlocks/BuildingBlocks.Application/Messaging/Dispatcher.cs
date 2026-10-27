using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.BuildingBlocks.Application.Messaging;

/// <summary>
/// Resolves the single handler for a command or query and validates the message first.
/// Replaces MediatR (ADR-0007); pipeline behaviour is limited to validation on purpose.
/// Handlers may be internal: calls go through typed wrappers, not reflection or dynamic.
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<(Type Message, Type Response, bool IsCommand), object> Wrappers = new();

    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        Invoke<TResponse>(command, isCommand: true, cancellationToken);

    public Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        Invoke<TResponse>(query, isCommand: false, cancellationToken);

    private async Task<Result<TResponse>> Invoke<TResponse>(object message, bool isCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var messageType = message.GetType();

        var errors = await Validate(message, messageType, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.ValidationFailure<TResponse>(errors);
        }

        var wrapper = (HandlerWrapper<TResponse>)Wrappers.GetOrAdd(
            (messageType, typeof(TResponse), isCommand),
            key => Activator.CreateInstance(
                (key.IsCommand ? typeof(CommandWrapper<,>) : typeof(QueryWrapper<,>)).MakeGenericType(key.Message, key.Response))!);

        return await wrapper.Handle(message, services, cancellationToken);
    }

    private async Task<IReadOnlyList<Error>> Validate(object message, Type messageType, CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(messageType);
        var validators = services.GetServices(validatorType).OfType<IValidator>().ToList();
        if (validators.Count == 0)
        {
            return [];
        }

        var context = new ValidationContext<object>(message);
        var errors = new List<Error>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            errors.AddRange(result.Errors.Select(f => Error.Validation(f.PropertyName, f.ErrorMessage)));
        }

        return errors;
    }

    private abstract class HandlerWrapper<TResponse>
    {
        public abstract Task<Result<TResponse>> Handle(object message, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class CommandWrapper<TCommand, TResponse> : HandlerWrapper<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<Result<TResponse>> Handle(object message, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<ICommandHandler<TCommand, TResponse>>().Handle((TCommand)message, cancellationToken);
    }

    private sealed class QueryWrapper<TQuery, TResponse> : HandlerWrapper<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<Result<TResponse>> Handle(object message, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<IQueryHandler<TQuery, TResponse>>().Handle((TQuery)message, cancellationToken);
    }
}
