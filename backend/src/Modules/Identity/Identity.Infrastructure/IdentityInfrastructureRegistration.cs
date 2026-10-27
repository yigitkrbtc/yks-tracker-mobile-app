using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Security;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Identity.Application;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Infrastructure.Mail;
using Nirengi.Modules.Identity.Infrastructure.Persistence;
using Nirengi.Modules.Identity.Infrastructure.Security;

namespace Nirengi.Modules.Identity.Infrastructure;

public static class IdentityInfrastructureRegistration
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<IdentityDbContext>(configuration, IdentityDbContext.SchemaName, Application.AssemblyReference.Assembly);
        services.AddIdentityApplication();

        services.AddOptions<IdentityOptions>().BindConfiguration(IdentityOptions.SectionName);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<IdentityOptions>>().Value);
        services.AddOptions<IdentitySecurityOptions>()
            .BindConfiguration(IdentitySecurityOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => Base64Length(o.HashKey) >= 32, "Identity:Security:HashKey must be base64 of at least 32 bytes.")
            .ValidateOnStart();
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRecoveryCodeRepository, RecoveryCodeRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOneTimeSecrets, HmacOneTimeSecrets>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<ITotpAuthenticator, OtpNetTotpAuthenticator>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddOptions<AccountPurgeOptions>().BindConfiguration(AccountPurgeOptions.SectionName);
        services.AddHostedService<AccountPurgeWorker>();

        services.AddDataProtection().SetApplicationName("Nirengi").PersistKeysToDbContext<IdentityDbContext>();
        return services;
    }

    private static int Base64Length(string value)
    {
        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out var written) ? written : 0;
    }
}
