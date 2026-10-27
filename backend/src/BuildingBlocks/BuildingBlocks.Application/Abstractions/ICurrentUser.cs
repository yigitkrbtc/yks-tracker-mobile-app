namespace Nirengi.BuildingBlocks.Application.Abstractions;

/// <summary>The authenticated caller of the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's user id; only read on endpoints that require authorization.</summary>
    Guid UserId { get; }
}
