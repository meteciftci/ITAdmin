namespace SasPortal.Application.Common.Models;

public sealed record LdapUserProfileByObjectIdRequest(
    string Host,
    int Port,
    bool UseSsl,
    string BaseDn,
    string UserSearchBase,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string DirectoryObjectId,
    string? NationalIdAttribute);
