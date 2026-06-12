namespace SasPortal.Application.Common.AdManagement;

public static class AdComputerAccountGuard
{
    public const int DomainControllersPrimaryGroupId = 516;
    public const int ServerTrustAccountFlag = 0x2000;
    public const int PartialSecretsAccountFlag = 0x04000000;

    public const string ProtectedComputerMessage =
        "Bu bilgisayar hesabı üzerinde etkinleştirme veya devre dışı bırakma işlemi yapılamaz.";

    public const string ProtectedComputerWriteOperationMessage =
        "Bu bilgisayar hesabı üzerinde bu işlem yapılamaz.";

    public const string ProtectedComputerDeleteMessage =
        "Bu bilgisayar hesabı silinemez.";

    public const string ProtectedComputerGroupMembershipMessage =
        "Bu bilgisayar hesabında grup üyeliği değiştirilemez.";

    public static bool IsProtectedComputer(
        int? primaryGroupId,
        int? userAccountControl,
        bool? isCriticalSystemObject)
    {
        if (isCriticalSystemObject == true)
        {
            return true;
        }

        if (primaryGroupId == DomainControllersPrimaryGroupId)
        {
            return true;
        }

        if (userAccountControl is null)
        {
            return false;
        }

        var flags = userAccountControl.Value;
        if ((flags & ServerTrustAccountFlag) != 0 && (flags & PartialSecretsAccountFlag) != 0)
        {
            return true;
        }

        return false;
    }
}
