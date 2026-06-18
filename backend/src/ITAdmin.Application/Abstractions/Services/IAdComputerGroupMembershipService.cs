using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdComputerGroupMembershipService
{
    Task<AdComputerGroupMembershipResult> GetComputerGroupsAsync(
        AdComputerGroupMembershipRequest request,
        CancellationToken cancellationToken = default);

    Task<AdComputerGroupSearchResult> SearchGroupCandidatesAsync(
        AdComputerGroupSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AdComputerGroupOperationResult> AddComputerToGroupAsync(
        AddAdComputerToGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<AdComputerGroupOperationResult> RemoveComputerFromGroupAsync(
        RemoveAdComputerFromGroupRequest request,
        CancellationToken cancellationToken = default);
}
