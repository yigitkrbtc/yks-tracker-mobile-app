using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Registration;

/// <summary>Sends a fresh code if the account is unverified and the cooldown has passed; always succeeds.</summary>
public sealed record ResendVerificationCodeCommand(string Email) : ICommand<Unit>;

internal sealed class ResendVerificationCodeValidator : AbstractValidator<ResendVerificationCodeCommand>
{
    public ResendVerificationCodeValidator() => RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
}

internal sealed class ResendVerificationCodeHandler(
    IUserRepository users,
    VerificationCodes codes,
    IEmailSender email,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<ResendVerificationCodeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResendVerificationCodeCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmail(Email.Normalize(command.Email), cancellationToken);
        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            return Unit.Value;
        }

        var code = await codes.Issue(user, CodePurpose.EmailVerification, clock.UtcNow, cancellationToken);
        if (code is null)
        {
            return Unit.Value;
        }

        await unitOfWork.SaveChanges(cancellationToken);
        await email.Send(IdentityEmails.VerificationCode(user.Email, user.FirstName, code, options.CodeLifetime), cancellationToken);
        return Unit.Value;
    }
}
