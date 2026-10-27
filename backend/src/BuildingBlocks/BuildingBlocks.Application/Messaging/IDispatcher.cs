using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.BuildingBlocks.Application.Messaging;

public interface IDispatcher
{
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
