using Nirengi.BuildingBlocks.Domain.Entities;
using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Identity.Domain.Users;

public enum UserStatus
{
    PendingVerification = 0,
    Active = 1,
    DeletionRequested = 2,
}

public enum UserRole
{
    Student = 0,
    Admin = 1,
}

public sealed class User : AggregateRoot<Guid>
{
    public const int NameMaxLength = 50;

    private User()
    {
    }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public int BirthYear { get; private set; }

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public bool MarketingConsent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockoutUntil { get; private set; }

    /// <summary>TOTP secret, encrypted by the infrastructure; null until enrolment is confirmed.</summary>
    public string? TotpSecret { get; private set; }

    public string? PendingTotpSecret { get; private set; }

    public DateTimeOffset? TotpEnabledAt { get; private set; }

    /// <summary>Last accepted TOTP time step; a code is never accepted twice.</summary>
    public long? TotpLastStep { get; private set; }

    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    public bool IsAdmin => Role == UserRole.Admin;

    public bool TotpEnabled => TotpSecret is not null;

    public static User Register(
        Email email,
        string firstName,
        string lastName,
        string passwordHash,
        int birthYear,
        bool marketingConsent,
        UserRole role,
        DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(now),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Value,
            NormalizedEmail = email.Normalized,
            PasswordHash = passwordHash,
            BirthYear = birthYear,
            MarketingConsent = marketingConsent,
            Role = role,
            Status = UserStatus.PendingVerification,
            CreatedAt = now,
        };

    /// <summary>
    /// A repeated sign-up before verification replaces the details: whoever proves the mailbox owns the account,
    /// so an unverified registration cannot squat someone else's address.
    /// </summary>
    public Result ReplacePendingRegistration(string firstName, string lastName, string passwordHash, int birthYear, bool marketingConsent)
    {
        if (Status != UserStatus.PendingVerification)
        {
            return IdentityErrors.AlreadyVerified;
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PasswordHash = passwordHash;
        BirthYear = birthYear;
        MarketingConsent = marketingConsent;
        return Result.Success();
    }

    public Result VerifyEmail(DateTimeOffset now)
    {
        if (Status != UserStatus.PendingVerification)
        {
            return IdentityErrors.AlreadyVerified;
        }

        Status = UserStatus.Active;
        EmailVerifiedAt = now;
        return Result.Success();
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutUntil > now;

    public void RecordFailedLogin(DateTimeOffset now, int threshold, TimeSpan lockout)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= threshold)
        {
            LockoutUntil = now + lockout;
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutUntil = null;
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        RecordSuccessfulLogin();
    }

    public Result RequestDeletion(DateTimeOffset now)
    {
        if (Status == UserStatus.DeletionRequested)
        {
            return Result.Success();
        }

        Status = UserStatus.DeletionRequested;
        DeletionRequestedAt = now;
        return Result.Success();
    }

    public Result CancelDeletion()
    {
        if (Status != UserStatus.DeletionRequested)
        {
            return IdentityErrors.NotPendingDeletion;
        }

        Status = UserStatus.Active;
        DeletionRequestedAt = null;
        return Result.Success();
    }

    public DateTimeOffset? PurgeAt(TimeSpan gracePeriod) => DeletionRequestedAt + gracePeriod;

    public Result StartTotpEnrollment(string protectedSecret)
    {
        if (TotpEnabled)
        {
            return IdentityErrors.TotpAlreadyEnabled;
        }

        PendingTotpSecret = protectedSecret;
        return Result.Success();
    }

    public Result ConfirmTotpEnrollment(long acceptedStep, DateTimeOffset now)
    {
        if (TotpEnabled)
        {
            return IdentityErrors.TotpAlreadyEnabled;
        }

        if (PendingTotpSecret is null)
        {
            return IdentityErrors.TotpSetupNotStarted;
        }

        TotpSecret = PendingTotpSecret;
        PendingTotpSecret = null;
        TotpEnabledAt = now;
        TotpLastStep = acceptedStep;
        return Result.Success();
    }

    public Result AcceptTotpStep(long step)
    {
        if (step <= TotpLastStep)
        {
            return IdentityErrors.InvalidTotpCode;
        }

        TotpLastStep = step;
        return Result.Success();
    }
}
