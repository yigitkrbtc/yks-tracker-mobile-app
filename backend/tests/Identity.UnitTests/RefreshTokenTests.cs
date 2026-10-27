using Nirengi.Modules.Identity.Domain.Tokens;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    [Fact]
    public void Rotation_keeps_family_and_expiry()
    {
        var first = RefreshToken.Issue(Guid.CreateVersion7(), "h1", Now, Lifetime);

        var second = first.Rotate("h2", Now.AddDays(10));

        second.FamilyId.ShouldBe(first.FamilyId);
        second.ExpiresAt.ShouldBe(first.ExpiresAt);
        second.IsActive(Now.AddDays(10)).ShouldBeTrue();
        first.IsActive(Now.AddDays(10)).ShouldBeFalse();
        first.RevokeReason.ShouldBe(RevokeReason.Rotated);
        first.ReplacedById.ShouldBe(second.Id);
    }

    [Fact]
    public void Family_ends_after_lifetime_regardless_of_rotation()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "h1", Now, Lifetime).Rotate("h2", Now.AddDays(29));

        token.IsActive(Now + Lifetime).ShouldBeFalse();
    }

    [Fact]
    public void First_revocation_reason_wins()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "h1", Now, Lifetime);
        token.Revoke(Now, RevokeReason.Logout);
        token.Revoke(Now.AddMinutes(1), RevokeReason.ReuseDetected);

        token.RevokeReason.ShouldBe(RevokeReason.Logout);
        token.RevokedAt.ShouldBe(Now);
    }
}
