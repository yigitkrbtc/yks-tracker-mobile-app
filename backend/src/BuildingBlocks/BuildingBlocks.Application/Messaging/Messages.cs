using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.BuildingBlocks.Application.Messaging;

public interface ICommand<TResponse>;

public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

/// <summary>Placeholder response for commands that return nothing.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}
