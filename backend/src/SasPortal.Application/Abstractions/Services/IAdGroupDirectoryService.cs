using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdGroupDirectoryService
{
    Task<AdGroupDirectoryListResult> SearchGroupsAsync(
        AdGroupListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdGroupDirectoryDetailResult> GetGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AdOrganizationalUnitSearchResult> SearchGroupOrganizationalUnitsAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<CreateAdGroupResult> CreateGroupAsync(
        CreateAdGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<AdGroupDirectoryDetailResult> UpdateGroupAsync(
        UpdateAdGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<DeleteAdGroupResult> DeleteGroupAsync(
        DeleteAdGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<AdGroupMembersListResult> GetGroupMembersAsync(
        AdGroupMembersListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdGroupMemberCandidatesResult> SearchGroupMemberCandidatesAsync(
        AdGroupMemberCandidatesQuery query,
        CancellationToken cancellationToken = default);

    Task<AdGroupMemberOperationResult> AddGroupMemberAsync(
        AddAdGroupMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<AdGroupMemberOperationResult> RemoveGroupMemberAsync(
        RemoveAdGroupMemberRequest request,
        CancellationToken cancellationToken = default);
}
