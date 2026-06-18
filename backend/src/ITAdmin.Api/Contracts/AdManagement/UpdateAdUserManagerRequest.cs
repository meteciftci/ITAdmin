namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record UpdateAdUserManagerRequest
{
    public Guid? ManagerUserId { get; init; }
    public bool ClearManager { get; init; }
}
