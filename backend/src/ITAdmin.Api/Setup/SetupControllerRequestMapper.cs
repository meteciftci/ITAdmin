using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models;

namespace ITAdmin.Api.Setup;

internal static class SetupControllerRequestMapper
{
    public static bool TryMapCompleteSetupRequest(
        CompleteSetupRequest? request,
        out AppModels.CompleteSetupRequest mapped,
        out string messageKey)
    {
        mapped = default!;
        messageKey = SetupApiMessageKeys.Validation.InvalidRequestBody;

        if (request is null)
        {
            return false;
        }

        if (request.Ldap is null)
        {
            messageKey = SetupApiMessageKeys.Validation.InvalidLdapSettings;
            return false;
        }

        mapped = new AppModels.CompleteSetupRequest(
            request.SetupKey ?? string.Empty,
            MapLdapSettings(request.Ldap),
            MapModules(request.Modules),
            (request.AdminUsers ?? Array.Empty<CompleteSetupAdminUserRequest>())
                .Select(adminUser => new AppModels.CompleteSetupAdminUser(
                    adminUser.UserName ?? string.Empty,
                    adminUser.DistinguishedName,
                    adminUser.DirectoryObjectId))
                .ToList());

        return true;
    }

    public static bool TryMapValidateLdapRequest(
        ValidateLdapRequest? request,
        out AppModels.ValidateSetupLdapRequest mapped,
        out string messageKey)
    {
        mapped = default!;
        messageKey = SetupApiMessageKeys.Validation.InvalidRequestBody;

        if (request is null)
        {
            return false;
        }

        mapped = new AppModels.ValidateSetupLdapRequest(
            request.SetupKey ?? string.Empty,
            MapLdapSettings(request));

        return true;
    }

    public static bool TryMapSearchAdminUsersRequest(
        SearchSetupAdminUsersRequest? request,
        out AppModels.SearchSetupAdminUsersRequest mapped,
        out string messageKey)
    {
        mapped = default!;
        messageKey = SetupApiMessageKeys.Validation.InvalidRequestBody;

        if (request is null)
        {
            return false;
        }

        if (request.Ldap is null)
        {
            messageKey = SetupApiMessageKeys.Validation.InvalidLdapSettings;
            return false;
        }

        mapped = new AppModels.SearchSetupAdminUsersRequest(
            request.SetupKey ?? string.Empty,
            MapLdapSettings(request.Ldap),
            request.Search ?? string.Empty);

        return true;
    }

    private static AppModels.CompleteSetupLdapSettings MapLdapSettings(CompleteSetupLdapSettingsRequest ldap) =>
        new(
            ldap.Name ?? string.Empty,
            ldap.Host ?? string.Empty,
            ldap.BaseDn ?? string.Empty,
            ldap.UserSearchBase ?? string.Empty,
            ldap.UserSearchFilter ?? string.Empty,
            ldap.BindUserName ?? string.Empty,
            ldap.BindUserDomain,
            ldap.BindPassword ?? string.Empty);

    private static AppModels.CompleteSetupLdapSettings MapLdapSettings(ValidateLdapRequest request) =>
        new(
            Name: "Default LDAP",
            request.Host ?? string.Empty,
            request.BaseDn ?? string.Empty,
            request.UserSearchBase ?? string.Empty,
            request.UserSearchFilter ?? string.Empty,
            request.BindUserName ?? string.Empty,
            request.BindUserDomain,
            request.BindPassword ?? string.Empty);

    private static AppModels.CompleteSetupModulesSettings MapModules(CompleteSetupModulesRequest? modules) =>
        new(modules?.AdManagement is null
            ? null
            : new AppModels.CompleteSetupAdManagementModuleSettings(
                modules.AdManagement.IsEnabled,
                modules.AdManagement.UsersSearchBase,
                modules.AdManagement.GroupsSearchBase,
                modules.AdManagement.ComputersSearchBase,
                modules.AdManagement.DefaultUserOu,
                modules.AdManagement.DefaultGroupOu,
                modules.AdManagement.DefaultComputerOu,
                modules.AdManagement.DeletedObjectsEnabled));
}
