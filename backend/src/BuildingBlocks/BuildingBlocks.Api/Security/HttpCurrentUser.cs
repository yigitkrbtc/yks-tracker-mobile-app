using Microsoft.AspNetCore.Http;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Security;

namespace Nirengi.BuildingBlocks.Api.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(NirengiClaims.Subject)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("No authenticated user on this request.");
}
