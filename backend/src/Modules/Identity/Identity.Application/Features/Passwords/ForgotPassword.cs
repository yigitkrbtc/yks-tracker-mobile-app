using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Passwords;

/// <summary>Mails a reset code to verified accounts; always succeeds so it cannot probe for accounts.</summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand<Unit>;

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
}

internal sealed class ForgotPasswordHandler(
    IUserRepository users,
    VerificationCodes codes,
    IEmailSender email,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<ForgotPasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmail(Email.Normalize(command.Email), cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Unit.Value;
        }

        var code = await codes.Issue(user, CodePurpose.PasswordReset, clock.UtcNow, cancellationToken);
        if (code is null)
        {
            return Unit.Value;
        }

        await unitOfWork.SaveChanges(cancellationToken);
        await email.Send(IdentityEmails.PasswordResetCode(user.Email, user.FirstName, code, options.CodeLifetime), cancellationToken);
        return Unit.Value;
    }
}
