namespace ITAdmin.Application.Common.AdManagement;

public enum AdGroupScope
{
    Global,
    DomainLocal,
    Universal,
    Unknown,
}

public sealed record AdGroupTypeInfo(bool SecurityEnabled, AdGroupScope Scope);

public static class AdGroupTypeHelper
{
    private const int GlobalGroupFlag = 0x00000002;
    private const int DomainLocalGroupFlag = 0x00000004;
    private const int UniversalGroupFlag = 0x00000008;
    private const int SecurityEnabledFlag = unchecked((int)0x80000000);

    public static AdGroupTypeInfo Parse(int? groupTypeRaw)
    {
        if (groupTypeRaw is null)
        {
            return new AdGroupTypeInfo(false, AdGroupScope.Unknown);
        }

        var value = groupTypeRaw.Value;
        var securityEnabled = (value & SecurityEnabledFlag) != 0;
        var scope = ResolveScope(value);
        return new AdGroupTypeInfo(securityEnabled, scope);
    }

    public static string ScopeToCode(AdGroupScope scope) =>
        scope switch
        {
            AdGroupScope.Global => "Global",
            AdGroupScope.DomainLocal => "DomainLocal",
            AdGroupScope.Universal => "Universal",
            _ => "Unknown",
        };

    public static bool TryParseScopeCode(string? scopeCode, out AdGroupScope scope)
    {
        scope = AdGroupScope.Unknown;
        if (string.IsNullOrWhiteSpace(scopeCode))
        {
            return false;
        }

        scope = scopeCode.Trim() switch
        {
            "Global" => AdGroupScope.Global,
            "DomainLocal" => AdGroupScope.DomainLocal,
            "Universal" => AdGroupScope.Universal,
            _ => AdGroupScope.Unknown,
        };

        return scope is AdGroupScope.Global or AdGroupScope.DomainLocal or AdGroupScope.Universal;
    }

    public static int BuildSecurityGroupType(AdGroupScope scope) =>
        scope switch
        {
            AdGroupScope.Global => unchecked(SecurityEnabledFlag | GlobalGroupFlag),
            AdGroupScope.DomainLocal => unchecked(SecurityEnabledFlag | DomainLocalGroupFlag),
            AdGroupScope.Universal => unchecked(SecurityEnabledFlag | UniversalGroupFlag),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported group scope."),
        };

    private static AdGroupScope ResolveScope(int groupTypeRaw)
    {
        if ((groupTypeRaw & UniversalGroupFlag) != 0)
        {
            return AdGroupScope.Universal;
        }

        if ((groupTypeRaw & DomainLocalGroupFlag) != 0)
        {
            return AdGroupScope.DomainLocal;
        }

        if ((groupTypeRaw & GlobalGroupFlag) != 0)
        {
            return AdGroupScope.Global;
        }

        return AdGroupScope.Unknown;
    }
}
