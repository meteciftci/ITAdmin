using System.DirectoryServices.Protocols;

namespace ITAdmin.Infrastructure.Ldap;

public static class AdLdapNoSuchObjectHelper
{
    private const int LdapNoSuchObjectErrorCode = 32;

    public static bool IsDirectoryNoSuchObject(DirectoryOperationException exception) =>
        exception.Response?.ResultCode == ResultCode.NoSuchObject;

    public static bool IsLdapNoSuchObject(LdapException exception) =>
        exception.ErrorCode == LdapNoSuchObjectErrorCode;

    public static bool IsNoSuchObjectResultCode(ResultCode resultCode) =>
        resultCode == ResultCode.NoSuchObject;
}
