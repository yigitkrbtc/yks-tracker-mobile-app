using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Consents;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence;

internal sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetById(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmail(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task<IReadOnlyList<User>> GetDeletionRequestedBefore(DateTimeOffset cutoff, int take, CancellationToken cancellationToken) =>
        await db.Users
            .Where(u => u.Status == UserStatus.DeletionRequested && u.DeletionRequestedAt < cutoff)
            .OrderBy(u => u.DeletionRequestedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> GetUnverifiedCreatedBefore(DateTimeOffset cutoff, int take, CancellationToken cancellationToken) =>
        await db.Users
            .Where(u => u.Status == UserStatus.PendingVerification && u.CreatedAt < cutoff)
            .OrderBy(u => u.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);
}

internal sealed class VerificationCodeRepository(IdentityDbContext db) : IVerificationCodeRepository
{
    public Task<VerificationCode?> Get(Guid userId, CodePurpose purpose, CancellationToken cancellationToken) =>
        db.VerificationCodes.SingleOrDefaultAsync(c => c.UserId == userId && c.Purpose == purpose, cancellationToken);

    public void Add(VerificationCode code) => db.VerificationCodes.Add(code);
}

internal sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHash(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetUnrevokedByFamily(Guid familyId, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetUnrevokedByUser(Guid userId, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}

internal sealed class RecoveryCodeRepository(IdentityDbContext db) : IRecoveryCodeRepository
{
    public async Task<IReadOnlyList<RecoveryCode>> GetUnused(Guid userId, CancellationToken cancellationToken) =>
        await db.RecoveryCodes.Where(c => c.UserId == userId && c.UsedAt == null).ToListAsync(cancellationToken);

    public void Add(RecoveryCode code) => db.RecoveryCodes.Add(code);
}

internal sealed class ConsentRepository(IdentityDbContext db) : IConsentRepository
{
    public void Add(ConsentRecord record) => db.ConsentRecords.Add(record);
}

internal sealed class IdentityUnitOfWork(IdentityDbContext db) : IIdentityUnitOfWork
{
    public void Enqueue(IIntegrationEvent integrationEvent) => db.Enqueue(integrationEvent);

    public Task SaveChanges(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
