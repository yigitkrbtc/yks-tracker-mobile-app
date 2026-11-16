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
    IClock clock,
    IdentityOptions options) : ICommandHandler<RefreshSessionCommand, AuthTokens>
{
    private const int MaxGraceChain = 5;

    public async Task<Result<AuthTokens>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var token = await refreshTokens.GetByHash(secrets.Hash(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var reason = RevokeReason.Rotated;
        if (token.RevokeReason is RevokeReason.Rotated or RevokeReason.Superseded)
        {
            var successor = token.RevokeReason == RevokeReason.Rotated
                ? await UnusedSuccessorInGrace(token, now, cancellationToken)
                : null;
            if (successor is null)
            {
                await signIn.RevokeFamily(token.FamilyId, RevokeReason.ReuseDetected, now, cancellationToken);
                await unitOfWork.TrySaveChanges(cancellationToken);
                return IdentityErrors.InvalidRefreshToken;
            }

            // The answer to the rotation was lost on the way: rotate the token the caller never received.
            token = successor;
            reason = RevokeReason.Superseded;
        }

        if (!token.IsActive(now))
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var user = await users.GetById(token.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            await signIn.RevokeFamily(token.FamilyId, RevokeReason.UserInactive, now, cancellationToken);
            await unitOfWork.TrySaveChanges(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        // Removed from AdminEmails: the admin session ends; the next sign-in issues a student session (ADR-0021).
        if (user.IsAdmin && options.RoleFor(user.NormalizedEmail) != UserRole.Admin)
        {
            user.ChangeRole(UserRole.Student);
            await signIn.RevokeAll(user.Id, RevokeReason.RoleChanged, now, cancellationToken);
            await unitOfWork.TrySaveChanges(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        // An admin session never passed the second factor (the account was promoted after this token was issued).
        if (user.IsAdmin && !token.IssuedWithMfa)
        {
            await signIn.RevokeFamily(token.FamilyId, RevokeReason.RoleChanged, now, cancellationToken);
            await unitOfWork.TrySaveChanges(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        var tokens = signIn.Rotate(user, token, now, reason);

        // Another request rotated or revoked this token in the meantime: it is spent for this caller.
        return await unitOfWork.TrySaveChanges(cancellationToken) ? tokens : IdentityErrors.InvalidRefreshToken;
    }

    /// <summary>
    /// The active token at the end of the replacement chain that starts at <paramref name="rotated"/>, when every step
    /// is within the grace period: the first token was rotated normally, later ones were superseded (their holder never
    /// received them either). A successor that was rotated on its own was presented, so the chain stops there: reuse.
    /// </summary>
    private async Task<RefreshToken?> UnusedSuccessorInGrace(RefreshToken rotated, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = rotated;
        for (var step = 0; step < MaxGraceChain; step++)
        {
            var expected = step == 0 ? RevokeReason.Rotated : RevokeReason.Superseded;
            if (current.RevokeReason != expected
                || current.RevokedAt is not { } revokedAt
                || now - revokedAt > options.RefreshReuseGrace
                || current.ReplacedById is not { } nextId)
            {
                return null;
            }

            var next = await refreshTokens.GetById(nextId, cancellationToken);
            if (next is null)
            {
                return null;
            }

            if (next.IsActive(now))
            {
                return next;
            }

            current = next;
        }

        return null;
    }
}
