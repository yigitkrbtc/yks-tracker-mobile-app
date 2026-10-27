namespace Nirengi.BuildingBlocks.Application.Security;

/// <summary>Claim names in Nirengi access tokens; inbound claim mapping is off, so these are the raw JWT names.</summary>
public static class NirengiClaims
{
    public const string Subject = "sub";

    public const string Role = "role";
}

public static class NirengiRoles
{
    public const string Student = "student";

    public const string Admin = "admin";
}
