using System.Security.Cryptography;
using System.Text;
using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Identity.Domain.Tokens;

/// <summary>Single-use admin fallback for a lost authenticator; only its hash is stored.</summary>
public sealed class RecoveryCode : Entity<Guid>
{
    public const int CountPerUser = 10;

    private RecoveryCode()
    {
    }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = null!;

    public DateTimeOffset? UsedAt { get; private set; }

    public static RecoveryCode Create(Guid userId, string codeHash, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(now), UserId = userId, CodeHash = codeHash };

    public bool TryUse(string candidateHash, DateTimeOffset now)
    {
        if (UsedAt is not null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeHash), Encoding.UTF8.GetBytes(candidateHash)))
        {
            return false;
        }

        UsedAt = now;
        return true;
    }
}
