using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.SignIn;

/// <summary>Resolves an MFA token to the active admin it was issued for, at the expected step.</summary>
internal sealed class MfaGate(ITokenIssuer tokens, IUserRepository users)
{
    public async Task<Result<User>> Open(string mfaToken, MfaStep expected, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ticket = await tokens.ReadMfaToken(mfaToken);
        if (ticket is null || ticket.Step != expected)
        {
            return IdentityErrors.InvalidMfaToken;
        }

        var user = await users.GetById(ticket.UserId, cancellationToken);
        if (user is null || !user.IsAdmin || user.Status != UserStatus.Active || user.IsLockedOut(now))
        {
            return IdentityErrors.InvalidMfaToken;
        }

        return user;
    }
}
