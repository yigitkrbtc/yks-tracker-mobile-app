using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain.Tokens;

namespace Nirengi.Modules.Identity.Application.Features.Sessions;

/// <summary>Ends the device's session (its token family); unknown tokens are ignored.</summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand<Unit>;

internal sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(200);
}

internal sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    IOneTimeSecrets secrets,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var token = await refreshTokens.GetByHash(secrets.Hash(command.RefreshToken), cancellationToken);
        if (token is not null)
        {
            await signIn.RevokeFamily(token.FamilyId, RevokeReason.Logout, clock.UtcNow, cancellationToken);
            await unitOfWork.SaveChanges(cancellationToken);
        }

        return Unit.Value;
    }
}
