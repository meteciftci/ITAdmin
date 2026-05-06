namespace SasPortal.Application.Common.Models;

public sealed record UpdateCurrentUserPreferencesRequest(
    Guid UserId,
    string PreferredLanguage,
    string? ActorUserName);
