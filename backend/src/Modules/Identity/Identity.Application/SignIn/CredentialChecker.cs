using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.SignIn;

/// <summary>
/// E-mail + password check with lockout. Every failure looks the same to the caller, and an unknown
/// address still pays for one hash so response time does not reveal which addresses exist.
/// </summary>
internal sealed class CredentialChecker(
    IUserRepository users,
    IPasswordHasher hasher,
    IIdentityUnitOfWork unitOfWork,
    IdentityOptions options)
{
    private static string? _dummyHash;

    public async Task<Result<User>> Check(string email, string password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmail(Email.Normalize(email), cancellationToken);
        if (user is null)
        {
            hasher.Verify(_dummyHash ??= hasher.Hash(Guid.NewGuid().ToString()), password);
            return IdentityErrors.InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            return IdentityErrors.InvalidCredentials;
        }

        var check = hasher.Verify(user.PasswordHash, password);
        if (check == PasswordCheck.Failed)
        {
            user.RecordFailedLogin(now, options.LockoutThreshold, options.LockoutDuration);
            await unitOfWork.SaveChanges(cancellationToken);
            return IdentityErrors.InvalidCredentials;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePassword(hasher.Hash(password));
        }

        user.RecordSuccessfulLogin();
        return user;
    }
}
