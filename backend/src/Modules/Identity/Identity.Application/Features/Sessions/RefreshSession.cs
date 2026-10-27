using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Sessions;

/// <summary>Trades a refresh token for a new pair. A rotated token coming back means theft: the family dies.</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<AuthTokens>;

internal sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator() => RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(200);
}

internal sealed class RefreshSessionHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IOneTimeSecrets secrets,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RefreshSessionCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var token = await refreshTokens.GetByHash(secrets.Hash(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        if (token.RevokeReason == RevokeReason.Rotated)
        {
            await signIn.RevokeFamily(token.FamilyId, RevokeReason.ReuseDetected, now, cancellationToken);
            await unitOfWork.SaveChanges(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        if (!token.IsActive(now))
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var user = await users.GetById(token.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            await signIn.RevokeFamily(token.FamilyId, RevokeReason.UserInactive, now, cancellationToken);
            await unitOfWork.SaveChanges(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        var tokens = signIn.Rotate(user, token, now);
        await unitOfWork.SaveChanges(cancellationToken);
        return tokens;
    }
}
