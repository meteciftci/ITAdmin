namespace SasPortal.Api.Contracts.AdManagement;

public sealed class MoveAdComputerOuRequest
{
    public string TargetOuDistinguishedName { get; set; } = string.Empty;
}
