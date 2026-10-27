using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.Modules.Identity.Contracts;

/// <summary>
/// The account is gone for good (30-day grace over). Every module deletes the user's data on receipt (ADR-0004);
/// handlers must be idempotent.
/// </summary>
public sealed record UserDeleted(Guid Id, DateTimeOffset OccurredAt, Guid UserId) : IIntegrationEvent;
