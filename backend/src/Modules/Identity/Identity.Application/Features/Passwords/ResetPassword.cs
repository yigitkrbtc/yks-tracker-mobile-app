using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Passwords;

/// <summary>Sets a new password with the mailed code and signs out every device.</summary>
public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword) : ICommand<Unit>;

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(c => c.Code).NotEmpty().Length(VerificationCodes.Digits).Matches("^[0-9]+$");
        RuleFor(c => c.NewPassword).StrongPassword()
            .Must((c, password) => !string.Equals(password, c.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Şifre e-posta adresinle aynı olamaz.");
    }
}

internal sealed class ResetPasswordHandler(
    IUserRepository users,
    VerificationCodes codes,
    IPasswordHasher hasher,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmail(Email.Normalize(command.Email), cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return IdentityErrors.InvalidCode;
        }

        var now = clock.UtcNow;
        var verified = await codes.Verify(user, CodePurpose.PasswordReset, command.Code, now, cancellationToken);
        if (verified.IsFailure)
        {
            await unitOfWork.SaveChanges(cancellationToken);
            return verified.Error;
        }

        user.ChangePassword(hasher.Hash(command.NewPassword));
        await signIn.RevokeAll(user.Id, RevokeReason.PasswordReset, now, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);
        return Unit.Value;
    }
}
