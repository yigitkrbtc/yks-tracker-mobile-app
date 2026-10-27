using Microsoft.AspNetCore.Identity;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's versioned PBKDF2 format, without the rest of ASP.NET Core Identity.</summary>
internal sealed class Pbkdf2PasswordHasher : Application.Abstractions.IPasswordHasher
{
    private static readonly object Subject = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public PasswordCheck Verify(string passwordHash, string password) => _hasher.VerifyHashedPassword(Subject, passwordHash, password) switch
    {
        PasswordVerificationResult.Success => PasswordCheck.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
        _ => PasswordCheck.Failed,
    };
}
