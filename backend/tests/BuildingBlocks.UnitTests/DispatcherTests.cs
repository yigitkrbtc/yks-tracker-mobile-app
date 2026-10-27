using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class DispatcherTests
{
    private readonly IDispatcher _dispatcher;

    public DispatcherTests()
    {
        var services = new ServiceCollection();
        services.AddApplicationHandlers(typeof(DispatcherTests).Assembly);
        _dispatcher = services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task Command_reaches_its_handler()
    {
        var result = await _dispatcher.Send(new ComputeNet(Correct: 30, Wrong: 8), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(28m);
    }

    [Fact]
    public async Task Invalid_command_never_reaches_handler()
    {
        var result = await _dispatcher.Send(new ComputeNet(Correct: -1, Wrong: 0), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Details.ShouldContain(e => e.Code == nameof(ComputeNet.Correct));
    }

    [Fact]
    public async Task Query_reaches_its_handler()
    {
        var result = await _dispatcher.Query(new Echo("nirengi"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("nirengi");
    }
}

public sealed record ComputeNet(int Correct, int Wrong) : ICommand<decimal>;

internal sealed class ComputeNetValidator : AbstractValidator<ComputeNet>
{
    public ComputeNetValidator()
    {
        RuleFor(c => c.Correct).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Wrong).GreaterThanOrEqualTo(0);
    }
}

internal sealed class ComputeNetHandler : ICommandHandler<ComputeNet, decimal>
{
    public Task<Result<decimal>> Handle(ComputeNet command, CancellationToken cancellationToken) =>
        Task.FromResult<Result<decimal>>(command.Correct - (command.Wrong / 4m));
}

public sealed record Echo(string Text) : IQuery<string>;

internal sealed class EchoHandler : IQueryHandler<Echo, string>
{
    public Task<Result<string>> Handle(Echo query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<string>>(query.Text);
}
