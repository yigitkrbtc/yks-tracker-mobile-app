using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Codes;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class VerificationCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly ResendPolicy Policy = ResendPolicy.Default;

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

        code.CanResend(Now.AddSeconds(30), Policy).ShouldBeFalse();
        code.CanResend(Now.AddSeconds(60), Policy).ShouldBeTrue();

        code.Reissue("hash-2", Now.AddMinutes(1), Lifetime, Policy);
        code.AttemptCount.ShouldBe(0);
        code.Verify("hash-2", Now.AddMinutes(2), maxAttempts: 5).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Requesting_codes_is_never_refused_for_good_so_the_owner_always_gets_one()
    {
        var code = NewCode();
        var at = Now;
        for (var i = 0; i < 40; i++)
        {
            // Whatever came before, waiting out the current wait (never more than the cap) always yields a code.
            at += Policy.MaxWait;
            code.CanResend(at, Policy).ShouldBeTrue();
            code.Reissue($"hash-{i + 2}", at, Lifetime, Policy);
        }
    }

    [Fact]
    public void The_wait_between_codes_doubles_after_three_codes_in_an_hour_up_to_the_cap()
    {
        var code = NewCode();
        var at = Now;
        var waits = new List<double>();
        for (var i = 0; i < 6; i++)
        {
            var wait = Policy.WaitAfter(code.CodesInWindow);
            waits.Add(wait.TotalSeconds);
            code.CanResend(at + wait - TimeSpan.FromSeconds(1), Policy).ShouldBeFalse();
            at += wait;
            code.CanResend(at, Policy).ShouldBeTrue();
            code.Reissue($"hash-{i + 2}", at, Lifetime, Policy);
        }

        // Codes 1-3 are followed by the base wait, then it doubles with each further code.
        waits.ShouldBe([60, 60, 60, 120, 240, 480]);
        Policy.WaitAfter(9).ShouldBe(TimeSpan.FromHours(1));
        Policy.WaitAfter(500).ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void A_new_window_starts_when_the_hour_is_over_and_the_wait_is_short_again()
    {
        var code = NewCode();
        var at = Now;
        while (code.CodesInWindow < 8)
        {
            at += Policy.WaitAfter(code.CodesInWindow);
            code.Reissue($"hash-{code.CodesInWindow}", at, Lifetime, Policy);
        }

        code.CanResend(at + TimeSpan.FromSeconds(61), Policy).ShouldBeFalse();
        var hourOver = Now + Policy.Window;
        code.CanResend(hourOver, Policy).ShouldBeTrue();

        code.Reissue("next-window", hourOver, Lifetime, Policy);
        code.CodesInWindow.ShouldBe(1);
        code.WindowStartedAt.ShouldBe(hourOver);
    }

    [Fact]
    public void A_flood_buys_at_most_eight_codes_an_hour_while_a_patient_owner_loses_nothing()
    {
        var code = NewCode();
        var at = Now;
        var issued = 1;
        // The attacker asks every second for a whole day; each code that goes out gives 5 guesses.
        for (var second = 1; second <= 24 * 3600; second++)
        {
            var now = Now.AddSeconds(second);
            if (code.CanResend(now, Policy))
            {
                code.Reissue($"hash-{second}", now, Lifetime, Policy);
                issued++;
                at = now;
            }
        }

        issued.ShouldBeLessThanOrEqualTo(24 * 8 + 1);
        issued.ShouldBeGreaterThan(24 * 7);

        // The owner waits at most an hour from the last code and then gets one.
        code.CanResend(at + Policy.MaxWait, Policy).ShouldBeTrue();
    }

    [Fact]
    public void A_consumed_code_does_not_hold_back_the_next_one()
    {
        var code = NewCode();
        code.Verify("hash-1", Now.AddSeconds(1), maxAttempts: 5).IsSuccess.ShouldBeTrue();

        code.CanResend(Now.AddSeconds(2), Policy).ShouldBeTrue();
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
            code.Reissue($"hash-{round + 1}", at, Lifetime, Policy);
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

        code.Reissue("hash-2", Now.AddMinutes(1), Lifetime, Policy);
        code.Verify("wrong", Now.AddMinutes(1), maxAttempts: 5).Error.ShouldBe(IdentityErrors.InvalidCode);
        code.Verify("hash-2", Now.AddMinutes(1), maxAttempts: 5).IsSuccess.ShouldBeTrue();
    }
}
