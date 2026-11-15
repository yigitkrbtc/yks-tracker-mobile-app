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
    public void Requesting_codes_is_not_capped_so_the_owner_always_gets_one()
    {
        var code = NewCode();
        var cooldown = TimeSpan.FromSeconds(60);
        var at = Now;
        for (var i = 0; i < 12; i++)
        {
            at = at.AddMinutes(2);
            code.CanResend(at, cooldown).ShouldBeTrue();
            code.Reissue($"hash-{i + 2}", at, Lifetime);
        }
    }

    [Fact]
    public void Burnt_codes_do_not_limit_the_next_one_there_is_no_account_budget()
    {
        var code = NewCode();
        var at = Now;
        // Many dead codes in a row: each burns after its own 5 attempts, none carries over to the next.
        for (var round = 0; round < 10; round++)
        {
            for (var i = 0; i < 5; i++)
            {
                at = at.AddSeconds(1);
                code.Verify("wrong", at, maxAttempts: 5).IsFailure.ShouldBeTrue();
            }

            code.Verify($"hash-{round}", at, maxAttempts: 5).Error.ShouldBe(IdentityErrors.CodeExpired);

            at = at.AddMinutes(2);
            code.Reissue($"hash-{round + 1}", at, Lifetime);
            code.AttemptCount.ShouldBe(0);
        }

        code.Verify("hash-10", at.AddSeconds(1), maxAttempts: 5).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Fifth_wrong_attempt_kills_the_code_and_a_new_code_starts_afresh()
    {
        var code = NewCode();
        for (var i = 0; i < 4; i++)
        {
            code.Verify("wrong", Now, maxAttempts: 5).Error.ShouldBe(IdentityErrors.InvalidCode);
        }

        code.Verify("wrong", Now, maxAttempts: 5).Error.ShouldBe(IdentityErrors.CodeExpired);
        code.AttemptCount.ShouldBe(5);

        code.Reissue("hash-2", Now.AddMinutes(1), Lifetime);
        code.Verify("wrong", Now.AddMinutes(1), maxAttempts: 5).Error.ShouldBe(IdentityErrors.InvalidCode);
        code.Verify("hash-2", Now.AddMinutes(1), maxAttempts: 5).IsSuccess.ShouldBeTrue();
    }
}
