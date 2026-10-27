using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Consents;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetById(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmail(string normalizedEmail, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> GetDeletionRequestedBefore(DateTimeOffset cutoff, int take, CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>Hard delete; codes, tokens and consent records cascade.</summary>
    void Remove(User user);
}

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> Get(Guid userId, CodePurpose purpose, CancellationToken cancellationToken);

    void Add(VerificationCode code);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHash(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetUnrevokedByFamily(Guid familyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetUnrevokedByUser(Guid userId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public interface IRecoveryCodeRepository
{
    Task<IReadOnlyList<RecoveryCode>> GetUnused(Guid userId, CancellationToken cancellationToken);

    void Add(RecoveryCode code);
}

public interface IConsentRepository
{
    void Add(ConsentRecord record);
}

/// <summary>Commits the Identity changes and queued integration events in one transaction.</summary>
public interface IIdentityUnitOfWork : IEventOutbox
{
    Task SaveChanges(CancellationToken cancellationToken);
}
