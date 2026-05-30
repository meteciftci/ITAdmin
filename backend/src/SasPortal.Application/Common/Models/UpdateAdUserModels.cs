namespace SasPortal.Application.Common.Models;

public sealed record UpdateAdUserRequest(
    Guid UserId,
    string GivenName,
    string Surname,
    string DisplayName,
    string SamAccountName,
    string UserPrincipalName,
    string? Mail,
    string? Department,
    IReadOnlyList<UpdateAdUserMappedAttributeRequest> MappedAttributes,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateAdUserMappedAttributeRequest(
    string LogicalField,
    object? Value);
