using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Register(
        Email.Create("ada@example.com").Value, " Ada ", "Yılmaz", "hash", 2008, marketingConsent: false, UserRole.Student, Now);

    [Fact]
    public void Registers_pending_and_verifies_once()
    {
        var user = NewUser();
        user.Status.ShouldBe(UserStatus.PendingVerification);
        user.FirstName.ShouldBe("Ada");

        user.VerifyEmail(Now).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.VerifyEmail(Now).Error.ShouldBe(IdentityErrors.AlreadyVerified);
    }

    [Fact]
    public void Repeated_sign_up_restarts_the_unverified_retention()
    {
        var user = NewUser();

        user.ReplacePendingRegistration("Ada", "Yılmaz", "hash2", 2008, marketingConsent: false, Now.AddDays(6)).IsSuccess.ShouldBeTrue();

        user.CreatedAt.ShouldBe(Now.AddDays(6));
        user.PasswordHash.ShouldBe("hash2");
    }

    [Fact]
    public void Locks_out_after_threshold_failures()
    {
        var user = NewUser();
        for (var i = 0; i < 9; i++)
        {
            user.RecordFailedLogin(Now, threshold: 10, TimeSpan.FromMinutes(15));
        }

        user.IsLockedOut(Now).ShouldBeFalse();
        user.RecordFailedLogin(Now, threshold: 10, TimeSpan.FromMinutes(15));
        user.IsLockedOut(Now.AddMinutes(14)).ShouldBeTrue();
        user.IsLockedOut(Now.AddMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void Deletion_can_be_cancelled_within_grace()
    {
        var user = NewUser();
        user.VerifyEmail(Now);

        user.RequestDeletion(Now).IsSuccess.ShouldBeTrue();
        user.PurgeAt(TimeSpan.FromDays(30)).ShouldBe(Now.AddDays(30));
        user.CancelDeletion().IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.CancelDeletion().Error.ShouldBe(IdentityErrors.NotPendingDeletion);
    }

    [Fact]
    public void Totp_step_is_never_accepted_twice()
    {
        var user = NewUser();
        user.StartTotpEnrollment("secret").IsSuccess.ShouldBeTrue();
        user.ConfirmTotpEnrollment(acceptedStep: 100, Now).IsSuccess.ShouldBeTrue();

        user.TotpEnabled.ShouldBeTrue();
        user.AcceptTotpStep(100).Error.ShouldBe(IdentityErrors.InvalidTotpCode);
        user.AcceptTotpStep(101).IsSuccess.ShouldBeTrue();
        user.StartTotpEnrollment("other").Error.ShouldBe(IdentityErrors.TotpAlreadyEnabled);
    }
}
