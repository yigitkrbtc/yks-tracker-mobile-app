using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Identity.Application.SignIn;

namespace Nirengi.Modules.Identity.Application;

public static class DependencyInjection
{
    /// <summary>Application services shared by several handlers (handlers themselves are registered by the module).</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<SignInService>();
        services.AddScoped<CredentialChecker>();
        services.AddScoped<VerificationCodes>();
        return services;
    }
}
