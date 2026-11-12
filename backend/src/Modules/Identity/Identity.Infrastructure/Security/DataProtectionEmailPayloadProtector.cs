using Microsoft.AspNetCore.DataProtection;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>Own purpose string, so a payload can never be opened as (or swapped with) another protected value.</summary>
internal sealed class DataProtectionEmailPayloadProtector(IDataProtectionProvider provider) : IEmailPayloadProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Nirengi.Identity.EmailPayload");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
