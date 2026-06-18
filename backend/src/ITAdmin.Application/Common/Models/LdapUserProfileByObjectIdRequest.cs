namespace ITAdmin.Application.Common.Models;

public sealed record LdapUserProfileByObjectIdRequest(
    string Host,
    string BaseDn,
    string UserSearchBase,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string DirectoryObjectId);
