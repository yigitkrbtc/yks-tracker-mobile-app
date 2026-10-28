using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Consents;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Registration;

/// <summary>
/// Always answers 202 for a well-formed request, whether or not the address is taken, so sign-up cannot be
/// used to probe which addresses have accounts. The mailbox owner learns the outcome by e-mail.
/// </summary>
public sealed record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    int BirthYear,
    string TermsVersion,
    string PrivacyNoticeVersion,
    bool MarketingConsent) : ICommand<Unit>;

internal sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(User.NameMaxLength);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(User.NameMaxLength);
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(c => c.Password).StrongPassword()
            .Must((c, password) => !string.Equals(password, c.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Şifre e-posta adresinle aynı olamaz.");
        RuleFor(c => c.BirthYear).InclusiveBetween(AgePolicy.EarliestBirthYear, 2100);
        RuleFor(c => c.TermsVersion).NotEmpty().MaximumLength(20);
        RuleFor(c => c.PrivacyNoticeVersion).NotEmpty().MaximumLength(20);
    }
}

internal sealed class RegisterHandler(
    IUserRepository users,
    IConsentRepository consents,
    VerificationCodes codes,
    IPasswordHasher hasher,
    IEmailSender email,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<RegisterCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var address = Email.Create(command.Email);
        if (address.IsFailure)
        {
            return address.Error;
        }

        var now = clock.UtcNow;
        var currentYear = AgePolicy.CurrentYear(now);
        if (!AgePolicy.CanRegister(command.BirthYear, currentYear))
        {
            return IdentityErrors.Underage;
        }

        var isAdult = AgePolicy.IsAdult(command.BirthYear, currentYear);
        if (command.MarketingConsent && !isAdult)
        {
            return IdentityErrors.MarketingConsentNotAllowed;
        }

        if (command.TermsVersion != options.TermsVersion || command.PrivacyNoticeVersion != options.PrivacyNoticeVersion)
        {
            return IdentityErrors.OutdatedTerms;
        }

        var passwordHash = hasher.Hash(command.Password);
        var user = await users.GetByEmail(address.Value.Normalized, cancellationToken);
        if (user is not null && user.Status != UserStatus.PendingVerification)
        {
            await email.Send(IdentityEmails.AccountExists(user.Email, user.FirstName), cancellationToken);
            return Unit.Value;
        }

        if (user is null)
        {
            var role = options.AdminEmails.Any(a => Email.Normalize(a) == address.Value.Normalized) ? UserRole.Admin : UserRole.Student;
            user = User.Register(address.Value, command.FirstName, command.LastName, passwordHash, command.BirthYear, command.MarketingConsent, role, now);
            users.Add(user);
        }
        else
        {
            user.ReplacePendingRegistration(command.FirstName, command.LastName, passwordHash, command.BirthYear, command.MarketingConsent, now);
        }

        consents.Add(ConsentRecord.Record(user.Id, ConsentKind.Terms, options.TermsVersion, granted: true, now));
        consents.Add(ConsentRecord.Record(user.Id, ConsentKind.PrivacyNotice, options.PrivacyNoticeVersion, granted: true, now));
        if (isAdult)
        {
            consents.Add(ConsentRecord.Record(user.Id, ConsentKind.Marketing, options.MarketingConsentVersion, command.MarketingConsent, now));
        }

        var code = await codes.Issue(user, CodePurpose.EmailVerification, now, cancellationToken);

        // A concurrent sign-up for the same address won; its code is the one in the mailbox.
        if (!await unitOfWork.TrySaveChanges(cancellationToken))
        {
            return Unit.Value;
        }

        if (code is not null)
        {
            await email.Send(IdentityEmails.VerificationCode(user.Email, user.FirstName, code, options.CodeLifetime), cancellationToken);
        }

        return Unit.Value;
    }
}
