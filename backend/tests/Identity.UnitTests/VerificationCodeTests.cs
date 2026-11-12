using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Codes;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class VerificationCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private static VerificationCode NewCode() =>
        VerificationCode.Issue(Guid.CreateVersion7(), CodePurpose.EmailVerification, "hash-1", Now, Lifetime);

    [Fact]
    public void Correct_code_is_consumed_once()
    {
        var code = NewCode();

        code.Verify("hash-1", Now.AddMinutes(1), maxAttempts: 5).IsSuccess.ShouldBeTrue();
        code.Verify("hash-1", Now.AddMinutes(2), maxAttempts: 5).Error.ShouldBe(IdentityErrors.CodeExpired);
    }

    [Fact]
    public void Expired_code_is_rejected()
    {
        NewCode().Verify("hash-1", Now + Lifetime, maxAttempts: 5).Error.ShouldBe(IdentityErrors.CodeExpired);
    }

    [Fact]
    public void Code_burns_after_max_attempts()
    {
        var code = NewCode();
        for (var i = 0; i < 4; i++)
        {
            code.Verify("wrong", Now, maxAttempts: 5).Error.ShouldBe(IdentityErrors.InvalidCode);
        }

        code.Verify("wrong", Now, maxAttempts: 5).Error.ShouldBe(IdentityErrors.CodeExpired);
        code.Verify("hash-1", Now, maxAttempts: 5).Error.ShouldBe(IdentityErrors.CodeExpired);
    }

    [Fact]
    public void Reissue_resets_attempts_and_cooldown_applies()
    {
        var code = NewCode();
        code.Verify("wrong", Now, maxAttempts: 5);

        code.CanResend(Now.AddSeconds(30), TimeSpan.FromSeconds(60)).ShouldBeFalse();
        code.CanResend(Now.AddSeconds(60), TimeSpan.FromSeconds(60)).ShouldBeTrue();

        code.Reissue("hash-2", Now.AddMinutes(1), Lifetime);
        code.AttemptCount.ShouldBe(0);
        code.Verify("hash-2", Now.AddMinutes(2), maxAttempts: 5).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void At_most_five_codes_a_day_then_resend_waits_for_the_window()
    {
        var code = NewCode();
        var cooldown = TimeSpan.FromSeconds(60);
        var at = Now;
        for (var i = 1; i < VerificationCode.MaxIssuesPerWindow; i++)
        {
            at = at.AddMinutes(2);
            code.CanResend(at, cooldown).ShouldBeTrue();
            code.Reissue($"hash-{i + 1}", at, Lifetime);
        }

        code.CanResend(at.AddMinutes(2), cooldown).ShouldBeFalse();
        code.CanResend(Now + VerificationCode.IssueWindow, cooldown).ShouldBeTrue();
    }

    [Fact]
    public void A_new_window_starts_the_count_again()
    {
        var code = NewCode();
        code.Reissue("hash-2", Now + VerificationCode.IssueWindow, Lifetime);

        code.IssuesInWindow.ShouldBe(1);
        code.IssueWindowStartedAt.ShouldBe(Now + VerificationCode.IssueWindow);
    }
}
