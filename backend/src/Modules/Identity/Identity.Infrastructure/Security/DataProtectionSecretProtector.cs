using Microsoft.AspNetCore.DataProtection;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

internal sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Nirengi.Identity.Totp");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
