using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdManagementControllerUserMutationGuardTests
{
    private static readonly Dictionary<string, string> ExpectedUserMutationPermissions = new(StringComparer.Ordinal)
    {
        ["POST:users"] = AdManagementPermissions.UsersCreate,
        ["POST:users/{id}/enable"] = AdManagementPermissions.UsersEnable,
        ["POST:users/{id}/disable"] = AdManagementPermissions.UsersDisable,
        ["POST:users/{id}/unlock"] = AdManagementPermissions.UsersUnlock,
        ["POST:users/{id}/move-ou"] = AdManagementPermissions.UsersMoveOu,
        ["POST:users/{id}/groups"] = AdManagementPermissions.UsersGroupsAdd,
        ["DELETE:users/{id}/groups"] = AdManagementPermissions.UsersGroupsRemove,
        ["PUT:users/{id}"] = AdManagementPermissions.UsersUpdate,
        ["PUT:users/{id}/manager"] = AdManagementPermissions.UsersUpdate,
        ["PUT:users/{id}/account-expiration"] = AdManagementPermissions.UsersUpdate,
    };

    [Fact]
    public void UserMutationEndpoints_HaveRequirePermissionMatchingConstants()
    {
        var controllerType = typeof(AdManagementController);
        var templatePrefix = "api/ad-management/";

        foreach (var (routeKey, expectedPermission) in ExpectedUserMutationPermissions)
        {
            var method = FindMutationMethod(controllerType, routeKey)
                ?? throw new InvalidOperationException($"Mutation endpoint not found: {routeKey}");

            var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.NotNull(permissionAttribute);
            Assert.Equal(
                RequirePermissionAttribute.PolicyPrefix + expectedPermission,
                permissionAttribute!.Policy);
        }

        Assert.Equal(
            ExpectedUserMutationPermissions.Count,
            CountUserMutationMethods(controllerType, templatePrefix));
    }

    [Fact]
    public void AdOperationLogsController_ReadEndpoints_UseOperationLogsViewPermission()
    {
        var controllerType = typeof(AdOperationLogsController);
        var readMethods = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .ToList();

        Assert.NotEmpty(readMethods);
        Assert.All(readMethods, method =>
        {
            var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.NotNull(permissionAttribute);
            Assert.Equal(
                RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.OperationLogsView,
                permissionAttribute!.Policy);
        });
    }

    private static int CountUserMutationMethods(Type controllerType, string templatePrefix)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Count(method =>
            {
                var httpMethod = GetHttpMethod(method);
                if (httpMethod is null || httpMethod is "GET")
                {
                    return false;
                }

                var routeTemplate = BuildRouteTemplate(method, templatePrefix);
                var routeKey = $"{httpMethod}:{NormalizeRoute(routeTemplate)}";
                return ExpectedUserMutationPermissions.ContainsKey(routeKey);
            });
    }

    private static MethodInfo? FindMutationMethod(Type controllerType, string routeKey)
    {
        var separatorIndex = routeKey.IndexOf(':');
        var httpMethod = routeKey[..separatorIndex];
        var normalizedRoute = routeKey[(separatorIndex + 1)..];

        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(GetHttpMethod(method), httpMethod, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var routeTemplate = BuildRouteTemplate(method, "api/ad-management/");
                return string.Equals(NormalizeRoute(routeTemplate), normalizedRoute, StringComparison.Ordinal);
            });
    }

    private static string BuildRouteTemplate(MethodInfo method, string templatePrefix)
    {
        var routeAttribute = method.GetCustomAttributes()
            .OfType<IRouteTemplateProvider>()
            .FirstOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.Template));

        var actionTemplate = routeAttribute?.Template ?? string.Empty;
        return $"{templatePrefix}{actionTemplate}".TrimEnd('/');
    }

    private static string? GetHttpMethod(MethodInfo method)
    {
        if (method.GetCustomAttribute<HttpPostAttribute>() is not null)
        {
            return "POST";
        }

        if (method.GetCustomAttribute<HttpPutAttribute>() is not null)
        {
            return "PUT";
        }

        if (method.GetCustomAttribute<HttpDeleteAttribute>() is not null)
        {
            return "DELETE";
        }

        if (method.GetCustomAttribute<HttpPatchAttribute>() is not null)
        {
            return "PATCH";
        }

        return null;
    }

    private static string NormalizeRoute(string routeTemplate) =>
        routeTemplate
            .Replace("api/ad-management/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim('/');
}
