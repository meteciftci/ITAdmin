using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdUserGroupMembershipService
{
    Task<AdUserGroupMembershipResult> GetUserGroupsAsync(
        AdUserGroupMembershipRequest request,
        CancellationToken cancellationToken = default);

    Task<AdUserEffectiveGroupsResult> GetUserEffectiveGroupsAsync(
        AdUserEffectiveGroupsRequest request,
        CancellationToken cancellationToken = default);

    Task<AdGroupSearchResult> SearchGroupsAsync(
        AdGroupSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AdUserGroupOperationResult> AddUserToGroupAsync(
        AddAdUserToGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<AdUserGroupOperationResult> RemoveUserFromGroupAsync(
        RemoveAdUserFromGroupRequest request,
        CancellationToken cancellationToken = default);
}
